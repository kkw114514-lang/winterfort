using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>投薪｜1 费｜法术｜火｜Common｜消耗。升级:去「消耗」。</summary>
public sealed class FeedTheFire : CardModel
{
    public FeedTheFire()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "投薪";
    protected override string DescriptionTemplate => "获得 {Str} 点力量。焚毁 2。消耗。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Str", 4m) };
    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        await CardSelectCmd.Immolate(state, Owner!, 2, this);
    }
}
