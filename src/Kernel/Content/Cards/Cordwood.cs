using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>薪柴｜0 费｜法术｜火｜Common。燃料:抽 2。升级:加「保留」。</summary>
public sealed class Cordwood : CardModel
{
    public Cordwood()
        : base(0, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "薪柴";
    protected override string DescriptionTemplate => "抽 1 张牌。燃料：抽 2 张牌。";
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Fuel };

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await CardPileCmd.Draw(state, Owner!, 1);

    protected internal override async Task OnFuel(CombatState state)
        => await CardPileCmd.Draw(state, Owner!, 2);
}
