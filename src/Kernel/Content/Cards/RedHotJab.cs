using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>赤热刺｜2 费｜攻击｜白。升级:15 伤/2 灼/2 弱。</summary>
public sealed class RedHotJab : CardModel
{
    public RedHotJab()
        : base(2, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "赤热刺";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。施加 {Sear} 层灼伤。施加 {Weak} 层弱化。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 14m), new DynamicVar("Sear", 1m), new DynamicVar("Weak", 1m) };

    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(1m);
        Vars["Sear"].UpgradeBy(1m);
        Vars["Weak"].UpgradeBy(1m);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        await BuffCmd.Apply<SearBuff>(state, play.Target!, Vars["Sear"].Int, this);
        await BuffCmd.Apply<WeakenBuff>(state, play.Target!, Vars["Weak"].Int, this);
    }
}
