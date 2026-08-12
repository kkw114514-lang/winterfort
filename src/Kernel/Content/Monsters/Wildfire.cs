namespace Kernel.Content.Monsters;

/// <summary>
/// 野火灵 Wildfire｜元素灵｜12–16 血。站位:左(先入场,先行动)。
/// 每回合恒「燎」(3 伤)——它自己没有成长,全部凶性来自拾柴人喂的力量。
/// 力量是挂在它身上的 buff:拾柴人死后不消失(设计稿:"先杀拾柴人止不了血,
/// 只止得住变旺")——零代码,buff 归属语义天然兑现。
/// </summary>
public sealed class Wildfire : MonsterModel
{
    public override int MinInitialHp => 12;
    public override int MaxInitialHp => 16;

    private static readonly MonsterMove Scorch = new() { Name = "燎", Kind = IntentKind.Attack, BaseDamage = 3 };

    protected override MonsterMove RollMove(Rng ai, CombatState state) => Scorch;
    // TakeTurn 不覆写:默认执行器(打首个存活对手)完全够用
}
