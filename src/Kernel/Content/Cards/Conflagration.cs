using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>狂炎引爆｜3 费｜攻击｜火｜Rare｜消耗。大量伤害;力量 5 倍生效(卡面明言):
/// 公式基础 + 4×力量入基数,管线再加 1× → 合计 5×。升级:费用→2。</summary>
public sealed class Conflagration : CardModel
{
    public Conflagration()
        : base(3, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "狂炎引爆";
    protected override string DescriptionTemplate => "根据灼伤程度对目标造成大量伤害。这张卡的力量加成以 5 倍生效。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int str = Owner!.Creature.GetBuffAmount<StrengthBuff>();
        int dmg = SearBlast.Of(play.Target!) + 4 * str;
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            dmg, ValueProp.Move, this);
    }
}
