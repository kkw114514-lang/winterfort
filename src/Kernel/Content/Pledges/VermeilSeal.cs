using System.Threading.Tasks;

namespace Kernel.Content.Pledges;

/// <summary>
/// 赤金王印 Vermeil Seal｜Infanta 初始信物。
/// 每场战斗的第一回合，获得 1 点能量。
/// 结构 = STS2 灯笼原样（Lantern：AfterSideTurnStart + 本侧 + RoundNumber<=1 → GainEnergy）。
/// </summary>
public sealed class VermeilSeal : PledgeModel
{
    public override async Task AfterTurnStarted(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Owner?.Creature.CombatState is not { } state) return;
        if (state.RoundNumber > 1) return;
        await CombatCmd.GainEnergy(state, Owner, 1);
    }
}