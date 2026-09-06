using System;

namespace Kernel;

public enum CardType
{
    Attack,     // 攻击
    Spell,      // 法术
    Aura,       // 永续 —— 打出后离场，本场不再出现，战斗结束回牌库
    Bane,      // 灾厄 —— 永久留在卡组
    Dross,     // 干扰 —— 战斗结束消失
    Quest,      // 任务 —— 任务发放的携带物；转化/复制类效果须排除它（同 STS2，做那类效果时落实）
}

/// <summary>
/// 稀有度 = 获取渠道 + 掉落权重，不是强度标签（同 STS2 的用法）。
/// 十二档里只有 Common/Uncommon/Rare 带权重——将来 run 层的加权 roll
/// 只会掷出这三档；其余全是"定向发放"的渠道标签，roll 永远掷不出。
///
/// 【顺序有意义】权重梯子固定在前，特殊档在后。以后加档只许追加到末尾、
/// 不许往中间插——一旦有代码按序号做区间判断（STS2 反编译里真的有），
/// 中间插档就是隐雷。
/// </summary>
public enum CardRarity
{
    /// <summary>算法哨兵，不是任何卡的稀有度——构造时禁止（见 CardModel 构造断言）。
    /// 留给将来"沿梯子向上找"类算法当终点信号（同 STS2 GetNextHighestRarity）。</summary>
    None,

    /// <summary>初始卡。不进商店、不进掉落、不进战斗内生成（同 STS2 Basic）。
    /// 惯例：Element 为 Basic（通用基石）的卡必须标这一档。</summary>
    Basic,

    // ── 权重梯子：掉落/商店/奖励的加权 roll 只掷这三档 ──
    Common,     // 白
    Uncommon,   // 蓝
    Rare,       // 金

    // ── 渠道标签：roll 掷不出，全部定向发放 ──

    /// <summary>契约师卡（对应 STS2 Ancient）。一切常规渠道全排除，特殊途径获得。</summary>
    Pactbearer,

    /// <summary>精灵（眷属）专属卡。随精灵进牌组；精灵被移除时这些卡一并移除
    ///（联动在 run 层实现，靠这一档识别该移除哪些卡）。</summary>
    Familiar,

    /// <summary>事件卡：只从事件获得。</summary>
    Event,

    /// <summary>战斗中生成的卡（非精灵来源）。</summary>
    Token,

    /// <summary>干扰卡的档位，与 CardType.Dross 同词——战斗中被塞进牌组。</summary>
    Dross,

    /// <summary>灾厄卡的档位，与 CardType.Bane 同词——事件/惩罚塞入。</summary>
    Bane,

    /// <summary>任务卡：任务发放。</summary>
    Quest,
}

/// <summary>
/// 卡牌系别。[Flags]：为双系卡（如水火）预留——单系卡照旧传单值。
/// 判定纪律：永远用 HasElement，禁止 ==（双系卡会让 == 静默漏判）。
/// </summary>
[Flags]
public enum CardElement
{
    None  = 0,        // flags 卫生位。构造时禁止（见 CardModel 构造断言）
    Basic = 1 << 0,   // B 通用基石——与其他系互斥（不参与双系）。掉落排除已由 CardRarity.Basic 承担
    Fire  = 1 << 1,   // F 火
    Water = 1 << 2,   // W 水
    Grass = 1 << 3,   // G 草
    Light = 1 << 4,   // L 光
    Dark  = 1 << 5,   // D 暗
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
/// 第一批住户（微裁定6）：油脂弹按 Immolate 识别"下一张焚毁卡"，
/// 薪火长明/油库引爆按 Fuel 识别"燃料卡"。行为本体仍是 virtual（OnFuel/选择器调用），
/// Tag 只回答"这张卡属不属于这一族"。
/// </summary>
public enum CardTag
{
    Fuel,       // 燃料：卡面带「燃料：」冒号效果
    Immolate,   // 焚毁：卡面带「焚毁 N」
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