using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Monsters;

/// <summary>
/// 拾柴人 Kindler｜流浪者｜30–34 血。站位:右(后入场,后行动)。
/// 野火灵活着:每回合「添柴」(给它 +4 力量,自己不攻击);火灭:每回合「挥柴」(13 伤)。
/// 【站位涌现】"当回合添的柴下一回合才烧":野火灵先动、拾柴人后动——
/// 加的力量本轮已用不上,下轮自动生效。设计与行动序一字不差咬合,零实现成本。
/// 【裁定】火灭瞬间改口重声明(原案'落空一拍'作废)
/// </summary>
public sealed class Kindler : MonsterModel
{
    public override int MinInitialHp => 30;
    public override int MaxInitialHp => 34;

    private static readonly MonsterMove Stoke    = new() { Name = "添柴", Kind = IntentKind.Buff };
    private static readonly MonsterMove Brandish = new() { Name = "挥柴", Kind = IntentKind.Attack, BaseDamage = 13 };

    private Creature? LivingWildfire(CombatState state) =>
        state.Enemies.FirstOrDefault(e => e != Creature && e.IsAlive && e.Monster is Wildfire);

    protected override MonsterMove RollMove(Rng ai, CombatState state)
        => LivingWildfire(state) != null ? Stoke : Brandish;

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Stoke)
        {
            Creature? fire = LivingWildfire(state);
            if (fire == null) return;                                  // 火在声明后灭了:落空
            await BuffCmd.Apply<StrengthBuff>(state, fire, 4, null);
            return;
        }
        await base.TakeTurn(state);                                    // 挥柴
    }

    /// <summary>【裁定:火灭立刻换岗】野火灵死亡的瞬间重新声明,不等下回合。
    /// 正常声明由回合机器发意图事件;这里是计划外改口,手动补发——日志与 UI 都要留痕。</summary>
    public override Task AfterCreatureDied(Creature creature, Creature? killer, CardModel? cardSource)
    {
        if (creature.Monster is not Wildfire) return Task.CompletedTask;
        if (Creature is not { IsAlive: true } me || me.CombatState is not { } state) return Task.CompletedTask;
        if (NextMove != Stoke) return Task.CompletedTask;          // 只有添柴计划作废才需要改口
        RollNextMove(state);                                       // 火已灭 → 必得挥柴
        state.Events.Emit(new MonsterIntentRolled
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Creature = me, MoveName = NextMove!.Name, Kind = NextMove.Kind,
            PreviewDamage = IntentPreviewDamage(state),
        });
        return Task.CompletedTask;
    }
}
