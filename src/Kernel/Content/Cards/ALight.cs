using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>借火｜0 费｜法术｜火｜Common。升级:加「保留」。</summary>
public sealed class ALight : CardModel
{
    public ALight()
        : base(0, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "借火";
    protected override string DescriptionTemplate => "获得 1 点能量。余烬 1。";

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CombatCmd.GainEnergy(state, Owner!, 1);
        await CardPileCmd.AddGenerated(state, state.CreateCard<Ember>(Owner!), PileType.Discard);
    }
}
