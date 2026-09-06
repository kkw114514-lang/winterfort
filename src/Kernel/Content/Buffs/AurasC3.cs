using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel.Content.Cards;

namespace Kernel.Content.Buffs;

/// <summary>燎原本体:你的余烬费用 −Amount;每打出一张余烬,附带(5 伤随机敌 + 抽 1)×Amount。
/// 微裁定2:附带伤害打随机敌(CombatTargets 流);附带伤走 Unpowered(群蛇形态判例:不吃力量、不吃乘算)。</summary>
public sealed class FirestormAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override string Describe() => $"燎原 {Amount}";

    public override int ModifyEnergyCost(CardModel card, int cost)
    {
        if (card is not Ember || card.Owner != Owner?.Player) return cost;
        return cost - Amount;
    }

    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (card is not Ember || card.Owner != Owner?.Player) return;
        if (Owner?.CombatState is not { } state) return;
        for (int i = 0; i < Amount; i++)
        {
            var alive = state.Enemies.Where(e => e.IsAlive).ToList();
            if (alive.Count > 0)
            {
                Creature t = alive[state.RngSet[RngStream.CombatTargets].NextInt(alive.Count)];
                await CreatureCmd.Damage(state, Owner, new[] { t }, 5m, ValueProp.Unpowered, null);   // 群蛇形态判例:附带伤=荆棘类
            }
            await CardPileCmd.Draw(state, Owner!.Player!, 1);
        }
    }
}

/// <summary>薪火长明本体:每张被消耗的【燃料卡】额外触发 Amount 次燃料效果。
/// 时序:Exhaust 动词先跑原生 OnFuel、后广播 AfterCardExhausted——我在原生之后加班。</summary>
public sealed class UndyingFlameAura : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override string Describe() => $"薪火长明 {Amount}";

    public override async Task AfterCardExhausted(CardModel card)
    {
        if (card.Owner != Owner?.Player || !card.HasTag(CardTag.Fuel)) return;
        if (Owner?.CombatState is not { } state) return;
        for (int i = 0; i < Amount; i++)
            await card.OnFuel(state);
    }
}

/// <summary>油脂印记(油脂弹的燃料产物):本回合下一张【焚毁卡】额外打出一次。
/// DuplicationPower 原式三件套:+1(不乘层)、用后自减一层、own 侧回合末自清。</summary>
public sealed class GreasePotBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override string Describe() => $"油脂 {Amount}";

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner != Owner?.Player || !card.HasTag(CardTag.Immolate)) return playCount;
        return playCount + 1;
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (Owner?.CombatState is not { } state) return;
        await BuffCmd.ChangeAmount(state, this, -1, null);   // 自减,归零由通用规则移除
    }

    public override async Task AfterTurnEnd(CombatSide side)
    {
        if (side != CombatSide.Player) return;               // 「本回合」:玩家回合末过期
        if (Owner?.CombatState is not { } state) return;
        await BuffCmd.Remove(state, this);
    }
}

/// <summary>背水本体(珊瑚判例):我主人的生命不会降低——钳位在 ModifyHpLost 末段,
/// 管线伤害与裸掉血(LoseHp 已补针)一并归零。「直到你的下回合开始」= 玩家回合开始自清。</summary>
public sealed class LastStandBuff : BuffModel
{
    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override string Describe() => "背水:生命不会降低";

    public override int ModifyHpLostAfterPet(Creature target, int amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        => target == Owner ? 0 : amount;

    public override async Task AfterTurnStarted(CombatSide side)
    {
        if (side != CombatSide.Player) return;
        if (Owner?.CombatState is not { } state) return;
        await BuffCmd.Remove(state, this);
    }
}

/// <summary>火神领域本体:你打出的每张牌,附带(焚毁 1 → 抽 1 → 力量 1 → 随机敌 1 层灼伤)×层数。
/// 【裁定13=群蛇形态判例】Before 记名、After 摘名:出牌起点在场才触发——
/// 打出领域那张卡本身不触发(登记那刻本体还没上身);打出第二张领域,旧层照常触发,
/// 且结算强度按【起点快照】,不吃结算途中的合层。</summary>
public sealed class FiregodDomainAura : BuffModel
{
    private Dictionary<CardModel, int> _witnessed = new Dictionary<CardModel, int>();

    public override BuffPolarity Polarity => BuffPolarity.Positive;
    public override string Describe() => $"火神领域 {Amount}";

    public override Task BeforeCardPlayed(CardModel card, Creature? target)
    {
        if (card.Owner == Owner?.Player)
            _witnessed[card] = Amount;                    // 起点记名 + 层数快照(索引器:重放逐次覆写不炸键)
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (card.Owner != Owner?.Player) return;
        if (!_witnessed.Remove(card, out int amount) || amount <= 0) return;   // 摘不到名 = 起点不在场
        if (Owner?.CombatState is not { } state) return;
        Player player = Owner.Player!;
        for (int i = 0; i < amount; i++)
        {
            await CardSelectCmd.Immolate(state, player, 1, this);
            await CardPileCmd.Draw(state, player, 1);
            await BuffCmd.Apply<StrengthBuff>(state, Owner, 1, null);
            var alive = state.Enemies.Where(e => e.IsAlive).ToList();
            if (alive.Count > 0)
            {
                Creature t = alive[state.RngSet[RngStream.CombatTargets].NextInt(alive.Count)];
                await BuffCmd.Apply<SearBuff>(state, t, 1, null);
            }
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _witnessed = new Dictionary<CardModel, int>();     // 克隆纪律:登记簿是实例状态,副本从零开始
    }
}
