using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>提气｜0 费｜法术｜蓝｜消耗。升级:抽 1→2。</summary>
public sealed class Brace : CardModel
{
    public Brace()
        : base(0, CardType.Spell, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "提气";
    protected override string DescriptionTemplate => "获得 {Str} 点力量。抽 {Cards} 张牌。消耗。";

    protected override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Str", 1m), new DynamicVar("Cards", 1m) };

    protected override void OnUpgrade() => Vars["Cards"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        await CardPileCmd.Draw(state, Owner!, Vars["Cards"].Int);
    }
}
