using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>烈阳神罚｜1 费｜攻击｜金。段数 = 1 + 目标灼伤等级(执行时实算);
/// 击杀 → 全体 2 层灼伤 + 等级+Ⅰ。升级:8→11。</summary>
public sealed class Sunwrath : CardModel
{
    public Sunwrath()
        : base(1, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "烈阳神罚";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害，目标每有一级灼伤，额外重复一次。若击杀目标，对敌方全体施加 2 层灼伤，并使灼伤等级+Ⅰ。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Damage", 8m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        Creature target = play.Target!;
        int hits = 1 + (target.GetBuff<SearBuff>()?.Level ?? 0);
        for (int i = 0; i < hits && target.IsAlive; i++)
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { target },
                Vars.Damage.Int, ValueProp.Move, this);
        if (target.IsDead)
        {
            foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive).ToList())
            {
                await BuffCmd.Apply<SearBuff>(state, enemy, 2, this);
                await BuffCmd.RaiseSearLevel(state, enemy, 1, this);
            }
        }
    }
}
