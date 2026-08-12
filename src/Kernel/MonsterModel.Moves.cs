using System.Linq;
using System.Threading.Tasks;

namespace Kernel;

public abstract partial class MonsterModel
{
    /// <summary>已声明的下一招。玩家回合开始时掷定(STS2 PrepareForNextTurn 同点)。</summary>
    public MonsterMove? NextMove { get; private set; }

    /// <summary>内容实现:用 MonsterAi 流掷下一招。【跨程序集覆写写 protected override】</summary>
    protected abstract MonsterMove RollMove(Rng ai, CombatState state);

    public void RollNextMove(CombatState state)
    {
        AssertMutable();
        NextMove = RollMove(state.RngSet[RngStream.MonsterAi], state);
    }

    private int _cycleIndex;

    /// <summary>严格循环选招(确定性状态机,不掷 MonsterAi 流)。
    /// 第二个雇主(雪傀儡)到场,按"共同形状等第二个雇主再抽"的规矩落户基类;
    /// 哨兵自己的手写循环 M5 rework 时迁入。mutable 副本每场新克隆,_cycleIndex 天然归零。</summary>
    protected MonsterMove Cycle(params MonsterMove[] moves)
    {
        MonsterMove move = moves[_cycleIndex];
        _cycleIndex = (_cycleIndex + 1) % moves.Length;
        return move;
    }

    /// <summary>
    /// 默认执行器:攻击打首个存活对手(名册序玩家在前 → 打玩家本体,舍身自然转移);
    /// 防御加盾。复杂招式的怪覆写本方法。
    /// </summary>
    public virtual async Task TakeTurn(CombatState state)
    {
        if (Creature == null || Creature.IsDead || NextMove == null) return;
        MonsterMove move = NextMove;
        switch (move.Kind)
        {
            case IntentKind.Attack:
                Creature? target = state.GetOpponentsOf(Creature).FirstOrDefault(c => c.IsAlive);
                if (target == null) return;
                for (int i = 0; i < move.Hits; i++)
                    await CreatureCmd.Damage(state, Creature, new[] { target }, move.BaseDamage, ValueProp.Move, null);
                break;
            case IntentKind.Defend:
                await CreatureCmd.GainBlock(state, Creature, move.BlockAmount, ValueProp.Move, null);
                break;
        }
    }

    /// <summary>意图上显示的数字——与实际结算【同一条】修正链("意图写 6 实际打 6"的兑现处)。</summary>
    public int IntentPreviewDamage(CombatState state)
    {
        if (Creature == null || NextMove is not { Kind: IntentKind.Attack } move) return 0;
        Creature? target = state.GetOpponentsOf(Creature).FirstOrDefault(c => c.IsAlive);
        return Hook.ModifyDamage(state, target, Creature, move.BaseDamage, ValueProp.Move, null, out _);
    }

    /// <summary>意图上显示的段数——与执行同源。默认取声明招式的 Hits;
    /// 段数动态的怪(棘背兽)覆写成实算(裁定②"实时"的兑现处)。</summary>
    public virtual int IntentPreviewHits(CombatState state) => NextMove?.Hits ?? 0;
}
