using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>引火索｜0 费｜攻击｜火｜Common。燃料:对随机敌造成 {FuelDamage}(CombatTargets 流)。</summary>
public sealed class Fuse : CardModel
{
    public Fuse()
        : base(0, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "引火索";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。燃料：对随机 1 个敌人造成 {FuelDamage} 点伤害。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 6m), new DynamicVar("FuelDamage", 6m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Fuel };

    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(3m);
        Vars["FuelDamage"].UpgradeBy(3m);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);

    protected internal override async Task OnFuel(CombatState state)
    {
        var alive = state.Enemies.Where(e => e.IsAlive).ToList();
        if (alive.Count == 0) return;
        Creature t = alive[state.RngSet[RngStream.CombatTargets].NextInt(alive.Count)];
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { t },
            Vars["FuelDamage"].Int, ValueProp.Move, this);
    }
}
