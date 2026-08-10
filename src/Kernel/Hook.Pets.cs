using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel;

public static partial class Hook
{
    public static int ModifySummonAmount(CombatState state, Player summoner, int baseAmount,
        GameModel? source, out List<GameModel> modifiers)
    {
        int amount = baseAmount;
        var mods = new List<GameModel>();
        foreach (GameModel l in Snapshot(state))
        {
            int next = l.ModifySummonAmount(summoner, amount, source);
            if (next != amount) mods.Add(l);
            amount = next;
        }
        modifiers = mods;
        return Math.Max(0, amount);
    }

    public static async Task AfterSummon(CombatState state, Player summoner, int amount)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterSummon(summoner, amount);
    }

    public static async Task AfterPetRevived(CombatState state, Creature pet)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterPetRevived(pet);
    }
}