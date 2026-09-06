using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>余烬倾泻｜2 费｜攻击｜火｜Uncommon｜全体。填满手牌=生成到 10 张上限。升级:28→34。</summary>
public sealed class Ashfall : CardModel
{
    public Ashfall()
        : base(2, CardType.Attack, CardRarity.Uncommon, CardElement.Fire, TargetType.AllEnemies) { }

    protected override string TitleText => "余烬倾泻";
    protected override string DescriptionTemplate => "对敌方全体造成 {Damage} 点伤害。用余烬牌填满手牌。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 28m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(6m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature,
            state.Enemies.Where(e => e.IsAlive).ToList(),
            Vars.Damage.Int, ValueProp.Move, this);
        var pcs = Owner!.PlayerCombatState!;
        while (pcs.Hand.Count < CardPile.MaxCardsInHand)
            await CardPileCmd.AddGenerated(state, state.CreateCard<Ember>(Owner!), PileType.Hand);
    }
}
