using System.Threading.Tasks;

namespace Kernel;

public static partial class Hook
{
    public static async Task AfterCardDrawn(CombatState state, CardModel card)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardDrawn(card);
    }

    public static async Task AfterCardDiscarded(CombatState state, CardModel card)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardDiscarded(card);
    }

    public static async Task AfterCardExhausted(CombatState state, CardModel card)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardExhausted(card);
    }

    public static async Task AfterCardPlayed(CombatState state, CardModel card, Creature? target)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardPlayed(card, target);
    }

    public static async Task BeforeCardPlayed(CombatState state, CardModel card, Creature? target)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l))
                await l.BeforeCardPlayed(card, target);
    }

    public static async Task AfterCardGenerated(CombatState state, CardModel card)
    {
        foreach (GameModel l in Snapshot(state))
            if (StillValid(l)) await l.AfterCardGenerated(card);
    }
}