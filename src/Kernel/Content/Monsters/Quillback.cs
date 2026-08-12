using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Monsters;

/// <summary>
/// 棘背兽｜野兽｜50–54 血。
/// 被动·竖刺:N = 1 + 本场已打出的【法术牌】数(裁定①),上限 5,永久累积不随回合重置。
/// 首回合必蜷伏(无动作,全场一次);此后每回合抖刺 3×N。
/// 【裁定②】N 在执行时实算;意图段数(IntentPreviewHits)每次重算——UI 拉取即实时。
/// 多人时合计所有契约师的出牌史("多人默认全体"精神的计数版)。
/// </summary>
public sealed class Quillback : MonsterModel
{
    public override int MinInitialHp => 50;
    public override int MaxInitialHp => 54;

    private bool _hunkered;
    private static readonly MonsterMove Hunker  = new() { Name = "蜷伏", Kind = IntentKind.Unknown };
    private static readonly MonsterMove Bristle = new() { Name = "抖刺", Kind = IntentKind.Attack, BaseDamage = 3 };
    // Bristle 的 Hits 留默认——段数不写死在招式里,预览与执行都实时算

    protected override MonsterMove RollMove(Rng ai, CombatState state)
    {
        if (!_hunkered) { _hunkered = true; return Hunker; }
        return Bristle;
    }

    private static int QuillCount(CombatState state)
    {
        int spells = state.Allies.Where(c => c.Player != null)
            .Sum(c => c.Player!.PlayerCombatState?.PlayHistory
                .Count(r => r.Card.Type == CardType.Spell) ?? 0);
        return System.Math.Min(1 + spells, 5);
    }

    public override int IntentPreviewHits(CombatState state)
        => NextMove == Bristle ? QuillCount(state) : base.IntentPreviewHits(state);

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Hunker) return;                                   // 无动作
        Creature? target = state.GetOpponentsOf(Creature).FirstOrDefault(c => c.IsAlive);
        if (target == null) return;
        int n = QuillCount(state);                                        // 裁定②:执行时实算
        for (int i = 0; i < n; i++)
            await CreatureCmd.Damage(state, Creature, new[] { target }, 3, ValueProp.Move, null);
    }
}
