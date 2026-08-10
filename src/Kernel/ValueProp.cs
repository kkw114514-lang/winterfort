using System;

namespace Kernel;

/// <summary>
/// 一次数值行为（伤害/护盾获得）的"性质标签"，作为一个 flags 参数
/// 跟着数值穿过整条结算管线（hook 签名里到处是它）。
///
/// 【加新位的铁律】命名方向必须保证"不设这一位 = 现在的默认行为"。
/// 否定式（Unblockable/Unpowered/Skip…）天然满足；肯定式要靠构造点默认值补
/// （攻击命令的默认 props 就是 Move）。方向选对，加位就是加一行，全库零改动；
/// 选反，就要回头审计每一个构造点。
/// </summary>
[Flags]
public enum ValueProp
{
    None         = 0,

    /// <summary>无视护盾，直击血量。</summary>
    Unblockable  = 1 << 0,

    /// <summary>不吃出手方的增益。解决两个经典 bug：
    /// 荆棘反伤被力量二次放大；两个荆棘互相弹到死循环。</summary>
    Unpowered    = 1 << 1,

    /// <summary>这是一次"卡牌或怪物招式"。中毒/灼烧这类持续伤害【不带】它——
    /// 这就是"中毒不算攻击"的全部实现。</summary>
    Move         = 1 << 2,

    /// <summary>纯表现：结算处不触发受击动画（中毒掉血不该让怪抽搐）。</summary>
    SkipHurtAnim = 1 << 3,
}

public static class ValuePropExtensions
{
    /// <summary>"这算不算一次正经攻击"——全代码库只用这一个判定回答，
    /// 别在内容代码里手拆 flags（同 STS2 的 IsPoweredAttack）。</summary>
    public static bool IsPoweredAttack(this ValueProp props)
        => props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);

    /// <summary>宽版：只问"是不是招式"，不管吃不吃增益（护盾获得的判定用这个）。</summary>
    public static bool IsCardOrMonsterMove(this ValueProp props)
        => props.HasFlag(ValueProp.Move);
}