using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>
/// 生物动词层。完整语义的唯一收口：问 hook → 走流程 → 改 Internal → 发事件 → 打节拍。
/// 内容代码（卡/buff/怪招）只准调这里，绕过 Cmd 直捅 Internal 就是 bug。
/// </summary>
public static class CreatureCmd
{
    /// <summary>
    /// 伤害管线（讨论定稿的 ①-⑧）。多目标按调用方传入的顺序结算（调用方负责按 CombatId 排）。
    /// 返回全部结果单：转移发生时一次打击产生两条（同伴的 + 主人溢出回流的）。
    /// </summary>
    public static async Task<List<DamageResult>> Damage(CombatState state, Creature? dealer,
        IReadOnlyList<Creature> targets, decimal baseAmount, ValueProp props, CardModel? source)
    {
        var all = new List<DamageResult>();

        foreach (Creature originalTarget in targets)
        {
            if (originalTarget.IsDead) continue;

            // ① 修正链 + 全局唯一取整点
            int dmg = Hook.ModifyDamage(state, originalTarget, dealer, baseAmount, props, source, out _);

            // ②
            await Hook.BeforeDamageReceived(state, originalTarget, dmg, props, dealer, source);

            // ③ 同伴用主人的盾
            Creature blockHolder = originalTarget.PetOwner?.Creature ?? originalTarget;
            int blocked = blockHolder.DamageBlockInternal(dmg, props);

            // ④ 按【被瞄准者】结算的减伤
            int unblocked = Hook.ModifyHpLostBeforePet(
                state, originalTarget, Math.Max(dmg - blocked, 0), props, dealer, source, out _);

            // ⑤ 伤害转移：换人不改值
            Creature realTarget = Hook.ModifyUnblockedDamageTarget(state, originalTarget, unblocked, props, dealer);

            // ⑥ 按【实际挨打者】结算的减伤
            unblocked = Hook.ModifyHpLostAfterPet(state, realTarget, unblocked, props, dealer, source, out _);

            // ⑦ 扣血
            DamageResult mainResult = realTarget.LoseHpInternal(unblocked, props);
            bool blockBroken = blockHolder.Block <= 0 && blocked > 0;
            bool fullyBlocked = !props.HasFlag(ValueProp.Unblockable)
                                && (blocked > 0 || blockHolder.Block > 0)
                                && unblocked == 0;

            var hits = new List<DamageResult>(2) { mainResult };

            if (realTarget == originalTarget)
            {
                mainResult.BlockedDamage = blocked;
                mainResult.WasBlockBroken = blockBroken;
                mainResult.WasFullyBlocked = fullyBlocked;
            }
            else
            {
                // ⑧ 溢出回流给被瞄准者。护盾三项记在他这条上——盾本来就是他的（同 STS2）。
                int spill = mainResult.OverkillDamage > 0
                    ? Hook.ModifyHpLostAfterPet(state, originalTarget, mainResult.OverkillDamage, props, dealer, source, out _)
                    : 0;
                DamageResult spillResult = spill > 0
                    ? originalTarget.LoseHpInternal(spill, props)
                    : new DamageResult(originalTarget, props);
                spillResult.BlockedDamage = blocked;
                spillResult.WasBlockBroken = blockBroken;
                spillResult.WasFullyBlocked = fullyBlocked;
                hits.Add(spillResult);
            }

            // 记录 + 表现
            foreach (DamageResult r in hits)
            {
                state.Events.Emit(new DamageReceived
                {
                    Round = state.RoundNumber, Side = state.CurrentSide,
                    Target = r.Receiver, Dealer = dealer,
                    UnblockedDamage = r.UnblockedDamage, BlockedDamage = r.BlockedDamage,
                    OverkillDamage = r.OverkillDamage, WasFullyBlocked = r.WasFullyBlocked,
                    WasKilled = r.WasTargetKilled, Props = props, Source = source,
                });
                if (r.UnblockedDamage > 0 && !props.HasFlag(ValueProp.SkipHurtAnim))
                    Fx.Anim(r.Receiver, "hurt");
            }
            Fx.Sfx("hit");
            await Fx.Wait(0.15f);

            // 反应钩子（先让在场者跟做，再结死亡——荆棘在目标咽气前弹出）
            foreach (DamageResult r in hits)
            {
                await Hook.AfterDamageReceived(state, r.Receiver, r, props, dealer, source);
                await Hook.AfterDamageGiven(state, dealer, r, props, r.Receiver, source);
            }

            // 死亡结算
            foreach (DamageResult r in hits)
                if (r.WasTargetKilled)
                    await Die(state, r.Receiver, dealer, source);

            all.AddRange(hits);
        }
        return all;
    }

    public static async Task<int> GainBlock(CombatState state, Creature creature,
        decimal baseAmount, ValueProp props, CardModel? source)
    {
        int modified = Hook.ModifyBlock(state, creature, baseAmount, props, source, out _);
        if (modified <= 0) return 0;

        int gained = creature.GainBlockInternal(modified);
        state.Events.Emit(new BlockGained
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Target = creature, Amount = gained,
        });
        Fx.Sfx("block_gain");
        Fx.Vfx("block", creature);
        await Fx.Wait(0.1f);
        return gained;
    }

    public static async Task<int> Heal(CombatState state, Creature creature, int amount)
    {
        if (creature.IsDead) return 0;   // 复活是 Step C 的专门流程，不走 Heal
        int healed = creature.HealInternal(amount);
        if (healed > 0)
        {
            state.Events.Emit(new Healed
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Target = creature, Amount = healed,
            });
            Fx.Vfx("heal", creature);
            await Fx.Wait(0.1f);
        }
        return healed;
    }

    /// <summary>死亡流程。返回 false = 被 ShouldDie 一票否决。</summary>
    public static async Task<bool> Die(CombatState state, Creature creature, Creature? killer, CardModel? source)
    {
        if (!Hook.ShouldDie(state, creature, out GameModel? preventer))
        {
            if (preventer != null)
                await Hook.AfterPreventingDeath(preventer, creature);
            return false;
        }

        state.RemoveCreature(creature);
        // 注意：同伴尸体仍留在主人的 PlayerCombatState.Pets 里——Step C 复活流程的前提。
        // STS2 的 ShouldCreatureBeRemovedFromCombatAfterDeath（尸体留场显示）到 Step C 一起做。

        state.Events.Emit(new CreatureDied
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Creature = creature, Killer = killer,
        });
        Fx.Anim(creature, "die");
        Fx.Sfx("die");
        await Fx.Wait(0.2f);
        await Hook.AfterCreatureDied(state, creature, killer, source);
        return true;
    }
}