using System.Linq;
using System.Threading.Tasks;
using Kernel.Content.Cards;

namespace Kernel.Content.Buffs;

// ═══ 永续本体族(偏离#6 翻案:被动住 buff,同名叠层效果按层数倍增)═══
// 通用守则:Owner 是王女的 Creature;每个钩子先验 Owner 活着、拿得到战场。

/// <summary>烈性本体:每次实际扣血 +Amount 力。</summary>
public sealed class HotBloodAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override async Task AfterHpLost(Creature target, int amount)
    {
        if (Owner is not { IsAlive: true } me || target != me) return;
        if (me.CombatState is not { } state) return;
        await BuffCmd.Apply<StrengthBuff>(state, me, Amount, null);
    }

    public override string Describe() => $"烈性 {Amount}";
}

/// <summary>烧刃本体:你的攻击漏伤 → 目标 +Amount 灼伤。SourceCard 记着卡壳——
/// 好让炽烈认得出"这是你施加的"。</summary>
public sealed class BurningEdgeAura : BuffModel
{
    public CardModel? SourceCard { get; set; }

    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override async Task AfterDamageGiven(Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? source)
    {
        if (Owner is not { IsAlive: true } me || dealer != me) return;
        if (!props.IsPoweredAttack() || result.UnblockedDamage <= 0) return;
        if (target.Side != CombatSide.Enemy || target.IsDead) return;
        if (me.CombatState is not { } state) return;
        await BuffCmd.Apply<SearBuff>(state, target, Amount, SourceCard);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        SourceCard = null;
    }

    public override string Describe() => $"烧刃 {Amount}";
}

/// <summary>持炎之环本体:敌人被施加灼伤 +Amount 层(不问来源)。</summary>
public sealed class EverflameRingAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override int ModifyBuffApplyAmount(Creature target, BuffModel canonical, int amount, CardModel? source)
    {
        if (Owner is not { IsAlive: true }) return amount;
        if (canonical is not SearBuff || target.Side != CombatSide.Enemy) return amount;
        return amount + Amount;
    }

    public override string Describe() => $"持炎之环 {Amount}";
}

/// <summary>炽烈本体:你施加的灼伤 +Amount 层(按来源卡归属;自灼也算你施加)。</summary>
public sealed class IntensifyAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override int ModifyBuffApplyAmount(Creature target, BuffModel canonical, int amount, CardModel? source)
    {
        if (Owner is not { IsAlive: true } me) return amount;
        if (canonical is not SearBuff || source?.Owner != me.Player) return amount;
        return amount + Amount;
    }

    public override string Describe() => $"炽烈 {Amount}";
}

/// <summary>灼伤精通本体:玩家回合结束,已有灼伤的敌人等级 +Amount(无灼伤者明文排除)。</summary>
public sealed class SearMasteryAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Owner is not { IsAlive: true } me || me.CombatState is not { } state) return;
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive && e.HasBuff<SearBuff>()).ToList())
            await BuffCmd.RaiseSearLevel(state, enemy, Amount, null);
    }

    public override string Describe() => $"灼伤精通 {Amount}";
}

/// <summary>复燃核心本体:你的牌被消耗 → 抽 Amount。</summary>
public sealed class RekindleAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override async Task AfterCardExhausted(CardModel card)
    {
        if (Owner is not { IsAlive: true } me || card.Owner != me.Player) return;
        if (me.CombatState is not { } state) return;
        await CardPileCmd.Draw(state, me.Player!, Amount);
    }

    public override string Describe() => $"复燃核心 {Amount}";
}

/// <summary>焚化炉本体:回合开始抽 2×Amount、+3×Amount 力;回合结束逐层:
/// 烧手牌 1 张余烬,无则烧抽牌堆顶 2 张。</summary>
public sealed class IncineratorAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;

    public override async Task AfterTurnStarted(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Owner is not { IsAlive: true } me || me.CombatState is not { } state) return;
        await CardPileCmd.Draw(state, me.Player!, 2 * Amount);
        await BuffCmd.Apply<StrengthBuff>(state, me, 3 * Amount, null);
    }

    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Owner is not { IsAlive: true } me || me.CombatState is not { } state) return;
        var pcs = me.Player!.PlayerCombatState!;
        for (int layer = 0; layer < Amount; layer++)
        {
            CardModel? ember = pcs.Hand.Cards.FirstOrDefault(c => c is Ember);
            if (ember != null)
            {
                await CardPileCmd.Exhaust(state, ember);
                continue;
            }
            for (int i = 0; i < 2 && !pcs.DrawPile.IsEmpty; i++)
                await CardPileCmd.Exhaust(state, pcs.DrawPile.Cards[0]);
        }
    }

    public override string Describe() => $"焚化炉 {Amount}";
}
