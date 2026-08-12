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
/// 【偏离 #3 已逆转，回归 STS2】Block/CurrentHp 等 setter 挂属性级 C# 事件（门铃）
/// 供表现层数值绑定；事件流职责收窄为：日志/确定性测试/语义时刻（抽牌≠检索）。
/// 【两层可见性不同步】绕过 Cmd 的裸改会响铃（UI 看得见）但不发流事件（日志看不见）——
/// 游戏性改动必须走 Cmd 的纪律不变。
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
            if (_block == value) return;               // 等值不响（STS2 守卫原样）
            int old = _block;
            _block = value;
            BlockChanged?.Invoke(old, _block);
        }
    }

    public int CurrentHp
    {
        get => _currentHp;
        private set
        {
            if (value < 0) throw new ArgumentException("血量不能为负", nameof(value));
            if (_currentHp == value) return;
            int old = _currentHp;
            _currentHp = value;
            CurrentHpChanged?.Invoke(old, _currentHp);
        }
    }

    public int MaxHp
    {
        get => _maxHp;
        private set
        {
            if (value < 0) throw new ArgumentException("血量上限不能为负", nameof(value));
            if (_maxHp == value) return;
            int old = _maxHp;
            _maxHp = value;
            MaxHpChanged?.Invoke(old, _maxHp);
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

    // ── 属性级门铃（签名与 STS2 Creature.cs 逐一对齐）──
    // 订阅纪律：表现层订阅者只读、不许改状态；成对退订（进树 += / 出树 -=）；
    // 处理器不依赖铃的先后顺序（每声独立回读）。异常由动作级围栏（TaskHelper）接。
    public event Action<int, int>? BlockChanged;        // (old, new)
    public event Action<int, int>? CurrentHpChanged;    // (old, new)
    public event Action<int, int>? MaxHpChanged;        // (old, new)
    public event Action<BuffModel>? BuffApplied;
    public event Action<BuffModel, int, bool>? BuffIncreased;   // (buff, 增量, silent)——silent 雇主后到，恒 false
    public event Action<BuffModel, bool>? BuffDecreased;        // (buff, silent)——STS2 原样：减不带量，订阅者回读
    public event Action<BuffModel>? BuffRemoved;
    
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
    
    /// <summary>上限与当前同加（STS2 GainMaxHp 语义——同伴活着再召唤用）。</summary>
    public void GainMaxHpInternal(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        MaxHp += amount;
        CurrentHp += amount;
    }

    /// <summary>重设上限；当前值超出新上限时钳到上限。</summary>
    public void SetMaxHpInternal(int value)
    {
        if (value < 0) throw new ArgumentException("必须非负", nameof(value));
        MaxHp = value;
        if (CurrentHp > MaxHp) CurrentHp = MaxHp;
    }

    // ── buff 挂载（只由 BuffModel.ApplyInternal / RemoveInternal 反向调用）──

    internal void AddBuffInternal(BuffModel buff)
    {
        if (_buffs.Contains(buff))
            throw new InvalidOperationException($"{Name} 身上已经有这个 {buff.Id} 实例了。");
        _buffs.Add(buff);
        BuffApplied?.Invoke(buff);                     // 入名单后响（STS2 :504 原样）
    }

    internal void RemoveBuffInternal(BuffModel buff)
    {
        if (!_buffs.Remove(buff))
            throw new InvalidOperationException($"{Name} 身上没有这个 {buff.Id} 实例。");
        BuffRemoved?.Invoke(buff);
    }

    /// <summary>层数变动拉铃口（STS2 :511/:519 同构：增减分铃、减不带量）。
    /// 铃在 Creature 身上、不在 BuffModel 上——绕开"克隆连名单一起复制"的软肋（STS2 同款布局）。
    /// 只由 BuffModel.ChangeAmountInternal 反向调用。</summary>
    internal void RaiseBuffAmountChangedInternal(BuffModel buff, int delta)
    {
        if (delta > 0) BuffIncreased?.Invoke(buff, delta, false);
        else if (delta < 0) BuffDecreased?.Invoke(buff, false);
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