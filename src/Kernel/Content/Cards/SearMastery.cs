using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>灼伤精通｜1 费｜永续｜金。升级:费用→0。</summary>
public sealed class SearMastery : CardModel
{
    public SearMastery()
        : base(1, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "灼伤精通";
    protected override string DescriptionTemplate => "每回合结束时，具有灼伤的敌人灼伤等级+Ⅰ。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<SearMasteryAura>(state, Owner!.Creature, 1, this);
}
