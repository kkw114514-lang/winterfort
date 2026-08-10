using System;
using System.Collections.Generic;

namespace Kernel;

/// <summary>
/// 一条战斗事实（对应 STS2 的 CombatHistoryEntry）。
/// 铁律：事件在状态【改完之后】发出——它是记录，不是请求；
/// 订阅者只准读，不准借事件里的引用改状态。
/// </summary>
public abstract class CombatEvent
{
    public int Round { get; init; }
    public CombatSide Side { get; init; }

    /// <summary>人话描述。确定性日志逐行 diff 的就是它，所以【禁止】在里面
    /// 使用引用哈希、内存地址等每次运行都不同的东西。</summary>
    public abstract string Description { get; }

    public string ToLogLine() => $"Rd {Round} ({Side}): {Description}";

    /// <summary>"这回合发生过没有"——内容判定的标准问法（同 STS2）。</summary>
    public bool HappenedThisTurn(CombatState state)
        => Round == state.RoundNumber && Side == state.CurrentSide;
}

/// <summary>
/// 战斗事实的流水账：一个列表 + 一个回调。一物三用——
///   内容代码查 Entries（"这回合抽了几张牌"）；
///   无头测试把 OnEvent 接到日志（同种子两跑逐行 diff = 里程碑验收）；
///   Godot 把 OnEvent 接到演出（伤害数字、飞卡）。
/// 具体事件类型随它们的发出者（Cmd 层，Step B）逐个加入，不预铸空壳。
/// </summary>
public sealed class CombatEvents
{
    private readonly List<CombatEvent> _entries = new();

    public IReadOnlyList<CombatEvent> Entries => _entries;

    public event Action<CombatEvent>? OnEvent;

    public void Emit(CombatEvent e)
    {
        _entries.Add(e);
        OnEvent?.Invoke(e);
    }
}