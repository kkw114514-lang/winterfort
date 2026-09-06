using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>挥霍｜1 费｜法术｜火｜Common。升级:焚毁 2→1(负增量)。</summary>
public sealed class Squander : CardModel
{
    public Squander()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "挥霍";
    protected override string DescriptionTemplate => "抽 3 张牌。焚毁 {Burn}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Burn", 2m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars["Burn"].UpgradeBy(-1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CardPileCmd.Draw(state, Owner!, 3);
        await CardSelectCmd.Immolate(state, Owner!, Vars["Burn"].Int, this);
    }
}
