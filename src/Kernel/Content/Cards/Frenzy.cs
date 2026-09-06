using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>狂战｜2 费｜法术｜火｜Uncommon｜消耗。微裁定5:「本回合攻击牌」=你本场全部攻击牌
/// (含抽牌堆/弃牌堆——回合内抽到照样享受),回合末由 EndOfTurnCleanup 统一过期。升级:力 3→4。</summary>
public sealed class Frenzy : CardModel
{
    public Frenzy()
        : base(2, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "狂战";
    protected override string DescriptionTemplate => "获得 {Str} 点力量。本回合攻击牌的费用 -1。消耗。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Str", 3m) };
    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => Vars["Str"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        foreach (CardModel card in Owner!.PlayerCombatState!.AllCards)
            if (card.Type == CardType.Attack)
                card.AddCostThisTurn(-1);
    }
}
