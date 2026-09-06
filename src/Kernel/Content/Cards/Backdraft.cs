using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>引火烧身｜1 费｜法术｜蓝。【v2】自灼 2 层;自伤走管线(裁定3,吃自己的盾)。升级:敌灼 3→5。</summary>
public sealed class Backdraft : CardModel
{
    public Backdraft()
        : base(1, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "引火烧身";
    protected override string DescriptionTemplate => "施加 {Sear} 层灼伤。灼伤等级+Ⅰ。对自己造成 2 点伤害。对自己施加 2 层灼伤。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Sear", 3m) };

    protected override void OnUpgrade() => Vars["Sear"].UpgradeBy(2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<SearBuff>(state, play.Target!, Vars["Sear"].Int, this);
        await BuffCmd.RaiseSearLevel(state, play.Target!, 1, this);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { Owner!.Creature }, 2m, ValueProp.Move, this);
        await BuffCmd.Apply<SearBuff>(state, Owner!.Creature, 2, this);
    }
}
