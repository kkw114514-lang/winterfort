using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>灰飞烟灭｜2 费｜攻击｜火｜Rare｜无指向(随机靶)。消耗全部手牌干扰牌(Type==Dross),
/// 每张对随机敌 8 伤(逐跳,微裁定7)。升级:8→11。</summary>
public sealed class UpInSmoke : CardModel
{
    public UpInSmoke()
        : base(2, CardType.Attack, CardRarity.Rare, CardElement.Fire, TargetType.None) { }

    protected override string TitleText => "灰飞烟灭";
    protected override string DescriptionTemplate => "消耗手牌中所有干扰牌。每消耗 1 张，对随机 1 个敌人造成 {Damage} 点伤害。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Damage", 8m) };

    protected override void OnUpgrade() => Vars.Damage.UpgradeBy(3m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        List<CardModel> dross = Owner!.PlayerCombatState!.Hand.Cards
            .Where(c => c.Type == CardType.Dross).ToList();
        foreach (CardModel card in dross)
            await CardPileCmd.Exhaust(state, card);
        for (int i = 0; i < dross.Count; i++)
        {
            var alive = state.Enemies.Where(e => e.IsAlive).ToList();
            if (alive.Count == 0) break;
            Creature t = alive[state.RngSet[RngStream.CombatTargets].NextInt(alive.Count)];
            await CreatureCmd.Damage(state, Owner!.Creature, new[] { t },
                Vars.Damage.Int, ValueProp.Move, this);
        }
    }
}
