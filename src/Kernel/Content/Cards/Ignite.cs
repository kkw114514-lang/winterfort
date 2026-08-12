using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>点燃｜0 费｜攻击｜火｜Basic（角色专属开局卡：稀有度 Basic + 火系）｜单体。</summary>
public sealed class Ignite : CardModel
{
    public Ignite()
        : base(0, CardType.Attack, CardRarity.Basic, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "点燃";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。施加 {Sear} 层灼伤。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 3m), new DynamicVar("Sear", 1m) };

    // 升级：3/1 → 4/2
    protected override void OnUpgrade()
    {
        Vars.Damage.UpgradeBy(1m);
        Vars["Sear"].UpgradeBy(1m);
    }

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        // 目标被这一击打死时 Apply 自己短路（target.IsDead → null），不用外面再守
        await BuffCmd.Apply<SearBuff>(state, play.Target!, Vars["Sear"].Int, this);
    }
}