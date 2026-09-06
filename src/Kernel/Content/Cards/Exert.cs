using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>奋击｜1 费｜攻击｜白｜消耗。力量在前,这一刀自己吃到。升级:力量 1→2。</summary>
public sealed class Exert : CardModel
{
    public Exert()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "奋击";
    protected override string DescriptionTemplate => "获得 {Str} 点力量。造成 {Damage} 点伤害。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Str", 1m), new DynamicVar("Damage", 9m) };

    protected override void OnUpgrade() => Vars["Str"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
    }
}
