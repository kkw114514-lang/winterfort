using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>永燃斗魂｜1 费｜法术｜金｜消耗。读消耗堆(自己入堆之前点数)。升级:去掉消耗。</summary>
public sealed class EverburningSpirit : CardModel
{
    public EverburningSpirit()
        : base(1, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "永燃斗魂";
    protected override string DescriptionTemplate => "你的消耗牌堆中每有 2 张牌，获得 1 点力量。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        int str = Owner!.PlayerCombatState!.ExhaustPile.Count / 2;
        if (str > 0)
            await BuffCmd.Apply<StrengthBuff>(state, Owner.Creature, str, this);
    }
}
