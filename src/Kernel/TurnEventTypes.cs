namespace Kernel;

public sealed class TurnStarted : CombatEvent
{
    public override string Description => $"回合 {Round}·{Side} 开始";
}

public sealed class TurnEnded : CombatEvent
{
    public override string Description => $"回合 {Round}·{Side} 结束";
}

/// <summary>回合末清手（Flush 动词——不是弃牌，不发 CardDiscarded，不触发遗言）。</summary>
public sealed class CardsFlushed : CombatEvent
{
    public required Player Player { get; init; }
    public int Count { get; init; }
    public override string Description => $"{Player.Name} 回合末清手 {Count} 张";
}

public sealed class MonsterIntentRolled : CombatEvent
{
    public required Creature Creature { get; init; }
    public required string MoveName { get; init; }
    public IntentKind Kind { get; init; }
    public int PreviewDamage { get; init; }
    public override string Description =>
        $"{Creature.Name} 意图：{MoveName}{(Kind == IntentKind.Attack ? $"（{PreviewDamage}）" : "")}";
}

public sealed class BlockCleared : CombatEvent
{
    public required Creature Target { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Target.Name} 护盾清零（-{Amount}）";
}

public sealed class CombatEnded : CombatEvent
{
    public bool Victory { get; init; }
    public override string Description => Victory ? "战斗胜利" : "战斗失败";
}

public sealed class EnergyGained : CombatEvent
{
    public required Player Player { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Player.Name} 获得 {Amount} 点能量";
}