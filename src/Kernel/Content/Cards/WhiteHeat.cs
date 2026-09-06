using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>白热化｜1 费｜攻击｜金｜消耗。打出后本回合每张攻击牌 → 其目标等级+Ⅰ
/// (对无灼伤目标走裁定5)。升级:费用→0。</summary>
public sealed class WhiteHeat : CardModel
{
    private CombatState? _armedCombat;
    private int _armedRound;

    public WhiteHeat()
        : base(1, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "白热化";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。本回合每打出 1 张攻击牌，其目标的灼伤等级+Ⅰ。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 5m) };

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        _armedCombat = state;
        _armedRound = state.RoundNumber;
    }

    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (_armedCombat is not { } state || state.RoundNumber != _armedRound) return;
        if (card == this || card.Owner != Owner || card.Type != CardType.Attack) return;
        if (target is not { IsAlive: true } || target.Side != CombatSide.Enemy) return;
        await BuffCmd.RaiseSearLevel(state, target, 1, this);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _armedCombat = null;
        _armedRound = 0;
    }
}
