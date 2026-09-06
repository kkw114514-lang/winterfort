using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>爆燃｜2 费｜攻击｜火｜Uncommon｜全体。升级:19→24。</summary>
public sealed class Flashover : CardModel
{
    public Flashover()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "爆燃";
    protected override string DescriptionTemplate => "对敌方全体造成 {Damage} 点伤害。将 2 张余烬加入弃牌堆。焚毁 2。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 19m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(5m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature,
            state.Enemies.Where(e => e.IsAlive).ToList(),
            Vars.Damage.Int, ValueProp.Move, this);
        for (int i = 0; i < 2; i++)
            await CardPileCmd.AddGenerated(state, state.CreateCard<Ember>(Owner!), PileType.Discard);
        await CardSelectCmd.Immolate(state, Owner!, 2, this);
    }
}
