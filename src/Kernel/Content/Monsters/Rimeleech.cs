using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Monsters;

/// <summary>
/// 霜蛭｜野兽｜40–44 血。严格循环:啃噬 9 → 蚀甲 4+1消融 → 硬皮 +5盾。
/// 被动·吸附(玩家回合结束时逐契约师结算):
///   剩有护盾 → 我 +2 力量;恰好 0 盾 → 我自伤 5(裸,裁定④)。
/// 玩法本体在被动:盾要用得一点不剩,多挡一点就是喂它。
/// 消融上身后你的卡牌盾 ×0.75 向下取整——算好的数字会被弄歪(设计稿原话)。
/// </summary>
public sealed class Rimeleech : MonsterModel
{
    public override int MinInitialHp => 40;
    public override int MaxInitialHp => 44;

    private static readonly MonsterMove Gnaw    = new() { Name = "啃噬", Kind = IntentKind.Attack, BaseDamage = 9 };
    private static readonly MonsterMove Corrode = new() { Name = "蚀甲", Kind = IntentKind.Attack, BaseDamage = 4 };
    private static readonly MonsterMove Harden  = new() { Name = "硬皮", Kind = IntentKind.Defend, BlockAmount = 5 };

    protected override MonsterMove RollMove(Rng ai, CombatState state) => Cycle(Gnaw, Corrode, Harden);

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Corrode)
        {
            Creature? target = state.GetOpponentsOf(Creature).FirstOrDefault(c => c.IsAlive);
            if (target == null) return;
            await CreatureCmd.Damage(state, Creature, new[] { target }, 4, ValueProp.Move, null);
            await BuffCmd.Apply<AblationBuff>(state, target, 1, null);   // 目标已死时 Apply 自己短路(点燃同款)
            return;
        }
        await base.TakeTurn(state);                                       // 啃噬/硬皮走默认执行器
    }

    /// <summary>吸附:挂在 MonsterModel 上——只要活着在名册,就听得到回合钩子。</summary>
    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Creature is not { IsAlive: true } me || me.CombatState is not { } state) return;
        foreach (Creature pact in state.Allies.Where(c => c.Player != null && c.IsAlive).ToList())
        {
            if (pact.Block > 0)
                await BuffCmd.Apply<StrengthBuff>(state, me, 2, null);
            else
                await CreatureCmd.LoseHp(state, me, 5);
            if (me.IsDead) return;                                        // 自伤致死后停止结算
        }
    }
}
