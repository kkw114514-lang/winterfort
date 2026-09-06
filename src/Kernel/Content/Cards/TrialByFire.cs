using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>烈焰审判｜3 费｜法术｜金｜消耗。灼伤Ⅳ → 消灭(裁定9:真死亡,终期照爆)。升级:费用→2。</summary>
public sealed class TrialByFire : CardModel
{
    public TrialByFire()
        : base(3, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "烈焰审判";
    protected override string DescriptionTemplate => "若目标的灼伤等级为Ⅳ，消灭之。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        if (play.Target!.GetBuff<SearBuff>()?.Level == SearBuff.MaxLevel)
            await CreatureCmd.Annihilate(state, play.Target!, Owner!.Creature, this);
    }
}
