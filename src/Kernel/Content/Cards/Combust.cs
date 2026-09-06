using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>焚身｜1 费｜攻击｜蓝。自灼是代价(灼伤放大你受的伤)。升级:自灼 3→1。</summary>
public sealed class Combust : CardModel
{
    public Combust()
        : base(1, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "焚身";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。对自己施加 {SelfSear} 层灼伤。自身的灼伤等级+Ⅰ。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 30m), new DynamicVar("SelfSear", 3m) };

    protected override void OnUpgrade() => Vars["SelfSear"].UpgradeBy(-2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        await BuffCmd.Apply<SearBuff>(state, Owner!.Creature, Vars["SelfSear"].Int, this);
        await BuffCmd.RaiseSearLevel(state, Owner!.Creature, 1, this);
    }
}
