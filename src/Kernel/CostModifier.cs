using System;

namespace Kernel;

[Flags]
public enum CostModifierExpiration
{
    None       = 0,
    EndOfTurn  = 1 << 0,   // 「本回合」
    WhenPlayed = 1 << 1,   // 「直到打出」
    EndOfCombat = 1 << 2,  // 「本场战斗」——战斗副本随战斗销毁，永远不用清扫
}

/// <summary>
/// 费用局部修正条（STS2 CardEnergyCost.LocalCostModifier 同款）：
/// 绝对/相对 × 过期条件 × 只减不加。挂在单张卡上，按加入顺序折叠。
/// </summary>
public sealed class CostModifier
{
    public int Amount { get; }
    public bool Absolute { get; }
    public CostModifierExpiration Expiration { get; }
    public bool ReduceOnly { get; }

    public CostModifier(int amount, bool absolute, CostModifierExpiration expiration, bool reduceOnly = false)
    {
        Amount = amount;
        Absolute = absolute;
        Expiration = expiration;
        ReduceOnly = reduceOnly;
    }

    public int Modify(int cost)
    {
        int next = Absolute ? Amount : cost + Amount;
        return ReduceOnly && next > cost ? cost : next;
    }

    public CostModifier Clone() => (CostModifier)MemberwiseClone();
}
