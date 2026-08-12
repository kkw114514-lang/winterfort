using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

// 真内容在 Kernel 程序集内，覆写 OnPlay 要写全 protected internal override
//（跨程序集的测试卡只写 protected override——CS0507 规则相反）。

/// <summary>攻击｜1 费｜攻击｜通用基石｜Basic｜单体。</summary>
public sealed class Attack : CardModel
{
    public Attack()
        : base(1, CardType.Attack, CardRarity.Basic, CardElement.Basic, TargetType.SingleEnemy) { }

    protected override string TitleText => "攻击";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 6m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
}