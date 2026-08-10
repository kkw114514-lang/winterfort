using System;

namespace Kernel;

/// <summary>力量：你打出的攻击 +层数（可以为负）。</summary>
public sealed class StrengthBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override bool AllowNegative => true;

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner) return 0m;
        if (!props.IsPoweredAttack()) return 0m;
        return Amount;
    }
}

/// <summary>
/// 灼伤：火系的易伤器。两个数——Amount（层数）+ Level（等级Ⅰ-Ⅳ）。
///   规则② 放大所有攻击伤害：×(1 + 0.25×等级)。不限攻击者、不限元素。
///   规则③（敌方回合末 层数-=等级）等 Step E 回合结构，覆写 AfterTurnEnd 加在这。
///   规则④（归零整条消失）由 BuffCmd 的归零移除承担——等级不能脱离层数存在。
/// </summary>
public sealed class SearBuff : BuffModel
{
    public const int MaxLevel = 4;

    /// <summary>首次施加默认Ⅰ级（规则①）。只被 RaiseLevelInternal 改。</summary>
    public int Level { get; private set; } = 1;

    public override BuffPolarity Polarity => BuffPolarity.Negative;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        return 1m + 0.25m * Level;          // Ⅰ×1.25 Ⅱ×1.5 Ⅲ×1.75 Ⅳ×2.0
    }

    public void RaiseLevelInternal(int levels)
    {
        AssertMutable();
        if (levels < 0) throw new ArgumentException("必须非负", nameof(levels));
        Level = Math.Min(MaxLevel, Level + levels);
    }

    public override string Describe() => $"灼伤{RomanLevel}·{Amount}";

    private string RomanLevel => Level switch { 1 => "Ⅰ", 2 => "Ⅱ", 3 => "Ⅲ", 4 => "Ⅳ", _ => Level.ToString() };
}

/// <summary>弱化：你打出的攻击 ×0.75（同 STS2 虚弱）。</summary>
public sealed class WeakenBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Negative;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (dealer != Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        return 0.75m;
    }
}

/// <summary>
/// 舍身：挂在【同伴】身上——主人未被护盾挡下的攻击伤害由我承受。
/// 三个转移条件抄 STS2 DieForYouPower 原样：打的是我主人、我还活着、是正经攻击
/// （中毒等直伤不转移）。
/// STS2 还有三个配套钩子（尸体不可选中/死后留场/主人死后保留）——Step C 复活流程一起做。
/// </summary>
public sealed class GuardianBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override BuffStackType StackType => BuffStackType.Single;

    public override Creature ModifyUnblockedDamageTarget(Creature target, int amount, ValueProp props, Creature? dealer)
    {
        if (Owner == null || Owner.IsDead) return target;
        if (target != Owner.PetOwner?.Creature) return target;
        if (!props.IsPoweredAttack()) return target;
        return Owner;
    }
}