using System;
using System.Collections.Generic;

namespace Kernel;

/// <summary>
/// 战斗里"能被打的身体"。玩家、怪物、同伴是同一个类，靠三个可空字段区分：
///
///   Player   非空 → 玩家的身体
///   Monster  非空 → 怪物或同伴的身体
///   PetOwner 非空 → 这是同伴（此时 Monster 也非空，Side 是 Player 侧）
///
/// 为什么不做三个子类：伤害管线只关心"有血有盾的东西"，三个类会让
/// LoseHp 写三遍或被迫上继承树（STS2 的选择相同）。
///
/// 它【不是】GameModel——运行时实体没有 canonical 形态。
///
/// 【偏离 STS2 #3 的体现】STS2 在 Block/CurrentHp 的 setter 里挂 C# 事件给 UI；
/// 我们不挂，表现层统一从事件流拿通知。代价是一条纪律：所有游戏性改动必须
/// 走 Cmd 层（Cmd 发事件）；绕过 Cmd 直捅 Internal 的改动，表现层看不见。
/// </summary>
public sealed class Creature
{
    /// <summary>护盾封顶（同 STS2 的 999）。</summary>
    public const int MaxBlock = 999;

    private int _block;
    private int _currentHp;
    private int _maxHp;
    private readonly List<BuffModel> _buffs = new();
    private Player? _petOwner;

    public int Block
    {
        get => _block;
        private set
        {
            if (value < 0) throw new ArgumentException("护盾不能为负", nameof(value));
            _block = value;
        }
    }

    public int CurrentHp
    {
        get => _currentHp;
        private set
        {
            if (value < 0) throw new ArgumentException("血量不能为负", nameof(value));
            _currentHp = value;
        }
    }

    public int MaxHp
    {
        get => _maxHp;
        private set
        {
            if (value < 0) throw new ArgumentException("血量上限不能为负", nameof(value));
            _maxHp = value;
        }
    }

    public Player? Player { get; }
    public MonsterModel? Monster { get; }
    public CombatSide Side { get; }

    /// <summary>
    /// 入场时由 CombatState 分配（下一批）。【确定性的地基】所有需要稳定顺序的
    /// 地方（随机选目标、日志排序）必须按它排——List 下标会因死亡移除而漂移，
    /// 引用哈希每次运行都不同，都不许用。
    /// </summary>
    public uint? CombatId { get; internal set; }
    /// <summary>所在的战斗。入场时由 CombatState 填，战斗结束置回 null（Step E）。</summary>
    public CombatState? CombatState { get; internal set; }

    /// <summary>我的同伴们（转发到 PlayerCombatState，同 STS2）。不在战斗中 = 空表。</summary>
    public IReadOnlyList<Creature> Pets =>
        Player?.PlayerCombatState?.Pets ?? Array.Empty<Creature>();
    /// <summary>同伴的主人。同 STS2：一经设置不许改（防止"换主人"这种未定义状态）。</summary>
    public Player? PetOwner
    {
        get => _petOwner;
         set
        {
            if (_petOwner != null)
                throw new InvalidOperationException($"{this} 已经有主人 {_petOwner} 了。");
            _petOwner = value;
        }
    }

    public bool IsPlayer => Player != null;
    public bool IsMonster => Monster != null;
    public bool IsPet => _petOwner != null;
    public bool IsAlive => CurrentHp > 0;
    public bool IsDead => !IsAlive;

    public IReadOnlyList<BuffModel> Buffs => _buffs;

    public string Name => Player?.Name ?? Monster?.Id.Entry ?? "???";
    public override string ToString() => Name;

    // ══ 构造 ══

    /// <summary>玩家的身体。只由 Player 的构造调用。</summary>
    internal Creature(Player player, int maxHp)
    {
        Player = player;
        Side = CombatSide.Player;
        MaxHp = maxHp;
        CurrentHp = maxHp;
    }

    /// <summary>
    /// 怪物/同伴的身体。maxHp 由调用方先掷好再传进来（下一批的
    /// CombatState.CreateCreature 会包掉这个构造并接上 MonsterHp 流）。
    /// </summary>
    public Creature(MonsterModel monster, CombatSide side, int maxHp)
    {
        if (!monster.IsMutable)
            throw new ArgumentException(
                $"{monster.Id} 是 canonical 定义，不能拿定义当身体入场。" +
                $"用 ModelRegistry.New<{monster.GetType().Name}>() 取 mutable 副本。");
        Monster = monster;
        monster.Creature = this;
        Side = side;
        MaxHp = maxHp;
        CurrentHp = maxHp;
    }

    // ══ Internal 层：只改状态。不跑 hook、不发事件、不播表现。只准 Cmd 层调用。 ══
    // public 而非 C# internal 的原因：Kernel.Headless 是另一个程序集，断言要直接敲。
    // 约束靠后缀契约执行——看到 Internal 就知道这是裸状态操作（STS2 同名同做法）。

    /// <summary>裸加盾。返回实际加上的量（撞到封顶会少于 amount）。</summary>
    public int GainBlockInternal(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负，掉盾走 LoseBlockInternal", nameof(amount));
        int before = Block;
        Block = Math.Min(Block + amount, MaxBlock);
        return Block - before;
    }

    /// <summary>裸掉盾（"失去所有护盾"类效果用）。返回实际失去的量。</summary>
    public int LoseBlockInternal(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        int lost = Math.Min(Block, amount);
        Block -= lost;
        return lost;
    }

    /// <summary>
    /// 护盾吃伤害，返回实际挡掉的量（同 STS2 DamageBlockInternal）。
    /// 进出全是 int：唯一取整点在上游 ModifyDamage 链末尾，这里之后不存在小数。
    /// </summary>
    public int DamageBlockInternal(int amount, ValueProp props)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        int blocked = props.HasFlag(ValueProp.Unblockable) ? 0 : Math.Min(Block, amount);
        Block -= blocked;
        return blocked;
    }

    /// <summary>
    /// 裸扣血（同 STS2 LoseHpInternal）。溢出量记进结果单——
    /// 同伴代受被打死时，管线拿 OverkillDamage 回流给主人。
    /// </summary>
    public DamageResult LoseHpInternal(int amount, ValueProp props)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        bool killed = CurrentHp > 0 && amount >= CurrentHp;
        int before = CurrentHp;
        CurrentHp = Math.Max(CurrentHp - amount, 0);
        return new DamageResult(this, props)
        {
            UnblockedDamage = before - CurrentHp,
            OverkillDamage  = amount - (before - CurrentHp),
            WasTargetKilled = killed,
        };
    }

    /// <summary>裸治疗，封顶在 MaxHp。返回实际回复量。</summary>
    public int HealInternal(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        int before = CurrentHp;
        CurrentHp = Math.Min(CurrentHp + amount, MaxHp);
        return CurrentHp - before;
    }

    // ── buff 挂载（只由 BuffModel.ApplyInternal / RemoveInternal 反向调用）──

    internal void AddBuffInternal(BuffModel buff)
    {
        if (_buffs.Contains(buff))
            throw new InvalidOperationException($"{Name} 身上已经有这个 {buff.Id} 实例了。");
        _buffs.Add(buff);
    }

    internal void RemoveBuffInternal(BuffModel buff)
    {
        if (!_buffs.Remove(buff))
            throw new InvalidOperationException($"{Name} 身上没有这个 {buff.Id} 实例。");
    }

    // ── buff 查询 ──

    public bool HasBuff<T>() where T : BuffModel
    {
        foreach (BuffModel b in _buffs)
            if (b is T) return true;
        return false;
    }

    public T? GetBuff<T>() where T : BuffModel
    {
        foreach (BuffModel b in _buffs)
            if (b is T t) return t;
        return null;
    }

    /// <summary>没有该 buff 时返回 0——"力量为 0"和"没有力量"在数值上等价。</summary>
    public int GetBuffAmount<T>() where T : BuffModel
        => GetBuff<T>()?.Amount ?? 0;
}