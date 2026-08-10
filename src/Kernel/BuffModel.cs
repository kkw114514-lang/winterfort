using System;

namespace Kernel;

/// <summary>
/// 战斗内 buff/debuff（力量/易伤/虚弱/中毒…）的基类。STS2 对应物：PowerModel。
///
/// 本批只有骨架。hook 虚方法（ModifyDamageAdditive 之类）等 Hook 总线建好后加在这。
///
/// 三条结构事实：
///  1. 它是 GameModel：canonical = 定义，mutable 副本 = 场上的一个实例。
///  2. 给同伴加成的 buff 挂在【主人】身上、靠 dealer 反查（同 STS2 CalcifyPower）——
///     同伴会死会重召，挂它身上会跟着蒸发。
///  3. 叠层合并/归零移除的政策在 BuffCmd（Step B）。这里只有裸状态。
/// </summary>
public abstract class BuffModel : GameModel
{
    public Creature? Owner { get; private set; }
    public int Amount { get; private set; }
    public bool Removed { get; private set; }

    public abstract BuffPolarity Polarity { get; }

    public virtual BuffStackType StackType => BuffStackType.Counter;

    /// <summary>层数可否为负。力量可以被打成负数（AllowNegative=true），中毒不行。同 STS2。</summary>
    public virtual bool AllowNegative => false;

    /// <summary>归零时是否自动移除。"仪式"这类恒定 buff 覆写成 false。BuffCmd 消费它。</summary>
    public virtual bool RemoveAtZero => true;

    public virtual string Describe() => $"{Id.Entry} {Amount}";

    // ══ Internal 层：只改状态，不跑 hook，不发事件。只准 Cmd 层调用 ══

    public void ApplyInternal(Creature owner, int amount)
    {
        AssertMutable();
        if (Owner != null)
            throw new InvalidOperationException($"{Id} 已经挂在 {Owner} 身上了，不能二次挂载。");
        Owner = owner;
        Amount = amount;
        owner.AddBuffInternal(this);
        OnApplied();
    }

    public void ChangeAmountInternal(int delta)
    {
        AssertMutable();
        if (Owner == null)
            throw new InvalidOperationException($"{Id} 还没挂到任何生物身上。");
        Amount += delta;
    }

    public void RemoveInternal()
    {
        AssertMutable();
        Removed = true;
        Owner?.RemoveBuffInternal(this);
    }

    /// <summary>挂载完成时的钩子（"获得仪式时立刻+1力量"这类写在这）。</summary>
    protected virtual void OnApplied() { }

    protected override void AfterCloned()
    {
        Owner = null;
        Removed = false;
    }
}