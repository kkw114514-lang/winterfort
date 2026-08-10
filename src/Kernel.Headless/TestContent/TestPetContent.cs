using Kernel;

namespace TestContent;

/// <summary>测试同伴：区间 1..1（同 Osty——真实血量由召唤量决定），不出招。</summary>
public sealed class TestPetMonster : MonsterModel
{
    public override int MinInitialHp => 1;
    public override int MaxInitialHp => 1;

    protected override MonsterMove RollMove(Rng ai, CombatState state)
        => new MonsterMove { Name = "IDLE", Kind = IntentKind.Unknown };
}

/// <summary>测：ModifySummonAmount 消费者——主人召唤量 +Amount。</summary>
public sealed class TestSummonBoostBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override int ModifySummonAmount(Player summoner, int amount, GameModel? source)
        => Owner != null && summoner.Creature == Owner ? amount + Amount : amount;
}