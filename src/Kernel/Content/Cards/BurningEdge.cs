using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>烧刃｜2 费｜永续｜蓝。升级:费用→1。</summary>
public sealed class BurningEdge : CardModel
{
    public BurningEdge()
        : base(2, CardType.Aura, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "烧刃";
    protected override string DescriptionTemplate => "每当你的攻击造成未被格挡的伤害时，对其目标施加 1 层灼伤。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        var aura = await BuffCmd.Apply<BurningEdgeAura>(state, Owner!.Creature, 1, this);
        if (aura != null) aura.SourceCard = this;            // 让炽烈认得出"你施加的"
    }
}
