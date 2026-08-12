using System;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>玩家演出速度三档(STS2 FastModeType 原名照抄,消费者是 CustomScaledWait)。
/// Normal=标准档 Fast=快档 Instant=全部跳过。将来设置界面写它、存档持久化它(挂账)。</summary>
public enum FastModeType { Normal, Fast, Instant }

/// <summary>
/// 【偏离 STS2 #1】表现后端接口。STS2 的 SfxCmd/VfxCmd 直接调 Godot 节点
/// (NAudioManager.Instance),我们隔一层接口——Kernel 不引 Godot。
/// 四个方法对应表现的四个物理通道:声音/粒子/骨骼/时间。
/// 新内容带来的是新 id 字符串,不是第五个方法——接口定格,永不增长。
/// 语义级演出(伤害数字、飞卡)不在这——走事件流(下一批)。
/// </summary>
public interface IFxBackend
{
    void Sfx(string id);
    void Vfx(string id, Creature on);
    void Anim(Creature who, string animId);
    Task Wait(float seconds);
}

/// <summary>
/// 全 Kernel 唯一触碰表现的门面。
/// 无头:Backend 保持 null,三个 void 空转、Wait 立即完成——整场战斗瞬间跑完。
/// 【偏离 STS2 #2】后端异常被吞掉只记警告:表现层的 bug(丢音效文件、节点没挂)
/// 永远不许打断结算。
/// 【确定性铁律】这里出去的信息永远不回来:void 无返回,Wait 的 Task 不携带值。
/// </summary>
public static class Fx
{
    public static IFxBackend? Backend;

    /// <summary>演出速度档位(STS2:PrefsSave.FastMode)。内核只读;谁来写是产品层的事。</summary>
    public static FastModeType FastMode = FastModeType.Normal;

    /// <summary>被吞异常的出口。台架设成 Console.Error.WriteLine;Godot 设成 GD.PushWarning。</summary>
    public static Action<string>? OnWarn;

    public static void Sfx(string id)
    {
        try { Backend?.Sfx(id); }
        catch (Exception e) { Warn("Sfx", id, e); }
    }

    public static void Vfx(string id, Creature on)
    {
        try { Backend?.Vfx(id, on); }
        catch (Exception e) { Warn("Vfx", id, e); }
    }

    public static void Anim(Creature who, string animId)
    {
        try { Backend?.Anim(who, animId); }
        catch (Exception e) { Warn("Anim", animId, e); }
    }

    /// <summary>不许压缩的节拍用它。async 包住 await 的原因:后端返回的 Task 若中途
    /// fault,同步的 try/catch 接不住——必须在 await 处接。</summary>
    public static async Task Wait(float seconds)
    {
        IFxBackend? b = Backend;
        if (b == null) return;
        try { await b.Wait(seconds); }
        catch (Exception e) { Warn("Wait", seconds.ToString("0.##"), e); }
    }

    /// <summary>可压缩的节拍用它(STS2 Cmd.CustomScaledWait 同构):每处停顿自带
    /// (快档, 标准档)两份手调时长——加速不是全局乘系数,是每一处自己申报能压多狠。
    /// Instant 短路一秒不等。Backend 为 null(无头)时 Wait 本身立即完成——白得
    /// STS2 的 NonInteractiveMode 守卫。他们还有一道 IsEnding(终局不磨蹭)守卫,
    /// 需要看得到战局,挂账待演出驱动成形。</summary>
    public static async Task CustomScaledWait(float fastSeconds, float standardSeconds)
    {
        switch (FastMode)
        {
            case FastModeType.Instant: return;
            case FastModeType.Fast:    await Wait(fastSeconds);     return;
            default:                   await Wait(standardSeconds); return;
        }
    }

    private static void Warn(string channel, string id, Exception e)
        => OnWarn?.Invoke($"[Fx] {channel}({id}) 后端异常已吞:{e.Message}");
}
