using System;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>
/// 【偏离 STS2 #1】表现后端接口。STS2 的 SfxCmd/VfxCmd 直接调 Godot 节点
/// （NAudioManager.Instance），我们隔一层接口——Kernel 不引 Godot。
/// 四个方法对应表现的四个物理通道：声音/粒子/骨骼/时间。
/// 新内容带来的是新 id 字符串，不是第五个方法——接口定格，永不增长。
/// 语义级演出（伤害数字、飞卡）不在这——走事件流（下一批）。
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
/// 无头：Backend 保持 null，三个 void 空转、Wait 立即完成——整场战斗瞬间跑完。
/// 【偏离 STS2 #2】后端异常被吞掉只记警告：表现层的 bug（丢音效文件、节点没挂）
/// 永远不许打断结算。
/// 【确定性铁律】这里出去的信息永远不回来：void 无返回，Wait 的 Task 不携带值。
/// </summary>
public static class Fx
{
    public static IFxBackend? Backend;

    /// <summary>被吞异常的出口。台架设成 Console.Error.WriteLine；Godot 设成 GD.PushWarning。</summary>
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

    /// <summary>async 包住 await 的原因：后端返回的 Task 若中途 fault，
    /// 同步的 try/catch 接不住——必须在 await 处接。</summary>
    public static async Task Wait(float seconds)
    {
        IFxBackend? b = Backend;
        if (b == null) return;
        try { await b.Wait(seconds); }
        catch (Exception e) { Warn("Wait", seconds.ToString("0.##"), e); }
    }

    private static void Warn(string channel, string id, Exception e)
        => OnWarn?.Invoke($"[Fx] {channel}({id}) 后端异常已吞：{e.Message}");
}