using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>公式件(卡表备注原文):大量 = 5 + n×(x²+x−1),x=目标灼伤等级、n=层数;巨额 = ×2。
/// 力量不入公式——公式只出"基础伤害",力量与目标灼伤乘算在管线里照常发生(微裁定1)。</summary>
internal static class SearBlast
{
    public static int Of(Creature target)
    {
        SearBuff? s = target.GetBuff<SearBuff>();
        int x = s?.Level ?? 0, n = s?.Amount ?? 0;
        return 5 + n * (x * x + x - 1);
    }
}

/// <summary>殉爆｜2 费｜攻击｜火｜Uncommon。升级:大量→巨额(档位跳变,非数值增量)。</summary>
public sealed class CookOff : CardModel
{
    public CookOff()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "殉爆";
    protected override string DescriptionTemplate => IsUpgraded
        ? "根据灼伤程度对目标造成巨额伤害。焚毁 2。"
        : "根据灼伤程度对目标造成大量伤害。焚毁 2。";

    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() { }   // 跳档走 IsUpgraded 分支

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int dmg = SearBlast.Of(play.Target!);
        if (IsUpgraded) dmg *= 2;
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
        await CardSelectCmd.Immolate(state, Owner!, 2, this);
    }
}
