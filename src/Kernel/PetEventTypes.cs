namespace Kernel;

public sealed class PetSummoned : CombatEvent
{
    public required Player Player { get; init; }
    public required Creature Pet { get; init; }
    public int Amount { get; init; }
    public bool WasRevive { get; init; }

    public override string Description => WasRevive
        ? $"{Player.Name} 的同伴复活（{Amount}）"
        : $"{Player.Name} 召唤同伴（{Amount}）";
}