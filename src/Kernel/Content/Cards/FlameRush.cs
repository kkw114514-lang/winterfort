using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火焰冲击｜2 费｜攻击｜火｜Uncommon｜单体。计数不含自身(裁定12)。升级:每张 +3→+5。</summary>
public sealed class FlameRush : CardModel
{
    public FlameRush()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "火焰冲击";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。本回合每打过一张牌，伤害+{Per}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 12m), new DynamicVar("Per", 3m) };

    protected override void OnUpgrade() => Vars["Per"].UpgradeBy(2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int dmg = Vars.Damage.Int + Vars["Per"].Int * PlayedThisTurnBesidesThis(state);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
    }
}
