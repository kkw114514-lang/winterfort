using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火焰净化｜2 费｜攻击｜火｜Rare｜消耗。引爆:先算巨额、再消除灼伤、后结算——
/// 灼伤已被吃掉,管线不再乘(微裁定1的天然例外)。升级:去「消耗」。</summary>
public sealed class CleansingFire : CardModel
{
    public CleansingFire()
        : base(2, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "火焰净化";
    protected override string DescriptionTemplate => "引爆并消除目标的灼伤，根据灼伤程度造成巨额伤害。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        Creature target = play.Target!;
        int dmg = 2 * SearBlast.Of(target);
        if (target.GetBuff<SearBuff>() is { } sear)
            await BuffCmd.Remove(state, sear);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { target },
            dmg, ValueProp.Move, this);
    }
}
