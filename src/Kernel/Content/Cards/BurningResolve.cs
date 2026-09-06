using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>燃烧斗志｜1 费｜法术｜蓝｜消耗。打出后【本回合】每打 1 张牌 +1 力(不含自身,从消耗堆听)。
/// 升级:费用→0。</summary>
public sealed class BurningResolve : CardModel
{
    private CombatState? _armedCombat;
    private int _armedRound;

    public BurningResolve()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "燃烧斗志";
    protected override string DescriptionTemplate => "本回合每打出 1 张牌，获得 {Per} 点力量。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Per", 1m) };

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override Task OnPlay(CombatState state, CardPlay play)
    {
        _armedCombat = state;
        _armedRound = state.RoundNumber;
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (_armedCombat is not { } state || state.RoundNumber != _armedRound) return;
        if (card == this || card.Owner != Owner) return;
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Per"].Int, this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _armedCombat = null;
        _armedRound = 0;
    }
}
