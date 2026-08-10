using Kernel;
using System.Threading.Tasks;

namespace TestContent;

/// <summary>测试毒：普通减益，层数不可负。</summary>
public sealed class TestVenomBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Negative;
}

/// <summary>测试力：增益，层数可以为负（同 STS2 力量）。</summary>
public sealed class TestMightBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override bool AllowNegative => true;
}

/// <summary>测试怪：血量区间 40..44。</summary>
public sealed class TestDummyMonster : MonsterModel
{
    public override int MinInitialHp => 40;
    public override int MaxInitialHp => 44;
}

/// <summary>测试不死：一票否决自己主人的死亡，被否决时回 1 血并计数。</summary>
public sealed class TestUndyingBuff : BuffModel
{
    public int TimesPrevented { get; private set; }

    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override bool ShouldDie(Creature creature) => creature != Owner;

    public override Task AfterPreventingDeath(Creature creature)
    {
        TimesPrevented++;
        creature.HealInternal(1);
        return Task.CompletedTask;
    }
}