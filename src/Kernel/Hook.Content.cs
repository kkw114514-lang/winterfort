using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel;

public static partial class Hook
{
    public static async Task AfterHpLost(CombatState state, Creature target, int amount)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.AfterHpLost(target, amount);
    }

    public static int ModifyBuffApplyAmount(CombatState state, Creature target, BuffModel canonical, int amount, CardModel? source)
    {
        foreach (GameModel l in Snapshot(state))
            amount = l.ModifyBuffApplyAmount(target, canonical, amount, source);
        return amount;
    }

    /// <summary>【S2】全局费用折叠。Modify 族惯例：不做 StillValid（与 ModifyDamage 一致）。</summary>
    public static int ModifyEnergyCost(CombatState state, CardModel card, int cost)
    {
        foreach (GameModel l in Snapshot(state))
            cost = l.ModifyEnergyCost(card, cost);
        return cost;
    }

    /// <summary>【多重打出】折叠 + 收集修改者名单（STS2 Hook.cs:1016 同构）。
    /// 名单交回 CardCmd 逐个发消耗回调。出口封底 1——负修正最多把多重打回单发。</summary>
    public static int ModifyCardPlayCount(CombatState state, CardModel card, Creature? target,
        int playCount, out List<GameModel> modifiers)
    {
        modifiers = new List<GameModel>();
        foreach (GameModel l in Snapshot(state))
        {
            int next = l.ModifyCardPlayCount(card, target, playCount);
            if (next != playCount) modifiers.Add(l);
            playCount = next;
        }
        return Math.Max(1, playCount);
    }

    /// <summary>多重打出消耗回调(STS2 Hook.cs:482 同款):只发给改过次数的那些。</summary>
    public static async Task AfterModifyingCardPlayCount(CombatState state, CardModel card, List<GameModel> modifiers)
    {
        foreach (GameModel m in modifiers)
            await m.AfterModifyingCardPlayCount(card);
    }
}
