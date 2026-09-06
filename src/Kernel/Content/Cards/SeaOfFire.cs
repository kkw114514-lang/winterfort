using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火海｜2 费｜法术｜火｜Uncommon｜全体。逐敌:加 {Sear} 层 → 提Ⅱ级。升级:6→9。</summary>
public sealed class SeaOfFire : CardModel
{
    public SeaOfFire()
        : base(2, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "火海";
    protected override string DescriptionTemplate => "对敌方全体施加 {Sear} 层灼伤。灼伤等级+Ⅱ。焚毁 2。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Sear", 6m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars["Sear"].UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive).ToList())
        {
            await BuffCmd.Apply<SearBuff>(state, enemy, Vars["Sear"].Int, this);
            await BuffCmd.RaiseSearLevel(state, enemy, 2, this);
        }
        await CardSelectCmd.Immolate(state, Owner!, 2, this);
    }
}
