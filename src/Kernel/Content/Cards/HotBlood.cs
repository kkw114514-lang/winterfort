using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel.Content.Buffs;

namespace Kernel.Content.Cards;

/// <summary>烈性｜1 费｜永续｜蓝。语序照卡面:先上被动再付代价——入场 3 点自伤触发自己。
/// 升级:每跳 +1→+2(卡壳施加 2 层)。</summary>
public sealed class HotBlood : CardModel
{
    public HotBlood()
        : base(1, CardType.Aura, CardRarity.Uncommon, CardElement.Fire, TargetType.Self) { }

    protected override string TitleText => "烈性";
    protected override string DescriptionTemplate => "每当你失去生命时，获得 {Per} 点力量。对自己造成 3 点伤害。";

    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { new DynamicVar("Per", 1m) };

    protected override void OnUpgrade() => Vars["Per"].UpgradeBy(1m);

    protected internal override async Task OnPlay(CombatState state, CardPlay play)
    {
        await BuffCmd.Apply<HotBloodAura>(state, Owner!.Creature, Vars["Per"].Int, this);
        await CreatureCmd.Damage(state, Owner!.Creature, new[] { Owner!.Creature }, 3m, ValueProp.Move, this);
    }
}
