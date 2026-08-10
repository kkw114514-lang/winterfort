using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel;

/// <summary>
/// 随机流的标识。
///
/// 【铁律】枚举成员名可以随便改，但 <see cref="RngSet.NameOf"/> 里对应的
/// 那行字符串是【种子派生的输入】，改了等于换了一局游戏——所有已分享的种子、
/// 所有存档、每日挑战全部作废。
///
/// 加流是免费的（新流对已有流零影响）。拆流是破坏性的（把已有系统挪到新流会让老存档分叉）。
/// 所以：宁可为一个只用一次的随机单开一条，也别复用别的流。
/// </summary>
public enum RngStream
{
    // ── 开局 / 章首 ──
    UpFront,                // 开局遗物（起始眷属固定，不随机）
    EncounterLineup,        // 每章开始时把整章遭遇排成队列
    FamiliarOffer,          // 章首眷属 3 选 1 的候选生成（第一章顺带定副系）

    // ── 战斗内 ──
    Shuffle,                // 抽牌堆洗回
    CombatTargets,          // 随机选靶（"对随机 1 个敌人"）
    MonsterAi,              // 敌人掷意图
    MonsterHp,              // 敌人血量在 min~max 内掷值
    CombatCardGeneration,   // 战斗中「生成」新卡
    CombatCardSelection,    // 战斗中「挑」已有的牌（随机弃/随机消耗/敌人偷牌）
    CombatEnergyCosts,      // 随机费用（占位，未使用时 counter 恒为 0，零成本）

    // ── 战斗外 ──
    CardReward,             // 战后 3 选 1：池选择 + 稀有度掷值 + 取卡
    ItemDrop,               // 一次性道具掉落
    RelicReward,            // 遗物奖励 / 精英必掉 / 宝箱
    UnknownMapPoint,        // 未知节点掷房型（运行时消费，带保底累积）
                            // 暴风雪只遮视野、不改节点，不消耗此流
    Event,                  // 事件房选项与结果（含移除眷属的事件）
    Shop,                   // 商店刷新
    Altar,                  // 祭坛（设计中，先占一条）
    Transformation,         // 卡牌转化（占位）

    // ── 兜底 ──
    Misc,                   // 一时想不好归哪的。每次往这里加东西之前先问一句
                            // "这个值得单独一条吗"——答案通常是值得。
}

/// <summary>
/// 一局游戏的全部随机流。
///
/// 【为什么要分流】如果只有一条流，"战斗里多掷了一次骰子"会把之后所有商店、
/// 事件、奖励整体平移。分流之后，改战斗逻辑不会动到商店。
/// 对一个要长期打补丁、还要支持种子分享的 roguelike，这是刚需不是优化。
///
/// 【存档】只存 StringSeed + 每条流的 counter。19 个 int，几十字节。
/// </summary>
public sealed class RngSet
{
    private readonly Dictionary<RngStream, Rng> _streams = new Dictionary<RngStream, Rng>();

    /// <summary>玩家看到、分享、输入的那个字符串。</summary>
    public string StringSeed { get; }

    /// <summary>由 StringSeed 派生的主种子。所有流都从它 + 流名派生。</summary>
    public uint Seed { get; }

    public RngSet(string stringSeed)
    {
        if (string.IsNullOrEmpty(stringSeed))
            throw new ArgumentException("种子字符串不能为空。", nameof(stringSeed));

        StringSeed = stringSeed;
        Seed = unchecked((uint)Rng.DeterministicHash(stringSeed));

        // 构造时就把 19 条流全建出来。
        // 副作用：任何忘了在 NameOf 里登记名字的枚举成员，在这里就会炸，
        // 而不是等到游戏跑到那个系统时才炸。
        foreach (RngStream stream in AllStreams)
            _streams[stream] = Create(stream);
    }

    /// <summary>枚举全量，顺序固定（按枚举声明顺序）。所有遍历都走它。</summary>
    public static readonly IReadOnlyList<RngStream> AllStreams =
        ((RngStream[])Enum.GetValues(typeof(RngStream))).ToList();

    private Rng Create(RngStream stream) => new Rng(Seed, NameOf(stream));

    /// <summary>
    /// 流名 —— 【种子派生的真正输入】。
    ///
    /// 刻意不用 enum.ToString()：那样会把 C# 标识符和哈希输入绑死，
    /// 以后重命名一个枚举成员就等于毁掉那条流的所有历史种子。
    /// 这里把名字当【数据】，代码可以随便重构。
    ///
    /// 存档序列化也用这个字符串，不要用枚举序号——否则枚举中间插一个成员，
    /// 所有旧存档的流就全对错位了。
    /// </summary>
    public static string NameOf(RngStream stream) => stream switch
    {
        RngStream.UpFront              => "up_front",
        RngStream.EncounterLineup      => "encounter_lineup",
        RngStream.FamiliarOffer        => "familiar_offer",

        RngStream.Shuffle              => "shuffle",
        RngStream.CombatTargets        => "combat_targets",
        RngStream.MonsterAi            => "monster_ai",
        RngStream.MonsterHp            => "monster_hp",
        RngStream.CombatCardGeneration => "combat_card_generation",
        RngStream.CombatCardSelection  => "combat_card_selection",
        RngStream.CombatEnergyCosts    => "combat_energy_costs",

        RngStream.CardReward           => "card_reward",
        RngStream.ItemDrop             => "item_drop",
        RngStream.RelicReward          => "relic_reward",
        RngStream.UnknownMapPoint      => "unknown_map_point",
        RngStream.Event                => "event",
        RngStream.Shop                 => "shop",
        RngStream.Altar                => "altar",
        RngStream.Transformation       => "transformation",

        RngStream.Misc                 => "misc",

        // 新增了枚举成员却忘了登记名字 → 构造 RngSet 时立刻炸。
        // 别删这行。它是这条纪律唯一的执行者。
        _ => throw new ArgumentOutOfRangeException(
                 nameof(stream), stream, "该流没有登记名字，请在 RngSet.NameOf 里补上。"),
    };

    /// <summary>从流名反查枚举。读档时用（存档里存的是名字）。</summary>
    public static bool TryParseStream(string name, out RngStream stream)
    {
        foreach (RngStream s in AllStreams)
        {
            if (NameOf(s) == name) { stream = s; return true; }
        }
        stream = default;
        return false;   // 未知流名 = 存档来自更新的版本，调用方应当忽略而不是崩溃
    }

    public Rng this[RngStream stream] => _streams[stream];

    // 战斗内高频的几条给个快捷属性，其余走索引器。
    // 不给全部 19 个加属性——那只是维护负担，索引器已经够清楚了。
    public Rng Shuffle => _streams[RngStream.Shuffle];
    public Rng CombatTargets => _streams[RngStream.CombatTargets];
    public Rng MonsterAi => _streams[RngStream.MonsterAi];

    /// <summary>
    /// 一次性流：用完即弃，不进存档，不占枚举位。
    ///
    /// 适用于【结果是纯函数】的东西——典型是地图生成：
    ///     var mapRng = rngSet.OneShot($"act_{actIndex}_map");
    /// 同样的 (种子, 幕号) 永远生成同样的地图，所以不需要记 counter，
    /// 而且【可以任意次重新生成而不产生漂移】。
    ///
    /// 后面这条性质很值钱：像"黄金罗盘/暴风雪"这种把整幕地图换掉的机制，
    /// 需要在游戏中途重新跑一遍地图生成。如果地图用的是带 counter 的正式流，
    /// 每重算一次就推进一次 counter，后续所有随机都会偏移——那种机制根本没法实现。
    /// </summary>
    public Rng OneShot(string name) => new Rng(Seed, name);

    // ────────────────────────────────────────────────────────────
    // 存档接口
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// 存档快照。
    ///
    /// 【返回 List 而不是 Dictionary】因为这份快照会进存档、以后还会进校验和，
    /// 而 Dictionary 的遍历顺序是实现细节，会随插入删除历史漂移。
    /// 任何进入序列化或校验和的集合都必须有显式定义的顺序。
    /// </summary>
    public List<KeyValuePair<RngStream, int>> Snapshot()
    {
        var result = new List<KeyValuePair<RngStream, int>>(AllStreams.Count);
        foreach (RngStream stream in AllStreams)   // 按枚举声明顺序，不是按字典顺序
            result.Add(new KeyValuePair<RngStream, int>(stream, _streams[stream].Counter));
        return result;
    }

    /// <summary>
    /// 把快照套用到这个 RngSet 上。
    ///
    /// 双向容错：
    ///   · 快照里有、我不认识的流   → 忽略（玩家从新版本回退到旧版本）
    ///   · 我有、快照里没有的流     → 保持当前值（老存档升级到新版本，新流从 0 开始）
    ///   · 快照的 counter 比当前小  → 重建那条流再快进（Rng 不能后退）
    /// </summary>
    public void Restore(IEnumerable<KeyValuePair<RngStream, int>> snapshot)
    {
        foreach (KeyValuePair<RngStream, int> entry in snapshot)
        {
            if (!_streams.TryGetValue(entry.Key, out Rng? rng))
                continue;   // 不认识的流，跳过

            if (entry.Value < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(snapshot), $"流 {NameOf(entry.Key)} 的 counter 为负数：{entry.Value}");

            if (entry.Value < rng.Counter)
            {
                // 本地已经跑超了（读旧档 / 联机回滚）。Rng 只能前进，所以重建再快进。
                Rng rebuilt = Create(entry.Key);
                rebuilt.FastForward(entry.Value);
                _streams[entry.Key] = rebuilt;
            }
            else
            {
                rng.FastForward(entry.Value);
            }
        }
    }

    public override string ToString() => $"RngSet(\"{StringSeed}\" → {Seed})";
}