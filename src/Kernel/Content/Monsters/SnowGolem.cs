using System.Threading.Tasks;

namespace Kernel.Content.Monsters;

/// <summary>
/// 雪傀儡｜魔法构造体｜38–42 血。
/// 严格三招循环(Cycle helper,不掷 MonsterAi 流):
///   抡砸 12 伤 → 积雪 +6 盾 +1 力量 → 横扫 6 伤 +3 盾 → 抡砸 …
/// 「积雪」每绕一圈 +1 力量;两招攻击都走全局伤害公式——
/// 设计稿那句"别只加在抡砸上"就是 ModifyDamageAdditive 链的天然性质,想漏都漏不掉。
/// 意图数字(IntentPreviewDamage)与实算同链:积雪落地的瞬间,下一招的预览自动 +1。
/// </summary>
public sealed class SnowGolem : MonsterModel
{
    public override int MinInitialHp => 38;
    public override int MaxInitialHp => 42;

    private static readonly MonsterMove Slam  = new() { Name = "抡砸", Kind = IntentKind.Attack, BaseDamage = 12 };
    private static readonly MonsterMove Pack  = new() { Name = "积雪", Kind = IntentKind.Defend };
    private static readonly MonsterMove Sweep = new() { Name = "横扫", Kind = IntentKind.Attack, BaseDamage = 6 };

    protected override MonsterMove RollMove(Rng ai, CombatState state) => Cycle(Slam, Pack, Sweep);

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        switch (NextMove.Name)
        {
            case "抡砸":
                await base.TakeTurn(state);                                            // 默认执行器:全链伤害
                break;
            case "积雪":
                await CreatureCmd.GainBlock(state, Creature, 6, ValueProp.Move, null);
                await BuffCmd.Apply<StrengthBuff>(state, Creature, 1, null);
                break;
            case "横扫":
                await base.TakeTurn(state);                                            // 先打(6+力量)
                await CreatureCmd.GainBlock(state, Creature, 3, ValueProp.Move, null); // 再举盾
                break;
        }
    }
}
