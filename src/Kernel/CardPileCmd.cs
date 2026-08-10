using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>牌堆动词层：抽 / 生成 / 消耗 / 通用搬家 / 洗回。</summary>
public static class CardPileCmd
{
    /// <summary>
    /// 抽牌（规则同 STS2）：满 10 不抽（牌留在原地）；抽牌堆空则把弃牌堆洗回；
    /// 洗完还空就作罢。逐张：移动 → 事件 → hook。
    /// </summary>
    public static async Task<List<CardModel>> Draw(CombatState state, Player player, int count)
    {
        var drawn = new List<CardModel>();
        PlayerCombatState pcs = player.PlayerCombatState
            ?? throw new InvalidOperationException($"{player} 不在战斗中，抽不了牌。");

        for (int i = 0; i < count; i++)
        {
            if (pcs.Hand.Count >= CardPile.MaxCardsInHand) break;
            if (pcs.DrawPile.IsEmpty)
                await ReshuffleDiscardIntoDraw(state, player);
            if (pcs.DrawPile.IsEmpty) break;

            CardModel card = pcs.DrawPile.Cards[0];          // 下标 0 = 堆顶
            pcs.DrawPile.RemoveInternal(card);
            pcs.Hand.AddInternal(card);

            drawn.Add(card);
            state.Events.Emit(new CardDrawn
            {
                Round = state.RoundNumber, Side = state.CurrentSide, Card = card,
            });
            Fx.Sfx("card_draw");
            await Fx.Wait(0.08f);
            await Hook.AfterCardDrawn(state, card);
        }
        return drawn;
    }

    /// <summary>弃牌堆整体洗回抽牌堆（同 STS2）。洗牌用 Shuffle 流——战斗内第一个随机消费者。</summary>
    public static async Task ReshuffleDiscardIntoDraw(CombatState state, Player player)
    {
        PlayerCombatState pcs = player.PlayerCombatState!;
        if (pcs.DiscardPile.IsEmpty) return;

        int moved = 0;
        while (!pcs.DiscardPile.IsEmpty)
        {
            CardModel card = pcs.DiscardPile.Cards[0];
            pcs.DiscardPile.RemoveInternal(card);
            pcs.DrawPile.AddInternal(card);
            moved++;
        }
        pcs.DrawPile.ShuffleInternal(state.RngSet[RngStream.Shuffle]);

        state.Events.Emit(new PilesReshuffled
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Player = player, CardCount = moved,
        });
        Fx.Sfx("shuffle");
        await Fx.Wait(0.2f);
    }

    /// <summary>
    /// 战斗内生成一张牌进某个堆。目标是手牌且已满 → 改进弃牌堆（STS2 实证规则）。
    /// 用法：state.CreateCard&lt;T&gt;(player) 之后调这里。
    /// </summary>
    public static async Task<CardModel> AddGenerated(CombatState state, CardModel card, PileType to)
    {
        if (card.Owner == null)
            throw new InvalidOperationException($"{card.Id} 没有 Owner——先走 CombatState.CreateCard。");
        if (card.Pile != null)
            throw new InvalidOperationException($"{card.Id} 已在 {card.Pile.Type} 堆里，不是新生牌。");

        PlayerCombatState pcs = card.Owner.PlayerCombatState!;
        PileType final = to;
        if (final == PileType.Hand && pcs.Hand.Count >= CardPile.MaxCardsInHand)
            final = PileType.Discard;                        // 满手改道（同 STS2）

        CardPile pile = CardPile.Get(final, card.Owner)
            ?? throw new InvalidOperationException($"生成目的地 {final} 不是有效牌堆。");
        pile.AddInternal(card);

        state.Events.Emit(new CardGenerated
        {
            Round = state.RoundNumber, Side = state.CurrentSide, Card = card, To = final,
        });
        Fx.Vfx("card_generate", card.Owner.Creature);
        await Fx.Wait(0.1f);
        await Hook.AfterCardGenerated(state, card);
        return card;
    }

    /// <summary>
    /// 消耗一张牌。三种途径（自带消耗关键词打完、被效果消耗、临时关键词回合末）
    /// 全走本动词——所以燃料"都触发"不需要任何分支。
    /// 顺序：先入消耗堆，再触发燃料（燃料效果执行时卡已在消耗堆，与"甲"监听一致）。
    /// </summary>
    public static async Task Exhaust(CombatState state, CardModel card)
    {
        CardPile from = card.Pile
            ?? throw new InvalidOperationException($"{card.Id} 不在任何堆里，无从消耗。");
        PlayerCombatState pcs = card.Owner!.PlayerCombatState!;

        from.RemoveInternal(card);
        pcs.ExhaustPile.AddInternal(card);

        state.Events.Emit(new CardExhausted
        {
            Round = state.RoundNumber, Side = state.CurrentSide, Card = card,
        });
        Fx.Vfx("card_exhaust", card.Owner.Creature);
        await Fx.Wait(0.1f);

        await card.OnFuel(state);                            // 燃料
        await Hook.AfterCardExhausted(state, card);
    }

    /// <summary>通用搬家（Removed↔手牌之类的内容效果用）。专有动词有专有事件，别用这个代替。</summary>
    public static async Task Move(CombatState state, CardModel card, CardPile to)
    {
        CardPile from = card.Pile
            ?? throw new InvalidOperationException($"{card.Id} 不在任何堆里——出生走 AddGenerated。");
        if (from == to) return;

        from.RemoveInternal(card);
        to.AddInternal(card);
        state.Events.Emit(new CardMoved
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Card = card, From = from.Type, To = to.Type,
        });
        await Fx.Wait(0.05f);
    }
}