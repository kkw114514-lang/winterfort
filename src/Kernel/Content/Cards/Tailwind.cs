using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>乘风｜0 费｜法术｜火｜Common｜自身。【v2】不超过 3 张才加抽(奖励前排)。
/// 升级:加「本能」。</summary>
public sealed class Tailwind : CardModel
{
    public Tailwind()
        : base(0, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "乘风";
    protected override string DescriptionTemplate => "抽 1 张牌。若本回合已经打出的牌不超过 3 张，再抽 1 张牌。";

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CardPileCmd.Draw(state, Owner!, 1);
        if (PlayedThisTurnBesidesThis(state) <= 3)
            await CardPileCmd.Draw(state, Owner!, 1);
    }
}
