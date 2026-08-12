using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel;

public static partial class Hook
{
    public static int ModifyHandDraw(CombatState state, Player player, int baseCount, out List<GameModel> modifiers)
    {
        int count = baseCount;
        var mods = new List<GameModel>();
        foreach (GameModel l in Snapshot(state))
        {
            int next = l.ModifyHandDraw(player, count);
            if (next != count) mods.Add(l);
            count = next;
        }
        modifiers = mods;
        return Math.Max(0, count);
    }

    public static async Task AfterTurnStarted(CombatState state, CombatSide side)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterTurnStarted(side);
    }
    
    public static async Task AfterTurnEnd(CombatState state, CombatSide side)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterTurnEnd(side);
    }

    public static bool ShouldFlush(CombatState state, Player player, out GameModel? preventer)
    {
        foreach (GameModel l in Snapshot(state))
        {
            if (!l.ShouldFlush(player))
            {
                preventer = l;
                return false;
            }
        }
        preventer = null;
        return true;
    }

    public static async Task AfterCardRetained(CombatState state, CardModel card)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardRetained(card);
    }
}