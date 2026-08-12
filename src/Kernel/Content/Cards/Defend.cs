using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>防御｜1 费｜法术｜通用基石｜Basic｜自身。</summary>
public sealed class Defend : CardModel
{
    public Defend()
        : base(1, CardType.Spell, CardRarity.Basic, CardElement.Basic, TargetType.Self) { }

    protected override string TitleText => "防御";
    protected override string DescriptionTemplate => "获得 {Shield} 点护盾。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Shield", 5m) };

    protected override void OnUpgrade() => Vars.Shield.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.GainBlock(state, Owner!.Creature, Vars.Shield.Int, ValueProp.Move, this);
}