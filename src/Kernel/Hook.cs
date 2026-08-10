using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>
/// 广播器：每个时机一个静态方法——取名单快照、逐个调虚方法、把答案折叠成结果。
/// 折叠方式因族而异：Modify=数值折叠，Should=一票否决，After=顺序 await。
/// 名单与顺序来自 CombatState.IterateHookListeners（[5] 已断言钉死）。
/// </summary>
public static class Hook
{
    /// <summary>快照原因：After 钩子里可能增删 buff，不能边遍历边改活列表。</summary>
    private static List<GameModel> Snapshot(CombatState state) => state.IterateHookListeners().ToList();

    /// <summary>快照之后才被移除的 buff 不再响应（防"死 buff 还在触发"）。</summary>
    private static bool StillValid(GameModel l) => l is not BuffModel { Removed: true };

    // ══ 伤害链：加段 → 乘段 → Max(0) → 取整。【全局唯一取整点】══

    public static int ModifyDamage(CombatState state, Creature? target, Creature? dealer,
        decimal baseAmount, ValueProp props, CardModel? cardSource, out List<GameModel> modifiers)
    {
        decimal num = baseAmount;
        var mods = new List<GameModel>();
        List<GameModel> listeners = Snapshot(state);

        foreach (GameModel l in listeners)
        {
            decimal d = l.ModifyDamageAdditive(target, num, props, dealer, cardSource);
            num += d;
            if (d != 0m) mods.Add(l);
        }
        foreach (GameModel l in listeners)
        {
            decimal f = l.ModifyDamageMultiplicative(target, num, props, dealer, cardSource);
            num *= f;
            if (f != 1m) mods.Add(l);
        }
        modifiers = mods;
        return (int)Math.Max(0m, num);
    }

    public static int ModifyBlock(CombatState state, Creature target,
        decimal baseAmount, ValueProp props, CardModel? cardSource, out List<GameModel> modifiers)
    {
        decimal num = baseAmount;
        var mods = new List<GameModel>();
        List<GameModel> listeners = Snapshot(state);

        foreach (GameModel l in listeners)
        {
            decimal d = l.ModifyBlockAdditive(target, num, props, cardSource);
            num += d;
            if (d != 0m) mods.Add(l);
        }
        foreach (GameModel l in listeners)
        {
            decimal f = l.ModifyBlockMultiplicative(target, num, props, cardSource);
            num *= f;
            if (f != 1m) mods.Add(l);
        }
        modifiers = mods;
        return (int)Math.Max(0m, num);
    }

    // ══ 护盾之后的两段（int 域）══

    public static int ModifyHpLostBeforePet(CombatState state, Creature target, int amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, out List<GameModel> modifiers)
    {
        var mods = new List<GameModel>();
        foreach (GameModel l in Snapshot(state))
        {
            int next = l.ModifyHpLostBeforePet(target, amount, props, dealer, cardSource);
            if (next != amount) mods.Add(l);
            amount = next;
        }
        modifiers = mods;
        return Math.Max(0, amount);
    }

    public static int ModifyHpLostAfterPet(CombatState state, Creature target, int amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, out List<GameModel> modifiers)
    {
        var mods = new List<GameModel>();
        foreach (GameModel l in Snapshot(state))
        {
            int next = l.ModifyHpLostAfterPet(target, amount, props, dealer, cardSource);
            if (next != amount) mods.Add(l);
            amount = next;
        }
        modifiers = mods;
        return Math.Max(0, amount);
    }

    /// <summary>伤害转移：只换人，不碰数值。</summary>
    public static Creature ModifyUnblockedDamageTarget(CombatState state, Creature target,
        int amount, ValueProp props, Creature? dealer)
    {
        Creature current = target;
        foreach (GameModel l in Snapshot(state))
            current = l.ModifyUnblockedDamageTarget(current, amount, props, dealer);
        return current;
    }

    // ══ Should 族：一票否决 ══

    public static bool ShouldDie(CombatState state, Creature creature, out GameModel? preventer)
    {
        foreach (GameModel l in Snapshot(state))
        {
            if (!l.ShouldDie(creature))
            {
                preventer = l;
                return false;
            }
        }
        preventer = null;
        return true;
    }

    // ══ Before / After 族：顺序 await ══

    public static async Task BeforeDamageReceived(CombatState state, Creature target, int amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.BeforeDamageReceived(target, amount, props, dealer, cardSource);
    }

    public static async Task AfterDamageReceived(CombatState state, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.AfterDamageReceived(target, result, props, dealer, cardSource);
    }

    public static async Task AfterDamageGiven(CombatState state, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.AfterDamageGiven(dealer, result, props, target, cardSource);
    }

    public static async Task AfterCreatureDied(CombatState state, Creature creature,
        Creature? killer, CardModel? cardSource)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.AfterCreatureDied(creature, killer, cardSource);
    }

    /// <summary>注意：只发给否决者一个人，不广播（STS2 语义）。</summary>
    public static Task AfterPreventingDeath(GameModel preventer, Creature creature)
        => preventer.AfterPreventingDeath(creature);
}