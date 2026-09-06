using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火烧连环｜1 费｜法术｜金。抽 1 并盯住它;本回合它被打出 → 重复本效果。
/// 从弃牌堆里听。升级:费用→0。</summary>
public sealed class ChainBlaze : CardModel
{
    private CardModel? _tracked;
    private CombatState? _armedCombat;
    private int _armedRound;

    public ChainBlaze()
        : base(1, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "火烧连环";
    protected override string DescriptionTemplate => "抽 1 张牌。本回合若打出这张抽到的牌，重复本效果。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await DrawAndTrack(state);

    private async Task DrawAndTrack(CombatState state)
    {
        _tracked = (await CardPileCmd.Draw(state, Owner!, 1)).FirstOrDefault();
        _armedCombat = _tracked != null ? state : null;
        _armedRound = state.RoundNumber;
    }

    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (_armedCombat is not { } state || state.RoundNumber != _armedRound) return;
        if (card != _tracked) return;
        await DrawAndTrack(state);                             // 链:再抽再盯
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _tracked = null;
        _armedCombat = null;
        _armedRound = 0;
    }
}
