using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>焚牌｜0 费｜法术｜火｜Common。升级:抽 1→2。</summary>
public sealed class Tinder : CardModel
{
    public Tinder()
        : base(0, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "焚牌";
    protected override string DescriptionTemplate => "抽 {Cards} 张牌。焚毁 1。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Cards", 1m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars["Cards"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CardPileCmd.Draw(state, Owner!, Vars["Cards"].Int);
        await CardSelectCmd.Immolate(state, Owner!, 1, this);
    }
}
