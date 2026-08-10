namespace Kernel;

/// <summary>
/// 一次打出的上下文（同 STS2 CardPlay）。OnPlay 的第二参数。
/// 扩展纪律：以后要给 OnPlay 传新信息（交互选择器、X 费实付值……），
/// 一律在这里加字段——对象加字段不破签名，几百张卡的覆写不用动。
/// </summary>
public sealed class CardPlay
{
    public required CardModel Card { get; init; }

    /// <summary>SingleEnemy 卡的目标；None/Self 卡为 null。</summary>
    public Creature? Target { get; init; }

    /// <summary>true = 被效果打出（遗言等），不是玩家手点的。免费、不做资源检查。</summary>
    public bool IsAutoPlay { get; init; }

    /// <summary>多重打出：第几次（0 起）/ 共几次。"最后一击才触发 X"类效果读它。</summary>
    public int PlayIndex { get; init; }
    public int PlayCount { get; init; } = 1;

    public bool IsFirstInSeries => PlayIndex == 0;
    public bool IsLastInSeries => PlayIndex == PlayCount - 1;
}