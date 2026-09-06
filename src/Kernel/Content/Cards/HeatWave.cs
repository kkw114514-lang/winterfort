using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>热浪｜1 费｜攻击｜火｜Common｜全体敌人。逐敌结算,有灼伤者加伤。升级:7→10。</summary>
public sealed class HeatWave : CardModel
{
    public HeatWave()
        : base(1, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "热浪";
    protected override string DescriptionTemplate => "对敌方全体造成 {Damage} 点伤害。对有灼伤的敌人，伤害+{Bonus}。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 7m), new DynamicVar("Bonus", 3m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive).ToList())
        {
            int dmg = Vars.Damage.Int + (enemy.HasBuff<SearBuff>() ? Vars["Bonus"].Int : 0);
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { enemy },
                dmg, ValueProp.Move, this);
        }
    }
}
