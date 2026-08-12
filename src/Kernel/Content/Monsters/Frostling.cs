using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Monsters;

/// <summary>
/// 小霜怪 Frostling｜元素灵｜20–24 血,成对入场(阵容写两行)。
/// 被动·抱团:每回合开始(意图掷定时)比较两只当前血量——
///   血多的「扑打」(7 伤),血少的「挤靠」(给另一只 +2 力量);
///   相等时左边那只扑打(裁定③:左 = 先入场 = CombatId 小者)。
/// 【规则自闭合】死剩一只:它天然是"血量较高者",恒扑打——零特例代码(设计稿原话兑现)。
/// 【裁定】队友阵亡瞬间改口（原"落空一拍"作废）。
/// 你越集火一只,另一只越滚力量:打进去的伤害会变成对面的力量(强度守恒)。
/// 选招在声明时定死,本回合内血量再变不改分工(设计:"每回合开始比较");
/// 意图的数值预览照常实时(力量涨了,扑打的数字跟着涨)。
/// 三只以上同场是超范围场景:与首个活着的同伴比较,不承诺语义。
/// </summary>
public sealed class Frostling : MonsterModel
{
    public override int MinInitialHp => 20;
    public override int MaxInitialHp => 24;

    private static readonly MonsterMove Pounce = new() { Name = "扑打", Kind = IntentKind.Attack, BaseDamage = 7 };
    private static readonly MonsterMove Lean   = new() { Name = "挤靠", Kind = IntentKind.Buff };

    private Creature? OtherLiving(CombatState state) =>
        state.Enemies.FirstOrDefault(e => e != Creature && e.IsAlive && e.Monster is Frostling);

    protected override MonsterMove RollMove(Rng ai, CombatState state)
    {
        Creature? other = OtherLiving(state);
        if (other == null || Creature == null) return Pounce;          // 独活:规则自闭合
        bool higher = Creature.CurrentHp > other.CurrentHp
            || (Creature.CurrentHp == other.CurrentHp && Creature.CombatId < other.CombatId);
        return higher ? Pounce : Lean;
    }

    public override async Task TakeTurn(CombatState state)
    {
        if (Creature is not { IsAlive: true } || NextMove == null) return;
        if (NextMove == Lean)
        {
            Creature? other = OtherLiving(state);
            if (other == null) return;                                 // 队友已倒:挤靠落空
            await BuffCmd.Apply<StrengthBuff>(state, other, 2, null);
            return;
        }
        await base.TakeTurn(state);                                    // 扑打
    }

    /// <summary>【裁定:队友阵亡瞬间改口】另一只死亡时,若我声明的是挤靠,当场重声明
    /// (独活必得扑打)。血量易位【不】触发改口——"每回合开始比较"仍是活人间的规则。</summary>
    public override Task AfterCreatureDied(Creature creature, Creature? killer, CardModel? cardSource)
    {
        if (creature == Creature || creature.Monster is not Frostling) return Task.CompletedTask;
        if (Creature is not { IsAlive: true } me || me.CombatState is not { } state) return Task.CompletedTask;
        if (NextMove != Lean) return Task.CompletedTask;
        RollNextMove(state);
        state.Events.Emit(new MonsterIntentRolled
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Creature = me, MoveName = NextMove!.Name, Kind = NextMove.Kind,
            PreviewDamage = IntentPreviewDamage(state),
        });
        return Task.CompletedTask;
    }
}
