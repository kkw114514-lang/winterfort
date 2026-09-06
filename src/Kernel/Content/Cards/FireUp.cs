using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel.Content.Cards;

/// <summary>焚牌蓄力｜1 费｜法术｜火｜Common。焚的是攻击牌 → 额外 +{Bonus} 力量。升级:额外 1→2。</summary>
public sealed class FireUp : CardModel
{
    public FireUp()
        : base(1, CardType.Spell, CardRarity.Common, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "焚牌蓄力";
    protected override string DescriptionTemplate => "获得 {Str} 点力量。焚毁 1。若焚毁的是攻击牌，额外获得 {Bonus} 点力量。";

    protected override IEnumerable<DynamicVar> CanonicalVars
        => new[] { new DynamicVar("Str", 2m), new DynamicVar("Bonus", 1m) };
    protected override IEnumerable<CardTag> CanonicalTags => new[] { CardTag.Immolate };

    protected override void OnUpgrade() => Vars["Bonus"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Str"].Int, this);
        var burned = await CardSelectCmd.Immolate(state, Owner!, 1, this);
        if (burned.Any(c => c.Type == CardType.Attack))
            await BuffCmd.Apply<StrengthBuff>(state, Owner!.Creature, Vars["Bonus"].Int, this);
    }
}
