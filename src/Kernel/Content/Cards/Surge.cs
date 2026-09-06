using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>狂涌｜3 费｜攻击｜火｜Uncommon。费用 = 3 − 全场最高灼伤等级(裁定7,微裁定9:
/// Cost 属性即层链出口,预览=结算同源;封底 0 由出口承担)。升级:12→15。</summary>
public sealed class Surge : CardModel
{
    public Surge()
        : base(3, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "狂涌";
    protected override string DescriptionTemplate => "场上最高灼伤等级每有 1，这张卡费用 -1。造成 {Damage} 点伤害。获得 1 点力量。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 12m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    public override int ModifyEnergyCost(CardModel card, int cost)
    {
        if (card != this) return cost;
        CombatState? cs = Owner?.Creature.CombatState;
        if (cs == null) return cost;
        int max = 0;
        foreach (Creature c in cs.Creatures)
        {
            int lv = c.GetBuff<SearBuff>()?.Level ?? 0;
            if (lv > max) max = lv;
        }
        return cost - max;
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, 1, this);
    }
}
