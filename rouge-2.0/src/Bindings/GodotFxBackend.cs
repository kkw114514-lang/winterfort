using System.Threading.Tasks;
using Godot;
using Kernel;

/// <summary>IFxBackend 的 Godot 实现(引擎胶水层)。Wait = 场景树计时器真等待;
/// 三个哑通道先打印 id——每跑一场,输出面板白得一份演出采购单,音效/特效批次照单采买。
/// 铁律回声:这里出去的信息永不回内核(打印/等待都不带值回去)。</summary>
public sealed class GodotFxBackend : IFxBackend
{
    private readonly Node _host;

    public GodotFxBackend(Node host) => _host = host;

    public void Sfx(string id)              => GD.Print($"[Fx] 音效 {id}");
    public void Vfx(string id, Creature on) => GD.Print($"[Fx] 特效 {id} @ {on.Name}");
    public void Anim(Creature who, string animId) => GD.Print($"[Fx] 动画 {animId} @ {who.Name}");

    public async Task Wait(float seconds)
        => await _host.ToSignal(_host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}