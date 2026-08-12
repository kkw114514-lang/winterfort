using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel;

/// <summary>
/// 玩家【只活一场战斗】的部分（对照 Player：整局）。战斗结束整个丢掉。
/// </summary>
public sealed class PlayerCombatState
{
    private readonly Player _player;
    private readonly List<Creature> _pets = new();
    private CardPile[]? _allPiles;
    private int _energy;
    
    /// <summary>虚无的边沿触发标志：上个检查点末尾手牌非空 = 已武装。
    /// 防止空手状态下的连锁自动打出反复触发（水密规则第②条）。</summary>
    internal bool NihilityArmed = true;

    public CardPile Hand        { get; } = new(PileType.Hand);
    public CardPile DrawPile    { get; } = new(PileType.Draw);
    public CardPile DiscardPile { get; } = new(PileType.Discard);
    public CardPile ExhaustPile { get; } = new(PileType.Exhaust);
    public CardPile PlayPile    { get; } = new(PileType.Play);
    public CardPile RemovedPile { get; } = new(PileType.Removed);

    /// <summary>
    /// 六个战斗堆，顺序写死（前五个同 STS2 的 AllPiles，Removed 排最后）。
    /// 这一个名单同时服务：所有权/存档/数牌 + hook 分发（IterateHookListeners 遍历它）。
    /// 【你的设计决定】不拆两个名单——移出区的牌被动仍生效。
    /// </summary>
    public IReadOnlyList<CardPile> AllPiles =>
        _allPiles ??= new[] { Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile, RemovedPile };

    /// <summary>这个玩家在本场战斗里的全部牌。</summary>
    public IEnumerable<CardModel> AllCards => AllPiles.SelectMany(p => p.Cards);

    public IReadOnlyList<Creature> Pets => _pets;

    // ── 出牌史书（规则查询走这里，事件流保持纯表现——同 STS2 History 与事件分离）──

    private readonly List<PlayRecord> _playHistory = new();

    /// <summary>本场战斗的出牌史。条目在打出【开始】时记账（CardCmd.Play），
    /// 所以正在结算的牌就是最后一条——读者查"上一张"用倒数第二条。
    /// 【按位置排除、不按对象比对】同一张牌被捞回重打时对象会重复出现，
    /// 按对象排除会误伤合法条目。</summary>
    public IReadOnlyList<PlayRecord> PlayHistory => _playHistory;

    internal void RecordPlayInternal(CardModel card, int round)
        => _playHistory.Add(new PlayRecord(card, round));
    
    /// <summary>能量门铃（STS2 PCS:90 逐字对齐）。</summary>
    public event Action<int, int>? EnergyChanged;
    
    public int Energy
    {
        get => _energy;
        private set
        {
            if (value < 0) throw new ArgumentException("能量不能为负", nameof(value));
            if (_energy == value) return;
            int old = _energy;
            _energy = value;
            EnergyChanged?.Invoke(old, _energy);
        }
    }

    /// <summary>Hook 总线建好后（Step B），这里变成 hook 挂点：
    /// Hook.ModifyMaxEnergy(...)——"薪火之源"那类提升上限的卡从那接入。</summary>
    public int MaxEnergy => _player.MaxEnergy;

    internal PlayerCombatState(Player player) => _player = player;

    public void ResetEnergy() => Energy = MaxEnergy;

    public void GainEnergy(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负，扣能量走 LoseEnergy", nameof(amount));
        Energy += amount;
    }

    /// <summary>"失去至多 N 点"语义：超出持有量截断到 0（游戏惯例，"失去所有能量"依赖它）。</summary>
    public void LoseEnergy(int amount)
    {
        if (amount < 0) throw new ArgumentException("必须非负", nameof(amount));
        Energy = Math.Max(0, Energy - amount);
    }

    /// <summary>
    /// 资源判定的唯一收口（同 STS2）。今天只有能量；第二种资源落地时改动
    /// 全在这一个方法内 + UnplayableReason 加一位——CanPlay 的签名不动。
    /// </summary>
    public bool HasEnoughResourcesFor(CardModel card, out UnplayableReason reason)
    {
        int cost = Math.Max(0, card.Cost);
        reason = UnplayableReason.None;
        if (cost > Energy) reason |= UnplayableReason.EnergyTooHigh;
        return reason == UnplayableReason.None;
    }

    /// <summary>同伴登记（同 STS2 AddPetInternal）：PetOwner 未设则设上——
    /// 已属他人时由 setter 的一次性守卫抛。</summary>
    public void AddPetInternal(Creature pet)
    {
        if (!pet.IsMonster)
            throw new ArgumentException("同伴必须是怪物身体（MonsterModel + Player 侧）", nameof(pet));
        if (_pets.Contains(pet))
            throw new InvalidOperationException($"{pet} 已在同伴名单里。");
        if (pet.PetOwner != _player) pet.PetOwner = _player;
        _pets.Add(pet);
    }
}
/// <summary>史书条目。窗口（本回合/本场战斗）是【查询条件】不是存储属性——
/// 跟进查本回合、腰带抽打查全场，同一本史书换个过滤而已。Round 供读者自滤。</summary>
public readonly record struct PlayRecord(CardModel Card, int Round);