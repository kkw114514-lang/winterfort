using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>薪火长明｜1 费｜永续｜火｜Rare。升级:加「本能」。</summary>
public sealed class UndyingFlame : CardModel
{
    public UndyingFlame()
        : base(1, CardType.Aura, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "薪火长明";
    protected override string DescriptionTemplate => "永续：每张被消耗的燃料卡额外触发 1 次其燃料效果。";

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<UndyingFlameAura>(state, Owner!.Creature, 1, this);
}
