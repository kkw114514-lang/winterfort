using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>掷火｜1 费｜攻击｜火｜Common。升级:12→15。</summary>
public sealed class ThrowFire : CardModel
{
    public ThrowFire()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "掷火";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。焚毁 1。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 12m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        await CardSelectCmd.Immolate(state, Owner!, 1, this);
    }
}
