using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>油焰引爆｜0 费｜攻击｜火｜Uncommon。段数=1+目标灼伤【层数】(开打时定死,烈阳神罚同款);
/// 每段独立过管线取整。燃料:抽 2 + 全体 3 层。升级:2→3。</summary>
public sealed class OilFlash : CardModel
{
    public OilFlash()
        : base(0, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "油焰引爆";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害，目标每有一层灼伤，额外重复一次。燃料：抽 2 张牌。对敌方全体施加 3 层灼伤。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 2m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Fuel };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        Creature target = play.Target!;
        int hits = 1 + (target.GetBuff<SearBuff>()?.Amount ?? 0);
        for (int i = 0; i < hits && target.IsAlive; i++)
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { target },
                Vars.Damage.Int, ValueProp.Move, this);
    }

    protected internal override async Task OnFuel(CombatState state)
    {
        await CardPileCmd.Draw(state, Owner!, 2);
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive).ToList())
            await BuffCmd.Apply<SearBuff>(state, enemy, 3, this);
    }
}
