using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>趁热｜1 费｜攻击｜火｜Common｜单体。等级判定在打出时读目标(≥Ⅱ 达标)。升级:7/9→10/10。</summary>
public sealed class HotIron : CardModel
{
    public HotIron()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "趁热";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。若目标的灼伤等级达到Ⅱ，伤害+{Bonus}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 7m), new DynamicVar("Bonus", 9m) };

    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(3m);
        Vars["Bonus"].UpgradeBy(1m);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int level = play.Target!.GetBuff<SearBuff>()?.Level ?? 0;
        int dmg = Vars.Damage.Int + (level >= 2 ? Vars["Bonus"].Int : 0);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
    }
}
