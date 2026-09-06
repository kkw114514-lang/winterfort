using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>灰烬护盾｜1 费｜法术｜火｜Common。升级:11→14。</summary>
public sealed class AshShield : CardModel
{
    public AshShield()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "灰烬护盾";
    protected override string DescriptionTemplate => "获得 {Shield} 点护盾。余烬 1。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Shield", 11m) };

    protected override void OnUpgrade() => Vars["Shield"].UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.GainBlock(state, Owner!.Creature, Vars["Shield"].Int, ValueProp.Move, this);
        await CardPileCmd.AddGenerated(state, state.CreateCard<Ember>(Owner!), PileType.Discard);
    }
}
