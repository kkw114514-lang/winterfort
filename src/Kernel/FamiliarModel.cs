namespace Kernel;

/// <summary>
/// 眷属（精灵）的定义基类。【不是生物】：没有 Creature、没有血条、不进战斗名册、
/// 全程不可被选中。它是挂在 Player 身上的被动监听者，只做两件事：
///   ① 给契约师提供一个特性——hook 覆写直接写在子类上
///   ② 绑定一张眷属专属卡（获得随卡入库、失去随卡离库——run 层实现）
/// 与同伴（Pet，C 步的战场生物）是两套系统，别混。
/// run 寿命：挂在 Player.Familiars 上跨战斗存活。
/// </summary>
public abstract class FamiliarModel : GameModel
{
    /// <summary>主人。AddFamiliarInternal 时反向填充。</summary>
    public Player? Owner { get; internal set; }

    public virtual string Describe() => Id.Entry;

    protected override void AfterCloned() => Owner = null;
}