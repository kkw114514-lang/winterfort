using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>孤注｜2 费｜攻击｜火｜Rare｜消耗。焚毁全手牌(不弹选择器),逐跳 9 伤(微裁定7,菲德火同源)。
/// 升级:9→12。</summary>
public sealed class AllIn : CardModel
{
    public AllIn()
        : base(2, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "孤注";
    protected override string DescriptionTemplate => "焚毁所有手牌。每焚毁 1 张牌，造成 {Damage} 点伤害。消耗。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 9m) };
    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        List<CardModel> hand = Owner!.PlayerCombatState!.Hand.Cards.ToList();   // 快照:边烧边掉
        foreach (CardModel card in hand)
            await CardPileCmd.Exhaust(state, card);
        for (int i = 0; i < hand.Count && play.Target!.IsAlive; i++)
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
                Vars.Damage.Int, ValueProp.Move, this);
    }
}
