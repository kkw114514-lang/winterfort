using System.Linq;
using System.Threading.Tasks;
using Kernel;

namespace TestContent;

// 注意：OnPlay/OnFuel/OnNihility 在 Kernel 里是 protected internal；
// 跨程序集覆写时 C# 规定只写 protected override（否则 CS0507）。

/// <summary>测：打出管线走伤害管线（吃力量/灼伤）。</summary>
public sealed class TestStrikePlay : CardModel
{
    public TestStrikePlay()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "试炼打击";
    protected override string DescriptionTemplate => "造成 6 点伤害。";

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! }, 6, ValueProp.Move, this);
}

/// <summary>测：打出获得护盾。</summary>
public sealed class TestDefendPlay : CardModel
{
    public TestDefendPlay()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Water, TargetType.Self) { }

    protected override string TitleText => "试炼防御";
    protected override string DescriptionTemplate => "获得 5 点护盾。";

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.GainBlock(state, Owner!.Creature, 5, ValueProp.Move, this);
}

/// <summary>测：抽牌动词。</summary>
public sealed class TestDrawTwo : CardModel
{
    public TestDrawTwo()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Grass, TargetType.None) { }

    protected override string TitleText => "汲取";
    protected override string DescriptionTemplate => "抽 2 张牌。";

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CardPileCmd.Draw(state, Owner!, 2);
}

/// <summary>测：消耗关键词 + 燃料（被消耗时 +3 盾）。</summary>
public sealed class TestFuelBlock : CardModel
{
    public TestFuelBlock()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Light, TargetType.None) { }

    protected override string TitleText => "薪柴";
    protected override string DescriptionTemplate => "燃料：获得 3 点护盾。消耗。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Exhaust };

    protected override async Task OnFuel(CombatState state)
        => await CreatureCmd.GainBlock(state, Owner!.Creature, 3, ValueProp.None, this);
}

/// <summary>测：遗言（被效果弃掉→免费打出自己：抽 1）。</summary>
public sealed class TestEpitaphDraw : CardModel
{
    public TestEpitaphDraw()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Dark, TargetType.None) { }

    protected override string TitleText => "回响";
    protected override string DescriptionTemplate => "抽 1 张牌。遗言。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Epitaph };

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CardPileCmd.Draw(state, Owner!, 1);
}

/// <summary>测：你当初举例虚无用的那张卡——"消耗手中一张卡。虚无：抽一张牌。消耗。"</summary>
public sealed class TestNihilityExhauster : CardModel
{
    public TestNihilityExhauster()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Dark, TargetType.None) { }

    protected override string TitleText => "归无";
    protected override string DescriptionTemplate => "消耗手中第一张卡。虚无：抽 1 张牌。消耗。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CombatState state, CardPlay play)
    {
        CardModel? first = Owner!.PlayerCombatState!.Hand.Cards.FirstOrDefault();
        if (first != null) await CardPileCmd.Exhaust(state, first);
    }

    protected override async Task OnNihility(CombatState state)
        => await CardPileCmd.Draw(state, Owner!, 1);
}

/// <summary>测：Aura → Removed（偏离 #6），被动直接写在卡上持续生效。</summary>
public sealed class TestAuraMight : CardModel
{
    public TestAuraMight()
        : base(1, CardType.Aura, CardRarity.Uncommon, CardElement.Fire, TargetType.None) { }

    protected override string TitleText => "余威";
    protected override string DescriptionTemplate => "永续：你的攻击 +1 伤害。";

    // 【翻案墓碑｜偏离#6已撤销】曾经的方案:打出后躺在 Removed 堆靠这个覆写持续生效。
    // 翻案后 Aura 打出即进 limbo(Pile=null):不在任何堆 = 不在 hook 名单,
    // 这个覆写【永远不会再被调用】。留作反证——永续的被动必须活在 buff 里
    // (Content/Buffs/Auras.cs),不能寄生在卡的钩子上。[7] 的"素 6 伤"断言踩着它验证。
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Pile?.Type != PileType.Removed) return 0m;   // 只在"已离场"状态生效
        if (dealer != Owner?.Creature) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return 1m;
    }
}

/// <summary>三案联测 A:指向攻击(裁定14 自动选靶)+遗言+消耗;虚无:+1 能量。</summary>
public sealed class TestVowEnergy : CardModel
{
    public TestVowEnergy()
        : base(1, CardType.Attack, CardRarity.Uncommon, CardElement.Dark, TargetType.SingleEnemy) { }

    protected override string TitleText => "试誓·能";
    protected override string DescriptionTemplate => "造成 1 点伤害。虚无：获得 1 点能量。遗言。消耗。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Exhaust, CardKeyword.Epitaph };

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! }, 1m, ValueProp.Move, this);

    protected override async Task OnNihility(CombatState state)
        => await CombatCmd.GainEnergy(state, Owner!, 1);
}

/// <summary>三案联测 B:指向攻击+遗言+消耗;虚无:+1 护盾。</summary>
public sealed class TestVowShield : CardModel
{
    public TestVowShield()
        : base(1, CardType.Attack, CardRarity.Uncommon, CardElement.Dark, TargetType.SingleEnemy) { }

    protected override string TitleText => "试誓·盾";
    protected override string DescriptionTemplate => "造成 2 点伤害。虚无：获得 1 点护盾。遗言。消耗。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Exhaust, CardKeyword.Epitaph };

    protected override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! }, 2m, ValueProp.Move, this);

    protected override async Task OnNihility(CombatState state)
        => await CreatureCmd.GainBlock(state, Owner!.Creature, 1m, ValueProp.Move, this);
}

/// <summary>三案联测 C(情况2):弃掉手中前两张,获得 2 点能量。</summary>
public sealed class TestDiscardTwoEnergy : CardModel
{
    public TestDiscardTwoEnergy()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Dark, TargetType.Self) { }

    protected override string TitleText => "试案·弃而生能";
    protected override string DescriptionTemplate => "弃掉手中前两张牌。获得 2 点能量。";

    protected override async Task OnPlay(CombatState state, CardPlay play)
    {
        var pcs = Owner!.PlayerCombatState!;
        await CardCmd.Discard(state, pcs.Hand.Cards.Take(2).ToList());
        await CombatCmd.GainEnergy(state, Owner!, 2);
    }
}

/// <summary>三案联测 C(情况3):弃掉手中前两张;虚无:抽 1。</summary>
public sealed class TestDiscardTwoVowDraw : CardModel
{
    public TestDiscardTwoVowDraw()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Dark, TargetType.Self) { }

    protected override string TitleText => "试案·弃而后取";
    protected override string DescriptionTemplate => "弃掉手中前两张牌。虚无：抽 1 张牌。";

    protected override async Task OnPlay(CombatState state, CardPlay play)
    {
        var pcs = Owner!.PlayerCombatState!;
        await CardCmd.Discard(state, pcs.Hand.Cards.Take(2).ToList());
    }

    protected override async Task OnNihility(CombatState state)
        => await CardPileCmd.Draw(state, Owner!, 1);
}
