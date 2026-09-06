using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>背水狂炎｜1 费｜法术｜火｜Rare｜消耗。语序照卡面:先裸失 {Loss} 生命、后挂背水
/// (挂反了会挡掉自己的入场费——热血同判例)。升级:失 20→15。</summary>
public sealed class LastStand : CardModel
{
    public LastStand()
        : base(1, CardType.Spell, CardRarity.Rare, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "背水狂炎";
    protected override string DescriptionTemplate => "失去 {Loss} 点生命。直到你的下回合开始，你的生命值不会降低。消耗。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Loss", 20m) };
    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => Vars["Loss"].UpgradeBy(-5m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await CreatureCmd.LoseHp(state, Owner!.Creature, Vars["Loss"].Int);
        await BuffCmd.Apply<LastStandBuff>(state, Owner!.Creature, 1, this);
    }
}
