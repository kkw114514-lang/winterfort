using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>群体烧伤｜1 费｜法术｜火｜Common｜全体敌人。逐敌按名册序施加。升级:1→3 层。</summary>
public sealed class MassBurn : CardModel
{
    public MassBurn()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "群体烧伤";
    protected override string DescriptionTemplate => "对敌方全体施加 {Sear} 层灼伤。抽 {Cards} 张牌。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Sear", 1m), new DynamicVar("Cards", 2m) };

    protected override void OnUpgrade() => Vars["Sear"].UpgradeBy(2m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        foreach (Creature enemy in state.Enemies.Where(e => e.IsAlive).ToList())
            await BuffCmd.Apply<SearBuff>(state, enemy, Vars["Sear"].Int, this);
        await CardPileCmd.Draw(state, Owner!, Vars["Cards"].Int);
    }
}
