using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>
/// 换步｜0 费｜法术｜火｜Familiar（眷属专属：随炎舞入库/离库）｜自身｜消耗。
/// 窗口【本回合】（跟进同款）：史书倒数第二条 + 回合号过滤。
/// 检索 = 拿到手、不算摸牌（STS2 判例：受膏/乱斗全是 Where→洗→取→入手的内联组合，
/// 没有专用动词——第二张检索卡出现时再抽公共动词）。
/// 落空三连（判例+裁定）：无上一张 / 无匹配 / 手满 → 什么都不发生，照常消耗。
/// </summary>
public sealed class Shiftstep : CardModel
{
    public Shiftstep()
        : base(0, CardType.Spell, CardRarity.Familiar, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "换步";

    protected override string DescriptionTemplate => CurrentUpgradeLevel > 0
        ? "抽 1 张牌。若本回合你打出的上一张牌是攻击牌，从抽牌堆随机抽 1 张非攻击牌；若是非攻击牌，随机抽 1 张攻击牌。消耗。"
        : "若本回合你打出的上一张牌是攻击牌，从抽牌堆随机抽 1 张非攻击牌；若是非攻击牌，随机抽 1 张攻击牌。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        PlayerCombatState pcs = Owner!.PlayerCombatState!;

        if (CurrentUpgradeLevel > 0)
            await CardPileCmd.Draw(state, Owner!, 1);       // 升级：先无条件抽 1（摸牌不进史书，不影响窗口）

        var history = pcs.PlayHistory;
        if (history.Count < 2) return;                      // 无上一张 → 落空
        PlayRecord prev = history[history.Count - 2];       // 最后一条是换步自己
        if (prev.Round != state.RoundNumber) return;        // 只认本回合 → 落空

        bool fetchAttack = prev.Card.Type != CardType.Attack;   // 上一张非攻击 → 检索攻击；反之
        List<CardModel> candidates = pcs.DrawPile.Cards
            .Where(c => (c.Type == CardType.Attack) == fetchAttack).ToList();
        if (candidates.Count == 0) return;                  // 无匹配 → 落空（不翻弃牌堆）
        if (pcs.Hand.Count >= CardPile.MaxCardsInHand) return;  // 手满 → 落空，牌留抽牌堆

        CardModel pick = state.RngSet[RngStream.CombatCardSelection].NextItem(candidates);
        await CardPileCmd.Move(state, pick, pcs.Hand);
    }
}