using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel.Content.Cards;
namespace Kernel.Content.Monsters;
/// <summary>
/// 讨食灵｜元素灵｜48–52 血。严格循环:抢夺 11 → 回赠 7+塞 1 冻僵入弃牌堆 → 囤积。
/// 被动·讨要(玩家回合结束,逐契约师):弃牌堆中费用最高的 1 张移入消耗堆,
/// 同费随机(CombatCardSelection 流);意图亮着「囤积」的那一拍吃 2 张(执行拍无动作)。
/// 无费用牌(CanonicalCost&lt;0,即冻僵)不参与比价。
/// 【原创机制,无 STS2 判例】边界逐一断言:空弃牌堆/全同费/只剩冻僵。
/// 它打的不是你的血,是你的牌组——拖得越久,好牌被吃得越多(设计稿原话)。
/// </summary>
public sealed class Waif : MonsterModel
{
    public override int MinInitialHp => 48;
    public override int MaxInitialHp => 52;
    private static readonly MonsterMove Snatch  = new() { Name = "抢夺", Kind = IntentKind.Attack, BaseDamage = 11 };
    private static readonly MonsterMove Handout = new() { Name = "回赠", Kind = IntentKind.Attack, BaseDamage = 7 };
    private static readonly MonsterMove Hoard   = new() { Name = "囤积", Kind = IntentKind.Buff };
    protected override MonsterMove RollMove(Rng ai, CombatState state) => Cycle(Snatch, Handout, Hoard);
    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Hoard) return;                                     // 效果在被动侧
        if (NextMove == Handout)
        {
            Creature? target = state.GetOpponentsOf(Creature).FirstOrDefault(c => c.IsAlive);
            if (target == null) return;
            Player? victim = target.Player ?? target.PetOwner;
            await CreatureCmd.Damage(state, Creature, new[] { target }, 7, ValueProp.Move, null);
            if (victim?.PlayerCombatState != null)
            {
                CardModel junk = state.CreateCard<Frostbitten>(victim);
                await CardPileCmd.AddGenerated(state, junk, PileType.Discard);
            }
            return;
        }
        await base.TakeTurn(state);                                        // 抢夺
    }
    /// <summary>讨要。</summary>
    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Creature is not { IsAlive: true } me || me.CombatState is not { } state) return;
        int count = NextMove == Hoard ? 2 : 1;
        foreach (Creature pact in state.Allies.Where(c => c.Player != null && c.IsAlive).ToList())
        {
            PlayerCombatState? pcs = pact.Player!.PlayerCombatState;
            if (pcs == null) continue;
            for (int i = 0; i < count; i++)
            {
                List<CardModel> candidates = pcs.DiscardPile.Cards
                    .Where(c => c.HasCost).ToList();
                if (candidates.Count == 0) break;                          // 空/只剩冻僵:落空
                int maxCost = candidates.Max(c => c.Cost);
                List<CardModel> top = candidates.Where(c => c.Cost == maxCost).ToList();
                CardModel pick = state.RngSet[RngStream.CombatCardSelection].NextItem(top);
                await CardPileCmd.Exhaust(state, pick);
            }
        }
    }
}
