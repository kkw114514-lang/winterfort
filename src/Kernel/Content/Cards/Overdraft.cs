using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>透支｜0 费｜法术｜金｜消耗。回合末从消耗堆里听:剩余能量每 1 点自灼 1 层。升级:4→6。</summary>
public sealed class Overdraft : CardModel
{
    private CombatState? _armedCombat;
    private int _armedRound;

    public Overdraft()
        : base(0, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "透支";
    protected override string DescriptionTemplate => "获得 {Energy} 点能量。本回合结束时，剩余能量每有 1 点，对自己施加 1 层灼伤。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Energy", 4m) };

    protected override void OnUpgrade() => Vars["Energy"].UpgradeBy(2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CombatCmd.GainEnergy(state, Owner!, Vars["Energy"].Int);
        _armedCombat = state;
        _armedRound = state.RoundNumber;
    }

    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (_armedCombat is not { } state || state.RoundNumber != _armedRound) return;
        _armedCombat = null;                                   // 一次性
        int remaining = Owner!.PlayerCombatState?.Energy ?? 0;
        if (remaining > 0)
            await BuffCmd.Apply<SearBuff>(state, Owner.Creature, remaining, this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _armedCombat = null;
        _armedRound = 0;
    }
}
