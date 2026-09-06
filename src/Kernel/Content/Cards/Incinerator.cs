using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>焚化炉｜2 费｜永续｜金。升级:费用→1。</summary>
public sealed class Incinerator : CardModel
{
    public Incinerator()
        : base(2, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "焚化炉";
    protected override string DescriptionTemplate => "每回合开始时，抽 2 张牌，获得 3 点力量。每回合结束时，消耗手牌中 1 张余烬；若没有余烬，改为消耗抽牌堆顶的 2 张牌。";

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<IncineratorAura>(state, Owner!.Creature, 1, this);
}
