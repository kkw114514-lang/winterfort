using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>炽烈｜1 费｜永续｜蓝。升级:费用→0。</summary>
public sealed class Intensify : CardModel
{
    public Intensify()
        : base(1, CardType.Aura, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "炽烈";
    protected override string DescriptionTemplate => "你施加的灼伤层数 +1。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<IntensifyAura>(state, Owner!.Creature, 1, this);
}
