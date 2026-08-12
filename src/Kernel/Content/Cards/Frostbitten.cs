using System.Collections.Generic;
namespace Kernel.Content.Cards;
/// <summary>冻僵｜无费(-1)｜干扰｜水｜Dross｜无目标｜无法打出。
/// 讨食灵「回赠」塞进弃牌堆的干扰牌(裁定⑥:同 STS2 伤口——不可打、不可升级)。
/// 无费用 → 不参与「讨要」的最高费比价:它堵抽牌,不挡刀。
/// 牌型走我们自己的 Dross(干扰:战斗结束消失,run 层落实),系别定水(元素灵造物)。</summary>
public sealed class Frostbitten : CardModel
{
    public Frostbitten()
        : base(-1, CardType.Dross, CardRarity.Dross, CardElement.Water, TargetType.None) { }
    public override int MaxUpgradeLevel => 0;
    protected override string TitleText => "冻僵";
    protected override string DescriptionTemplate => "无法打出。";
    protected override IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Unplayable };
}
