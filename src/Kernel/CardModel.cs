using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Kernel;

/// <summary>附魔（腐化）的正面部分。祭坛获得，一张牌至多一个，不能洗掉。</summary>
public abstract class EnchantmentModel : GameModel
{
    public abstract string Describe();
}

/// <summary>附魔（腐化）的负面部分。与正面独立，各占一个槽。</summary>
public abstract class AfflictionModel : GameModel
{
    public abstract string Describe();
}

/// <summary>
/// 一张卡的定义 + 运行时状态（canonical / mutable 双态，见 GameModel）。
///
/// 【本轮只有数据层】OnPlay 的签名取决于 CombatContext，等战斗层建好再加。
/// </summary>
public abstract class CardModel : GameModel
{
    // ════════ 定义性属性：构造时定死，永不改变 ════════

    public int CanonicalCost { get; }
    public CardType Type { get; }
    public CardRarity Rarity { get; }
    public CardElement Element { get; }
    public TargetType TargetType { get; }

    protected CardModel(int cost, CardType type, CardRarity rarity, CardElement element, TargetType targetType)
    {
        CanonicalCost = cost;
        _baseCost = cost;
        Type = type;
        Rarity = rarity;
        Element = element;
        TargetType = targetType;
    }

    /// <summary>
    /// 是否进掉落池。两类例外：
    ///   ① 通用基石（Basic）——只作起始卡组素材
    ///   ② 眷属专属卡——只能靠获得该眷属取得
    /// </summary>
    public bool IsInDropPool => Element != CardElement.Basic && !IsFamiliarSignature;

    /// <summary>眷属专属卡覆写成 true。</summary>
    public virtual bool IsFamiliarSignature => false;

    // ════════ 文本 ════════

    /// <summary>
    /// 卡名。真实项目里应该指向本地化表的 key，现在先硬编码。
    /// （STS2 的做法：类名 Slugify 之后就是 loc key，一个字都不用手写。）
    /// </summary>
    protected abstract string TitleText { get; }

    /// <summary>描述模板。{Damage} 会被替换成同名 DynamicVar 的 Preview 值。</summary>
    protected abstract string DescriptionTemplate { get; }

    public string Title
    {
        get
        {
            if (CurrentUpgradeLevel == 0) return TitleText;
            return MaxUpgradeLevel > 1
                ? $"{TitleText}+{CurrentUpgradeLevel}"
                : TitleText + "+";
        }
    }

    private static readonly Regex VarPattern = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

    /// <summary>数值与文案同源：模板里的 {X} 直接读 DynamicVar，不可能漂移。</summary>
    public string Describe() => VarPattern.Replace(DescriptionTemplate, m =>
    {
        string name = m.Groups[1].Value;
        return Vars.Has(name) ? Vars[name].PreviewInt.ToString() : m.Value;
    });

    // ════════ 数值 ════════

    protected virtual IEnumerable<DynamicVar> CanonicalVars => Array.Empty<DynamicVar>();

    private DynamicVarSet? _vars;
    public DynamicVarSet Vars => _vars ??= new DynamicVarSet(CanonicalVars);

    // ════════ 费用 ════════
    //
    // 完整形态是四层：Canonical →（升级）→ _baseCost →（本回合修正）→（全局 hook）
    // 后两层要等战斗层。

    private int _baseCost;
    public int Cost => Math.Max(0, _baseCost);

    protected void UpgradeCostBy(int addend)
    {
        AssertMutable();
        _baseCost += addend;
    }

    // ════════ 关键词与标记 ════════

    protected virtual IEnumerable<CardKeyword> CanonicalKeywords => Array.Empty<CardKeyword>();
    protected virtual IEnumerable<CardTag> CanonicalTags => Array.Empty<CardTag>();

    private HashSet<CardKeyword>? _keywords;
    private HashSet<CardTag>? _tags;

    private HashSet<CardKeyword> KeywordSet => _keywords ??= new HashSet<CardKeyword>(CanonicalKeywords);
    private HashSet<CardTag> TagSet => _tags ??= new HashSet<CardTag>(CanonicalTags);

    /// <summary>仅供渲染遍历。行为判定用 HasKeyword。</summary>
    public IReadOnlyCollection<CardKeyword> Keywords => KeywordSet;
    public IReadOnlyCollection<CardTag> Tags => TagSet;

    public bool HasKeyword(CardKeyword keyword) => KeywordSet.Contains(keyword);
    public bool HasTag(CardTag tag) => TagSet.Contains(tag);

    protected void AddKeyword(CardKeyword keyword)
    {
        AssertMutable();
        KeywordSet.Add(keyword);
    }

    protected void RemoveKeyword(CardKeyword keyword)
    {
        AssertMutable();
        KeywordSet.Remove(keyword);
    }

    // ════════ 附魔（两个独立槽）════════

    public EnchantmentModel? Enchantment { get; private set; }
    public AfflictionModel? Affliction { get; private set; }

    internal void SetEnchantment(EnchantmentModel? e)
    {
        AssertMutable();
        Enchantment = e;
    }

    internal void SetAffliction(AfflictionModel? a)
    {
        AssertMutable();
        Affliction = a;
    }

    // ════════ 升级（支持多级）════════

    public int CurrentUpgradeLevel { get; private set; }

    /// <summary>
    /// 默认只能升一级。可反复升级的卡覆写它。
    ///
    /// 【别用 int.MaxValue】即使设计上是"无限升级"，也要给一个大但有限的哨兵值：
    ///   · 防御性——某个 bug 导致循环升级时不会死循环 / 数值溢出
    ///   · 读档要重放 N 次 Upgrade()，N 无界意味着读档时间无界
    ///   · UI —— "灼热打击+2147483647" 会撑爆卡面
    /// 一局撑死升二十来次，给 99 玩家永远碰不到上限。
    /// </summary>
    public virtual int MaxUpgradeLevel => 1;

    public bool IsUpgraded => CurrentUpgradeLevel > 0;
    public bool IsUpgradable => CurrentUpgradeLevel < MaxUpgradeLevel;

    /// <summary>
    /// 升级 = 对同一个实例做一次原地 mutation。没有第二个类、没有 "+" 后缀类名。
    ///
    /// 【存档】只存 CurrentUpgradeLevel 这一个 int，读档时重放 N 次。
    /// 多级升级不增加任何存档复杂度。
    /// 副作用：你以后调整升级公式，老存档里的卡会自动吃到新公式——这是有意的取舍。
    /// </summary>
    public void Upgrade()
    {
        AssertMutable();
        if (!IsUpgradable) return;
        CurrentUpgradeLevel++;
        OnUpgrade();
    }

    /// <summary>升级预览展示完毕后调用，清掉数值上的"刚变过"高亮标记。</summary>
    public void FinalizeUpgrade() => Vars.FinalizeUpgrade();

    /// <summary>
    /// 卡自己只声明【增量】：改数值、改费用、加关键词，或什么都不改
    /// （靠 IsUpgraded 分支改行为）。
    ///
    /// 多级升级时它每级被调一次，可以读 CurrentUpgradeLevel 让增量递增/递减。
    /// 灼热打击式：UpgradeBy(CurrentUpgradeLevel + 3) → +4, +5, +6, +7…
    /// </summary>
    protected virtual void OnUpgrade() { }
    // ════════ 战斗归属（批次 2 加入）════════

    /// <summary>这张牌属于谁。由 CombatState.CreateCard 设置；canonical 上恒为 null。</summary>
    public Player? Owner { get; private set; }

    /// <summary>这张牌现在躺在哪个堆（由 CardPile 的 Add/RemoveInternal 维护）。</summary>
    public CardPile? Pile { get; private set; }

    internal void SetOwner(Player owner)
    {
        AssertMutable();
        Owner = owner;
    }

    internal void SetPile(CardPile? pile)
    {
        AssertMutable();
        Pile = pile;
    }

    // ════════ 能否打出 ════════

    /// <summary>
    /// 【STS2 无参形态】卡从自己的 Owner 拿战斗状态，资源判定整体委托给
    /// PlayerCombatState.HasEnoughResourcesFor——加第二种资源时本签名不变。
    /// BlockedByHook / NoValidTarget 等 Hook 总线（Step B）接上后在这追加，
    /// preventer 到那时才有值。
    /// </summary>
    public bool CanPlay(out UnplayableReason reason, out GameModel? preventer)
    {
        reason = UnplayableReason.None;
        preventer = null;

        if (Owner?.PlayerCombatState is not { } resources)
            throw new InvalidOperationException(
                $"{Id} 不在战斗中（Owner/PlayerCombatState 为空）。CanPlay 是战斗内的问题，" +
                "牌库预览等场景不该调它。");

        if (HasKeyword(CardKeyword.Unplayable))
            reason |= UnplayableReason.HasUnplayableKeyword;

        if (!resources.HasEnoughResourcesFor(this, out UnplayableReason resourceReason))
            reason |= resourceReason;

        if (!CanPlayByCardLogic())
            reason |= UnplayableReason.BlockedByCardLogic;

        return reason == UnplayableReason.None;
    }

    /// <summary>常用路径便捷版。要知道"为什么打不出"用带 out 的版本。</summary>
    public bool CanPlay() => CanPlay(out _, out _);

    /// <summary>卡自身的额外条件（"只有手牌为空时才能打出"这类）。</summary>
    protected virtual bool CanPlayByCardLogic() => true;

    // ════════ 克隆纪律 ════════

    protected override void DeepCloneFields()
    {
        // 每一个可变集合都必须在这里重新 new。漏一个 → 升级时加的关键词会漏到
        // canonical 定义上，全世界这张卡一起变。编译器不会提醒你。
        _keywords = new HashSet<CardKeyword>(KeywordSet);
        _tags = new HashSet<CardTag>(TagSet);
        _vars = Vars.Clone();

        // 附魔本身也是 GameModel，副本要有自己的一份
        if (Enchantment != null) Enchantment = Enchantment.MutableCloneAs<EnchantmentModel>();
        if (Affliction != null) Affliction = Affliction.MutableCloneAs<AfflictionModel>();
    }

    protected override void AfterCloned()
    {
        // 归属是运行时状态，副本必须从零开始——MemberwiseClone 会把引用一起拷走。
        Owner = null;
        Pile = null;

        // 目前还没有 event。战斗层加了 Drawn / Discarded / Exhausted 之类之后，
        // 每一个都必须在这里置 null —— MemberwiseClone 会把委托链一起复制。
    }
}