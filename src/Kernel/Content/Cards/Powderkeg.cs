using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>油库引爆｜1 费｜法术｜火｜Rare。焚毁任意张(min0 → 必弹选择器);
/// 燃料卡额外触发燃料 2 次;非燃料卡每张对自己 1 点管线伤(裁定3)。升级:费用→0。</summary>
public sealed class Powderkeg : CardModel
{
    public Powderkeg()
        : base(1, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "油库引爆";
    protected override string DescriptionTemplate => "焚毁任意张手牌。其中每张燃料卡额外触发其燃料效果 2 次。每张非燃料卡对自己造成 1 点伤害。";

    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => UpgradeCostBy(-1);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        var pcs = Owner!.PlayerCombatState!;
        List<CardModel> picked = await CardSelectCmd.FromHand(state, Owner!,
            new CardSelectorPrefs("焚毁任意张", 0, pcs.Hand.Count), null, this);
        foreach (CardModel card in picked)
        {
            await CardPileCmd.Exhaust(state, card);                  // 原生燃料在这触发一次
            if (card.HasTag(CardTag.Fuel))
            {
                await card.OnFuel(state);                            // 额外 ×2
                await card.OnFuel(state);
            }
            else
            {
                await CreatureCmd.Damage(state, Owner!.Creature, new[] { Owner!.Creature },
                    1m, ValueProp.Move, this);
            }
        }
    }
}
