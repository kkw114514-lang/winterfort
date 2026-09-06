using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel;

public static class CardCmd
{
    /// <summary>
    /// 打出流水线（评审定稿）：校验 → 扣费 → 入 Play 堆 → OnPlay×N →
    /// 虚无检查点 → 去向堆 → 收尾 hook。
    /// 返回 false = 没打出去（不可打/目标非法/能量不足）。
    /// </summary>
    public static async Task<bool> Play(CombatState state, CardModel card, Creature? target, bool isAutoPlay = false)
    {
        PlayerCombatState pcs = card.Owner?.PlayerCombatState
            ?? throw new InvalidOperationException($"{card.Id} 不在战斗中。");

        // ── 校验 ──
        if (isAutoPlay)
        {
            if (card.HasKeyword(CardKeyword.Unplayable))
            {
                await MoveToDestinationWithoutPlaying(state, card);   // 裁定14:不结算也要归位(同 STS2)
                return false;
            }
        }
        else
        {
            if (card.Pile?.Type != PileType.Hand) return false;      // 主动打出只能从手牌
            if (!card.CanPlay(out _, out _)) return false;
        }
        if (card.TargetType == TargetType.SingleEnemy)
        {
            if (target == null)
            {
                if (!isAutoPlay)
                    throw new InvalidOperationException($"{card.Id} 需要目标。");
                // 【裁定14】自动打出的指向卡:CombatTargets 流随机活敌;全场无活敌 → 落空直送去向堆。
                List<Creature> living = state.Enemies.Where(e => e.IsAlive).ToList();
                if (living.Count == 0)
                {
                    await MoveToDestinationWithoutPlaying(state, card);
                    return false;
                }
                target = living[state.RngSet[RngStream.CombatTargets].NextInt(living.Count)];
            }
            if (target.IsDead) return false;
        }

        // ── 扣费（自动打出免费——遗言语义；重放不重复扣费,费只在这一处扣一次）──
        if (!isAutoPlay) pcs.LoseEnergy(card.Cost);

        // ── 入 Play 堆（物理隔离位）──
        card.Pile?.RemoveInternal(card);
        pcs.PlayPile.AddInternal(card);
        Fx.Sfx("card_play");
        await Fx.CustomScaledWait(0.05f, 0.1f);

        // ── 次数与去向,循环前一次定死（STS2 CardModel:1455-1462 原序）──
        PileType destination = card.DestinationPileAfterPlay;
        int playCount = Hook.ModifyCardPlayCount(state, card, target, 1,
            out List<GameModel> playCountModifiers);
        await Hook.AfterModifyingCardPlayCount(state, card, playCountModifiers);

        // ── 效果本体:【逐次】节拍（STS2 1464-1508 原序）──
        for (int i = 0; i < playCount; i++)
        {
            if (i > 0)
            {
                Fx.Sfx("card_play");                     // 重放的第二声（AnimMultiCardPlay 位）
                await Fx.CustomScaledWait(0.05f, 0.1f);
            }
            var play = new CardPlay
            {
                Card = card, Target = target, IsAutoPlay = isAutoPlay,
                PlayIndex = i, PlayCount = playCount,
            };
            await Hook.BeforeCardPlayed(state, card, target);   // 裁定13:起点广播——逐次,与 After 成对
            pcs.RecordPlayInternal(card, state.RoundNumber);    // 史书逐次记账:重放=两条(STS2 双份账同义)
            state.Events.Emit(new CardPlayStarted
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Card = card, Target = target, IsAutoPlay = isAutoPlay,
            });
            await card.OnPlay(state, play);
            state.Events.Emit(new CardPlayFinished
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Card = card, Destination = destination,          // 去向是预告值:循环后才真搬家(同 STS2 ResultPile)
            });
            if (!state.IsOver)                                   // STS2 IsInProgress 守卫原样
                await Hook.AfterCardPlayed(state, card, target);
        }

        // ── 虚无检查点【裁定15 定稿】──
        // 原创机制登记:冒号效果只属于"打出中"的这张(守卫 Pile==Play),故检查点住在去向前;
        // 无状态——逐笔交易结账查一次,谁空手收场谁触发(武装已撤编,广播已随法令拆除);
        // 回合末 Flush 不设检查点(不休陀螺 IsPlayPhase 教训,结构性封死)。
        if (pcs.Hand.IsEmpty)
        {
            state.Events.Emit(new NihilityTriggered
            {
                Round = state.RoundNumber, Side = state.CurrentSide, Player = card.Owner!,
            });
            if (card.Pile?.Type == PileType.Play)
                await card.OnNihility(state);
        }

        // ── 去向 ──
        if (card.Pile?.Type == PileType.Play)    // 效果可能已把自己搬走，搬走就不重复处理(明耀打击家族)
        {
            if (destination == PileType.Exhaust)
            {
                await CardPileCmd.Exhaust(state, card);      // 走消耗动词 → 燃料触发
            }
            else if (destination == PileType.None)
            {
                pcs.PlayPile.RemoveInternal(card);           // 永续:离开牌堆宇宙(STS2 limbo 同款)
            }
            else
            {
                pcs.PlayPile.RemoveInternal(card);
                CardPile.Get(destination, card.Owner!)!.AddInternal(card);
            }
        }
        card.AfterPlayedCostCleanup();                       // 「直到打出」修正条离场（STS2 同位:去向后）
        return true;
    }

    /// <summary>【裁定14】落空路径(STS2 MoveToResultPileWithoutPlaying 同款):
    /// 不结算、不扣费、不进史书,入 Play 堆走一遭后按去向归位——消耗词条照走消耗动词(燃料照触发)。</summary>
    private static async Task MoveToDestinationWithoutPlaying(CombatState state, CardModel card)
    {
        PlayerCombatState pcs = card.Owner!.PlayerCombatState!;
        card.Pile?.RemoveInternal(card);
        pcs.PlayPile.AddInternal(card);
        PileType destination = card.DestinationPileAfterPlay;
        if (destination == PileType.Exhaust)
        {
            await CardPileCmd.Exhaust(state, card);
        }
        else if (destination == PileType.None)
        {
            pcs.PlayPile.RemoveInternal(card);
        }
        else
        {
            pcs.PlayPile.RemoveInternal(card);
            CardPile.Get(destination, card.Owner!)!.AddInternal(card);
        }
    }

    /// <summary>
    /// 【效果弃牌】动词。结构抄 STS2 的 Sly 三步：先记名 → 全部弃完 → 再逐张免费打出。
    /// 回合末清手是另一个动词（Flush，Step E），天然不触发遗言——双动词纪律。
    /// </summary>
    public static async Task Discard(CombatState state, IEnumerable<CardModel> cards)
    {
        List<CardModel> list = cards.ToList();
        if (list.Count == 0) return;

        var epitaphs = new List<CardModel>();
        foreach (CardModel card in list)
        {
            if (card.Pile?.Type != PileType.Hand)
                throw new InvalidOperationException($"{card.Id} 不在手牌，弃牌动词只弃手牌。");
            if (card.HasKeyword(CardKeyword.Epitaph))
                epitaphs.Add(card);                          // ① 先记名

            PlayerCombatState pcs = card.Owner!.PlayerCombatState!;
            pcs.Hand.RemoveInternal(card);                   // ② 弃完
            pcs.DiscardPile.AddInternal(card);
            state.Events.Emit(new CardDiscarded
            {
                Round = state.RoundNumber, Side = state.CurrentSide, Card = card,
            });
            Fx.Sfx("card_discard");
            await Fx.CustomScaledWait(0.02f, 0.05f);
            await Hook.AfterCardDiscarded(state, card);
        }

        foreach (CardModel card in epitaphs)                 // ③ 遗言：免费打出自己
            await Play(state, card, null, isAutoPlay: true);
    }

    public static Task Discard(CombatState state, CardModel card)
        => Discard(state, new[] { card });
}