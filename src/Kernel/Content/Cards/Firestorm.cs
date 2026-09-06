using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>燎原｜2 费｜永续｜火｜Rare。升级:费用→1。</summary>
public sealed class Firestorm : CardModel
{
    public Firestorm()
        : base(2, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "燎原";
    protected override string DescriptionTemplate => "永续：你的余烬费用 -1，并附带：造成 5 点伤害、抽 1 张牌。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<FirestormAura>(state, Owner!.Creature, 1, this);
}
