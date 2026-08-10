using System.Threading.Tasks;
using Kernel;

namespace TestContent;

/// <summary>测试蛮兵：MonsterAi 流二选一——攻 8 或 防 5。</summary>
public sealed class TestBruteMonster : MonsterModel
{
    public override int MinInitialHp => 26;
    public override int MaxInitialHp => 30;

    protected override MonsterMove RollMove(Rng ai, CombatState state)
        => ai.NextInt(2) == 0
            ? new MonsterMove { Name = "SMASH", Kind = IntentKind.Attack, BaseDamage = 8 }
            : new MonsterMove { Name = "BRACE", Kind = IntentKind.Defend, BlockAmount = 5 };
}

/// <summary>测：临时关键词（回合末被消耗，触发燃料途径③）。</summary>
public sealed class TestTemporaryCard : CardModel
{
    public TestTemporaryCard()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Dark, TargetType.None) { }

    protected override string TitleText => "泡影";
    protected override string DescriptionTemplate => "临时。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Temporary };
}

/// <summary>测：本能（首回合必上手）+ 保留（回合末不被清）。</summary>
public sealed class TestInnateRetainCard : CardModel
{
    public TestInnateRetainCard()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Light, TargetType.None) { }

    protected override string TitleText => "常备";
    protected override string DescriptionTemplate => "本能。保留。";
    protected override System.Collections.Generic.IEnumerable<CardKeyword> CanonicalKeywords
        => new[] { CardKeyword.Innate, CardKeyword.Retain };
}

/// <summary>测：ModifyHandDraw（长蛇戒指位）。挂主人身上，+Amount 张。</summary>
public sealed class TestHandDrawBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override int ModifyHandDraw(Player player, int count)
        => Owner != null && player.Creature == Owner ? count + Amount : count;
}