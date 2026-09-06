using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火墙｜1 费｜法术｜火｜Uncommon｜单体。加层→提级→举盾三连。升级:盾 5→8。</summary>
public sealed class Firewall : CardModel
{
    public Firewall()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "火墙";
    protected override string DescriptionTemplate => "施加 {Sear} 层灼伤。灼伤等级+Ⅰ。获得 {Shield} 点护盾。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Sear", 1m), new DynamicVar("Shield", 5m) };

    protected override void OnUpgrade() => Vars["Shield"].UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<SearBuff>(state, play.Target!, Vars["Sear"].Int, this);
        await BuffCmd.RaiseSearLevel(state, play.Target!, 1, this);
        await CreatureCmd.GainBlock(state, Owner!.Creature, Vars["Shield"].Int, ValueProp.Move, this);
    }
}
