using System;

namespace Kernel;

public enum CardType
{
    Attack,     // 攻击
    Spell,      // 法术
    Aura,       // 永续 —— 打出后离场，本场不再出现，战斗结束回牌库
    Bane,      // 灾厄 —— 永久留在卡组
    Dross,     // 干扰 —— 战斗结束消失
}

public enum CardRarity
{
    Common,     // 白
    Uncommon,   // 蓝
    Rare,       // 金
    Special,    // 红 —— 仅特殊场景，不进常规掉落
}

public enum CardElement
{
    Basic,      // B 通用基石 —— 不进任何掉落池，只作起始卡组素材
    Fire,       // F 火
    Water,      // W 水
    Grass,      // G 草
    Light,      // L 光
    Dark,       // D 暗
}

/// <summary>
/// 卡牌关键词：【引擎读它】，它改变这张卡怎么被对待。
/// 效果对所有卡一致的才进这里——"冒号后效果因卡而异"的（燃料/虚无/收割/生长）
/// 是 CardModel 上的 virtual 方法，不是关键词。
/// </summary>
public enum CardKeyword
{
    Eternal,      // 缠身：无法被移出牌组
    Exhaust,      // 消耗：打出后移出本场战斗
    Retain,       // 保留：回合结束不被弃置
    Temporary,    // 临时：回合结束时若仍在手上，将其消耗
    Innate,       // 本能：战斗开始必定在起手牌
    Unplayable,   // 无法打出
    Epitaph,      // 遗言：被弃置时免费打出自己（回合末自然弃牌不触发）
}

/// <summary>
/// 卡牌族标记：【引擎不读它，只有内容读】——让别的卡/遗物能识别"某一族卡"。
/// 例："每有一张基石卡 +2 伤害"。
/// 现在是空的，等出现第一个这种需求再往里加。
/// </summary>
public enum CardTag
{
}

public enum TargetType
{
    None,          // 不需要目标
    Self,          // 自己
    SingleEnemy,   // 单个敌人
    AllEnemies,    // 全体敌人
}

/// <summary>
/// "这张牌为什么打不出"——多个毫不相干的来源收口到一个 flags。
///
/// 它不只答"能不能"，还答"为什么"：UI 因此能显示"能量不足"而不是干瘪的
/// "不可打出"，遗言自动打出的路径也能弹气泡说明是哪个遗物挡的。
///
/// 这是这一层唯一从第一天就该收口的问题——它【天生】多来源。
/// 其余关键词现在都是单来源，直接 HasKeyword 查即可。
///
/// 纪律：以后要加信息就【加一个新的 out 参数】，绝不改现有参数的含义。
/// </summary>
[Flags]
public enum UnplayableReason
{
    None                 = 0,
    HasUnplayableKeyword = 1 << 0,
    EnergyTooHigh        = 1 << 1,
    BlockedByHook        = 1 << 2,   // 遗物 / 能力否决（战斗层接上后填）
    BlockedByCardLogic   = 1 << 3,   // 卡自身条件不满足
    NoValidTarget        = 1 << 4,
}