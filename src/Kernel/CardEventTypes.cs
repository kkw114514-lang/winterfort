namespace Kernel;

public sealed class CardPlayStarted : CombatEvent
{
    public required CardModel Card { get; init; }
    public Creature? Target { get; init; }
    public bool IsAutoPlay { get; init; }
    public override string Description =>
        $"打出 {Card.Title}{(Target != null ? $" → {Target.Name}" : "")}{(IsAutoPlay ? "（自动）" : "")}";
}

public sealed class CardPlayFinished : CombatEvent
{
    public required CardModel Card { get; init; }
    public PileType Destination { get; init; }
    public override string Description => $"{Card.Title} 打出完毕 → {Destination}";
}

public sealed class CardDrawn : CombatEvent
{
    public required CardModel Card { get; init; }
    public override string Description => $"抽到 {Card.Title}";
}

/// <summary>只记【效果弃牌】。回合末 Flush 是另一个动词，不发这个（双动词纪律）。</summary>
public sealed class CardDiscarded : CombatEvent
{
    public required CardModel Card { get; init; }
    public override string Description => $"弃掉 {Card.Title}";
}

public sealed class CardExhausted : CombatEvent
{
    public required CardModel Card { get; init; }
    public override string Description => $"消耗 {Card.Title}";
}

public sealed class CardGenerated : CombatEvent
{
    public required CardModel Card { get; init; }
    public PileType To { get; init; }
    public override string Description => $"生成 {Card.Title} → {To}";
}

/// <summary>通用搬家事件——只由通用 Move 动词发出。注意 From 不可空：
/// 搬家必有来源；出生走 CardGenerated，这个字段在那边不存在（拆事件纪律）。</summary>
public sealed class CardMoved : CombatEvent
{
    public required CardModel Card { get; init; }
    public PileType From { get; init; }
    public PileType To { get; init; }
    public override string Description => $"{Card.Title}：{From} → {To}";
}

public sealed class PilesReshuffled : CombatEvent
{
    public required Player Player { get; init; }
    public int CardCount { get; init; }
    public override string Description => $"{Player.Name} 弃牌堆洗回抽牌堆（{CardCount} 张）";
}

public sealed class NihilityTriggered : CombatEvent
{
    public required Player Player { get; init; }
    public override string Description => $"{Player.Name} 手牌已空——虚无触发";
}