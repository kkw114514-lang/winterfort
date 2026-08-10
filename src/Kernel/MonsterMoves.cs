namespace Kernel;

/// <summary>意图类别（UI 图标按它选；Unknown = 问号）。</summary>
public enum IntentKind
{
    Attack,
    Defend,
    Buff,
    Debuff,
    Unknown,
}

/// <summary>一个怪物招式的声明。Name 是机器名——进日志、供断言，不是显示文本。</summary>
public sealed class MonsterMove
{
    public required string Name { get; init; }
    public IntentKind Kind { get; init; }
    public int BaseDamage { get; init; }
    public int Hits { get; init; } = 1;
    public int BlockAmount { get; init; }
}