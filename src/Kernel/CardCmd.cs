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
            if (card.HasKeyword(CardKeyword.Unplayable)) return false;   // 自动打出免资源检查，但无法打出仍是无法打出
        }
        else
        {
            if (card.Pile?.Type != PileType.Hand) return false;          // 主动打出只能从手牌
            if (!card.CanPlay(out _, out _)) return false;
        }
        if (card.TargetType == TargetType.SingleEnemy)
        {
            if (target == null)
                throw new InvalidOperationException(
                    isAutoPlay ? "自动打出指向性卡的目标选择策略：等第一张这种内容出现时再定。"
                               : $"{card.Id} 需要目标。");
            if (target.IsDead) return false;
        }

        // ── 扣费（自动打出免费——遗言语义）──
        if (!isAutoPlay) pcs.LoseEnergy(card.Cost);

        // ── 入 Play 堆（物理隔离位）──
        card.Pile?.RemoveInternal(card);
        pcs.PlayPile.AddInternal(card);
        pcs.RecordPlayInternal(card, state.RoundNumber);   // 史书记账：与 CardPlayStarted 同刻（跟进同款——记在开始，读者排除自己）
        state.Events.Emit(new CardPlayStarted
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Card = card, Target = target, IsAutoPlay = isAutoPlay,
        });
        Fx.Sfx("card_play");
        await Fx.CustomScaledWait(0.05f, 0.1f);

        // ── 效果本体（多重打出的循环脚手架，现在恒为 1 次）──
        PileType destination = card.DestinationPileAfterPlay;
        const int playCount = 1;
        for (int i = 0; i < playCount; i++)
        {
            var play = new CardPlay
            {
                Card = card, Target = target, IsAutoPlay = isAutoPlay,
                PlayIndex = i, PlayCount = playCount,
            };
            await card.OnPlay(state, play);
        }

        // ── 虚无检查点（水密规则的落地）──
        // 位置：OnPlay 跑完、卡还在 Play 堆（它触发自己的虚无时必须还算"打出中"）。
        // 主人限定 + 边沿触发（NihilityArmed）+ 只对打出中的这张执行冒号效果。
        // Step E 注意：回合末 Flush 清手【不设】检查点——否则每回合末必触发一次
        //（不休陀螺的 IsPlayPhase 守卫，同一个教训）。
        if (pcs.Hand.IsEmpty && pcs.NihilityArmed)
        {
            state.Events.Emit(new NihilityTriggered
            {
                Round = state.RoundNumber, Side = state.CurrentSide, Player = card.Owner!,
            });
            if (card.Pile?.Type == PileType.Play)
                await card.OnNihility(state);
            await Hook.AfterNihilityTriggered(state, card.Owner!);
        }
        pcs.NihilityArmed = !pcs.Hand.IsEmpty;   // 检查点【末尾】重新武装（冒号效果可能刚补了牌）

        // ── 去向 ──
        if (card.Pile?.Type == PileType.Play)    // 效果可能已把自己搬走，搬走就不重复处理
        {
            if (destination == PileType.Exhaust)
            {
                await CardPileCmd.Exhaust(state, card);      // 走消耗动词 → 燃料触发
            }
            else
            {
                pcs.PlayPile.RemoveInternal(card);
                CardPile.Get(destination, card.Owner!)!.AddInternal(card);
            }
        }
        state.Events.Emit(new CardPlayFinished
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Card = card, Destination = card.Pile?.Type ?? destination,
        });
        await Hook.AfterCardPlayed(state, card, target);
        return true;
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