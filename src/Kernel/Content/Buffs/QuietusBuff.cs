using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Buffs;

/// <summary>
/// 终期:求终者阵营的共有增益(哨兵先用;游骑兵等后续单位挂上即自动继承节奏)。
///   · 【M5 rework】喂层住我这,不再由宿主 TakeTurn 代喂(势力机制化):
///     宿主自己的回合【开始】时 +1。裁定⑦:1 层出生+回合开始加层——
///     绕开 BuffCmd 的 0 门,层数轨迹与原案在回合边界完全等价。
///   · 死亡时对全体契约师造成【层数 ×2】伤害(rework:倍率 1→2),
///     走完整攻击管线(ValueProp.Move):吃护盾、吃灼伤放大;将来有同伴时会被代受。
///   · 爆得响的前提是 Die 的"遗言时点"——分发在 RemoveCreature 之前。
///   · 联机裁定:怪打所有人,爆炸也炸全体契约师(单人局行为不变;入场序=稳定序)。
/// </summary>
public sealed class QuietusBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    /// <summary>宿主回合开始 +1(只认自己的侧别;死人不涨层)。</summary>
    public override async Task AfterTurnStarted(CombatSide side)
    {
        if (Owner is not { IsAlive: true } me || me.Side != side) return;
        if (me.CombatState is not { } state) return;
        await BuffCmd.ChangeAmount(state, this, 1, null);
    }

    public override async Task AfterCreatureDied(Creature creature, Creature? killer, CardModel? cardSource)
    {
        if (creature != Owner) return;
        if (Amount <= 0) return;
        if (Owner!.CombatState is not { } state) return;

        Creature[] pactbearers = state.Allies.Where(c => c.IsPlayer && c.IsAlive).ToArray();
        if (pactbearers.Length == 0) return;
        await CreatureCmd.Damage(state, Owner, pactbearers, Amount * 2, ValueProp.Move, null);
    }

    public override string Describe() => $"终期 {Amount}";
}
