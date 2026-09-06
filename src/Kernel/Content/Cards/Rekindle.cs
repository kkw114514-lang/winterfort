using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>复燃核心｜2 费｜永续｜金。升级:加「本能」。</summary>
public sealed class Rekindle : CardModel
{
    public Rekindle()
        : base(2, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "复燃核心";
    protected override string DescriptionTemplate => "每当有牌被消耗时，抽 1 张牌。";

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<RekindleAura>(state, Owner!.Creature, 1, this);
}
