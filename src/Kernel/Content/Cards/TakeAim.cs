using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>瞄准｜1 费｜法术｜火｜Common｜单体。升级:2→4 层。</summary>
public sealed class TakeAim : CardModel
{
    public TakeAim()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "瞄准";
    protected override string DescriptionTemplate => "施加 {Sear} 层灼伤。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Sear", 2m) };

    protected override void OnUpgrade() => Vars["Sear"].UpgradeBy(2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
        => await BuffCmd.Apply<SearBuff>(state, play.Target!, Vars["Sear"].Int, this);
}
