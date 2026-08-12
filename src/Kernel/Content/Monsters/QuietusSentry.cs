using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Monsters;

/// <summary>
/// 求终者哨兵｜44–48 血。【M5 rework】
/// 首回合必「终期」且全场一次(技能「终期」与状态「终期」同名异物——设计稿自标的红线,
/// 代码里一个是 MonsterMove 一个是 BuffModel,天然分居);
/// 之后严格循环:止刃 7 → 急刺 3×3 → 据守 7 盾(Cycle helper,M1 迁移承诺兑现)。
/// 喂层已搬进 QuietusBuff(势力机制化);终期招执行 = 施加 1 层出生(裁定⑦)。
/// 在它第一次行动之前打死:终期未上身,零爆炸。
/// </summary>
public sealed class QuietusSentry : MonsterModel
{
    public override int MinInitialHp => 44;
    public override int MaxInitialHp => 48;

    private bool _opened;
    private static readonly MonsterMove Quietus    = new() { Name = "终期", Kind = IntentKind.Buff };
    private static readonly MonsterMove Stillblade = new() { Name = "止刃", Kind = IntentKind.Attack, BaseDamage = 7 };
    private static readonly MonsterMove Flurry     = new() { Name = "急刺", Kind = IntentKind.Attack, BaseDamage = 3, Hits = 3 };
    private static readonly MonsterMove Hold       = new() { Name = "据守", Kind = IntentKind.Defend, BlockAmount = 7 };

    protected override MonsterMove RollMove(Rng ai, CombatState state)
    {
        if (!_opened) { _opened = true; return Quietus; }
        return Cycle(Stillblade, Flurry, Hold);
    }

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Quietus)
        {
            await BuffCmd.Apply<QuietusBuff>(state, Creature, 1, null);   // 1 层出生
            return;
        }
        await base.TakeTurn(state);
    }
}
