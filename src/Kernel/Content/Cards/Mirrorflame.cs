using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>镜炎｜0 费｜攻击｜火｜Rare。Misery 原序:先照镜子(快照负面)→打→泼给其他敌人。
/// 裁定6:层数叠加(ApplyLike 合层)、灼伤等级取高(不够才补提)。升级:6→9。</summary>
public sealed class Mirrorflame : CardModel
{
    public Mirrorflame()
        : base(0, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "镜炎";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。将目标的全部负面状态复制给其他所有敌人。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 6m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        Creature target = play.Target!;
        var captured = target.Buffs
            .Where(b => !b.Removed && b.Polarity == BuffPolarity.Negative)
            .Select(b => (proto: (BuffModel)b, amount: b.Amount, searLevel: (b as SearBuff)?.Level ?? 0))
            .ToList();
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { target },
            Vars.Damage.Int, ValueProp.Move, this);
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive && e != target).ToList())
        {
            foreach (var (proto, amount, searLevel) in captured)
            {
                await BuffCmd.ApplyLike(state, enemy, proto, amount, this);
                if (searLevel > 0 && enemy.GetBuff<SearBuff>() is { } sear && sear.Level < searLevel)
                    await BuffCmd.RaiseSearLevel(state, enemy, searLevel - sear.Level, this);
            }
        }
    }
}
