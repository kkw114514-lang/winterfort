using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>突进｜0 费｜攻击｜火｜Common｜单体。计数不含自身(裁定12)。升级:每张 +2→+3。</summary>
public sealed class Dash : CardModel
{
    public Dash()
        : base(0, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "突进";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。本回合每打过一张牌，伤害+{Per}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 3m), new DynamicVar("Per", 2m) };

    protected override void OnUpgrade() => Vars["Per"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int dmg = Vars.Damage.Int + Vars["Per"].Int * PlayedThisTurnBesidesThis(state);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
    }
}
