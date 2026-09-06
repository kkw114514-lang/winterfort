using System.Collections.Generic;

namespace Kernel.Content.Cards;

/// <summary>余烬｜1 费｜干扰｜火｜Dross｜无目标｜无效果,打出即消耗(裁定2:纯过牌代价)。
/// 战斗结束消失(Dross);算干扰牌(灰飞烟灭吃它)。不可升级。</summary>
public sealed class Ember : CardModel
{
    public Ember()
        : base(1, CardType.Dross, CardRarity.Dross, CardElement.Fire, TargetType.None) { }

    public override int MaxUpgradeLevel => 0;

    protected override string TitleText => "余烬";
    protected override string DescriptionTemplate => "无效果。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
}
