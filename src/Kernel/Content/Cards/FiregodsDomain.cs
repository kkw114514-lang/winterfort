using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>火神领域｜3 费｜永续｜火｜Rare。升级:加「本能」。</summary>
public sealed class FiregodsDomain : CardModel
{
    public FiregodsDomain()
        : base(3, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "火神领域";
    protected override string DescriptionTemplate => "永续：你打出的每张牌附带：焚毁 1、抽 1 张牌、获得 1 点力量、对随机 1 个敌人施加 1 层灼伤。";

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<FiregodDomainAura>(state, Owner!.Creature, 1, this);
}
