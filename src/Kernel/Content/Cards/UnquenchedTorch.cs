using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>不熄之炬｜2 费｜攻击｜火｜Rare。燃料:回手 + 伤害翻倍,本场可反复叠加。
/// 微裁定8:翻倍只翻基础值(力量在管线另加)。翻倍借 UpgradeBy(Base) 落账,
/// 随手 FinalizeUpgrade 清掉高亮标记——不污染升级预览。升级:加「保留」。</summary>
public sealed class UnquenchedTorch : CardModel
{
    public UnquenchedTorch()
        : base(2, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "不熄之炬";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。燃料：将这张卡移回手牌，其伤害翻倍（本场战斗可叠加）。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 10m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Fuel };

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);

    protected internal override async Task OnFuel(CombatState state)
    {
        await CardPileCmd.Move(state, this, Owner!.PlayerCombatState!.Hand);
        Vars.Damage.UpgradeBy(Vars.Damage.Base);   // ×2
        FinalizeUpgrade();
    }
}
