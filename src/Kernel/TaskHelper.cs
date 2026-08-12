using System;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>
/// 动作级围栏（对应 STS2 TaskHelper.RunSafely：Log.Error + Sentry + 重抛进无人 await 的任务）。
/// 【差异记录】我们的驱动循环直接 await 每一步，所以接住后不重抛：记警告、循环继续。
/// 围栏内死，围栏外活；被吞异常必须可观测（OnWarn 出口，将来接崩溃遥测也从这走）。
/// 【只给产品驱动用】headless 里程碑不包围栏——开发期的断言就该大声死。
/// 队列落地时（可玩期），本围栏原地升格为 GameAction.Execute 的包装，与 STS2 全面对齐。
/// </summary>
public static class TaskHelper
{
    /// <summary>台架接 Console.Error，Godot 接 GD.PushWarning。</summary>
    public static Action<string>? OnWarn;

    public static async Task RunStepSafely(Func<Task> step)
    {
        try { await step(); }
        catch (Exception e)
        {
            OnWarn?.Invoke($"[Step] 一步结算中异常已接住：{e.GetType().Name}: {e.Message}");
        }
    }
}