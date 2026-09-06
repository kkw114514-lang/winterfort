using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>火山灰｜0 费｜攻击｜火｜Common。余烬 2(裁定2:生成入弃牌堆)。升级:9→12。</summary>
public sealed class Tephra : CardModel
{
    public Tephra()
        : base(0, CardType.Attack, CardRarity.Common, CardElement.Fire, TargetType.SingleEnemy) { }

    protected override string TitleText => "火山灰";
    protected override string DescriptionTemplate => "造成 {Damage} 点伤害。余烬 2。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 9m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { play.Target! },
            Vars.Damage.Int, ValueProp.Move, this);
        for (int i = 0; i < 2; i++)
            await CardPileCmd.AddGenerated(state, state.CreateCard<Ember>(Owner!), PileType.Discard);
    }
}
