using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>持炎之环｜2 费｜永续｜金。升级:费用→1。</summary>
public sealed class EverflameRing : CardModel
{
    public EverflameRing()
        : base(2, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "持炎之环";
    protected override string DescriptionTemplate => "每当敌人被施加灼伤时，层数额外 +1。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<EverflameRingAura>(state, Owner!.Creature, 1, this);
}
