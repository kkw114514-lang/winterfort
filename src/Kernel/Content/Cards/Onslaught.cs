using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>无双乱舞｜20 费｜攻击｜火｜Rare。费用 = 20 − 全场出牌史条数(打出自己那条
/// 记在扣费之后,天然不含自身);段数 = 1 + 本回合已打出(裁定12,不含自身);
/// 语序照卡面:每段先伤害后力量 → 后段吃前段的力量(微裁定3);目标死即停。升级:费用→15。</summary>
public sealed class Onslaught : CardModel
{
    public Onslaught()
        : base(20, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "无双乱舞";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。获得 1 点力量。本回合每打出过一张牌，额外重复一次。本场战斗中你每打出过一张牌，这张卡费用 -1。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 4m) };

    protected override void OnUpgrade() => UpgradeCostBy(-5);

    public override int ModifyEnergyCost(CardModel card, int cost)
    {
        if (card != this) return cost;
        return cost - (Owner?.PlayerCombatState?.PlayHistory.Count ?? 0);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int reps = 1 + PlayedThisTurnBesidesThis(state);
        for (int i = 0; i < reps && play.Target!.IsAlive; i++)
        {
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
                Vars.Damage.Int, ValueProp.Move, this);
            await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, 1, this);
        }
    }
}
