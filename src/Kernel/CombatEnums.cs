namespace Kernel;

/// <summary>战斗中的阵营。同伴（宠物）站在 Player 侧。</summary>
public enum CombatSide
{
    Player,
    Enemy,
}

/// <summary>
/// buff 的极性。STS2 对应物是 PowerType { Buff, Debuff }——我们把实体改名叫 Buff 后
/// 照搬会变成 BuffType.Buff（类型名和成员名同词自撞），所以极性改用 Positive/Negative。
/// 用途：UI 决定图标描边颜色；净化/驱散类效果决定"清哪一半"。
/// </summary>
public enum BuffPolarity
{
    Positive,   // 增益
    Negative,   // 减益
}

/// <summary>
/// 叠加规则（同 STS2 的 PowerStackType）：
///   Counter  —— 层数相加：力量3 + 力量2 = 力量5
///   Duration —— 回合数相加：虚弱2回合 + 1回合 = 3回合，回合末 -1
///   Single   —— 不叠加，有即有（"下回合抽牌+1"这类标记）
/// 具体合并逻辑住在 BuffCmd.Apply（Step B）——本批的 Internal 层只做裸加减。
/// </summary>
public enum BuffStackType
{
    Counter,
    Duration,
    Single,
}