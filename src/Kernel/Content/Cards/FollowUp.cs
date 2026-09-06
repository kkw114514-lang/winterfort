using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>追击｜1 费｜攻击｜火｜Common｜单体。门槛 3 张,不含自身(裁定12)。升级:8/8→10/10。</summary>
public sealed class FollowUp : CardModel
{
    public FollowUp()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "追击";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。若本回合已经打出 3 张或以上的牌，伤害+{Bonus}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 8m), new DynamicVar("Bonus", 8m) };

    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(2m);
        Vars["Bonus"].UpgradeBy(2m);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int dmg = Vars.Damage.Int + (PlayedThisTurnBesidesThis(state) >= 3 ? Vars["Bonus"].Int : 0);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
    }
}
