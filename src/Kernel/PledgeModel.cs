namespace Kernel;

/// <summary>
/// 信物（遗物）的定义基类，对应 STS2 RelicModel。
/// 结构上是眷属的孪生：挂在 Player 身上的被动监听者，hook 覆写写在子类上；
/// run 寿命、跨战斗；摘除即停听（名单寿命=监听寿命）。
/// 与眷属的语义差别：无绑定卡、无特性叙事；将来有自己的稀有度档
///（STS2 RelicRarity：灯笼 Common、南瓜烛 Ancient——档位表等掉落系统落地时定）。
/// </summary>
public abstract class PledgeModel : GameModel
{
    /// <summary>主人。AddPledgeInternal 时反向填充。</summary>
    public Player? Owner { get; internal set; }

    public virtual string Describe() => Id.Entry;

    protected override void AfterCloned() => Owner = null;
}