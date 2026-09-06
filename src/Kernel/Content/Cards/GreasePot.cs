using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>油脂弹｜1 费｜攻击｜火｜Common。燃料:挂 1 层油脂印记(本回合下一张焚毁卡×2,裁定8)。
/// 升级:加「保留」。</summary>
public sealed class GreasePot : CardModel
{
    public GreasePot()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "油脂弹";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。获得 {Shield} 点护盾。燃料：本回合的下一张焚毁卡额外打出一次。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 6m), new DynamicVar("Shield", 4m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Fuel };

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        await CreatureCmd.GainBlock(state, Owner!.Creature, Vars["Shield"].Int, ValueProp.Move, this);
    }

    protected internal override async Task OnFuel(CombatState state)
        => await BuffCmd.Apply<GreasePotBuff>(state, Owner!.Creature, 1, this);
}
