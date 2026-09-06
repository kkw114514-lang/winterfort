using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>血战｜1 费｜法术｜蓝｜消耗。【v2】失去生命 = 裸掉血(不吃盾,喂烈性)。升级:去掉消耗。</summary>
public sealed class Bloodletting : CardModel
{
    public Bloodletting()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "血战";
    protected override string DescriptionTemplate => "失去 {Loss} 点生命。获得 {Str} 点力量。余烬 1。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Loss", 5m), new DynamicVar("Str", 4m) };

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.LoseHp(state, Owner!.Creature, Vars["Loss"].Int);
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        CardModel junk = state.CreateCard<Ember>(Owner!);
        await CardPileCmd.AddGenerated(state, junk, PileType.Discard);
    }
}
