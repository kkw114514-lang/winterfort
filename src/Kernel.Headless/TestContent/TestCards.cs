using System.Collections.Generic;
using Kernel;

namespace TestContent;

// ══════════════════════════════════════════════════════════════════
// 测试素材，不是游戏内容。
//
// 它们存在的唯一理由：CardModel 是抽象类，必须有具体子类才能验证它工作。
// 放在 Kernel.Headless 里，所以永远不会被打包进游戏。
// 每张卡对应一组断言要验证的性质，名字直接写明它测什么。
// ══════════════════════════════════════════════════════════════════

/// <summary>测：基础数值 + 单级升级 + 描述渲染。</summary>
public sealed class TestBasicAttack : CardModel
{
    public TestBasicAttack()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "余烬";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 6m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);
}

/// <summary>测：非 Damage 的数值键 + 自我目标。</summary>
public sealed class TestShieldSkill : CardModel
{
    public TestShieldSkill()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Water, TargetType.Self) { }

    protected override string TitleText => "涟漪";
    protected override string DescriptionTemplate => "获得 {Shield} 点护盾。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Shield", 5m) };

    protected override void OnUpgrade() => Vars.Shield.UpgradeBy(3m);
}

/// <summary>测：关键词集合的深拷贝隔离 + 升级改费用 + 升级加关键词。</summary>
public sealed class TestKeywordCard : CardModel
{
    public TestKeywordCard()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "炭火";
    protected override string DescriptionTemplate => "对所有敌人造成 {Damage} 点伤害。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 8m) };

    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(4m);
        UpgradeCostBy(-1);
        AddKeyword(CardKeyword.Retain);
    }
}

/// <summary>
/// 测：多级升级 + 递增增量。灼热打击式：基础 12，第 n 次升级 +(n+3)。
/// 伤害 = n(n+7)/2 + 12 → 12, 16, 21, 27, 34, 42…
/// </summary>
public sealed class TestMultiUpgrade : CardModel
{
    public TestMultiUpgrade()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Grass, TargetType.SingleEnemy) { }

    public override int MaxUpgradeLevel => 99;

    protected override string TitleText => "灼烧";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。可以被反复升级。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 12m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(CurrentUpgradeLevel + 3);
}

/// <summary>测：无法打出关键词参与 CanPlay 收口。</summary>
public sealed class TestUnplayableCurse : CardModel
{
    public TestUnplayableCurse()
        : base(0, CardType.Bane, CardRarity.Special, CardElement.Dark, TargetType.None) { }

    protected override string TitleText => "枷锁";
    protected override string DescriptionTemplate => "无法打出。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Unplayable, CardKeyword.Eternal };
}