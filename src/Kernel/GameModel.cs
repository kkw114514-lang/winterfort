using System;
using System.Text;

namespace Kernel;

/// <summary>
/// 内容的唯一标识：Category.Entry，两段都从类名机械推导。
/// 例：Fireball : CardModel : GameModel  →  CARD.FIREBALL
///
/// 【它会进存档】所以一旦发布就不能改。改类名 = 改 ID = 老存档里那张卡找不到了。
/// （真到那天，做法是留一个 Deprecated 占位类，而不是改 ID。）
/// </summary>
public sealed record ModelId : IComparable<ModelId>
{
    public string Category { get; }
    public string Entry { get; }

    public ModelId(string category, string entry)
    {
        if (string.IsNullOrEmpty(category)) throw new ArgumentException("Category 不能为空", nameof(category));
        if (string.IsNullOrEmpty(entry)) throw new ArgumentException("Entry 不能为空", nameof(entry));
        Category = category;
        Entry = entry;
    }

    public int CompareTo(ModelId? other)
    {
        if (other is null) return 1;
        int c = string.CompareOrdinal(Category, other.Category);
        return c != 0 ? c : string.CompareOrdinal(Entry, other.Entry);
    }

    public override string ToString() => $"{Category}.{Entry}";

    public static ModelId Parse(string s)
    {
        string[] parts = s.Split('.');
        if (parts.Length != 2)
            throw new FormatException($"\"{s}\" 不是合法的 ModelId（应形如 CARD.FIREBALL）");
        return new ModelId(parts[0], parts[1]);
    }

    /// <summary>PascalCase → SNAKE_CASE。Fireball → FIREBALL，StrikeIronclad → STRIKE_IRONCLAD。</summary>
    public static string Slugify(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c))
            {
                bool prevIsLowerOrDigit = char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]);
                bool nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (prevIsLowerOrDigit || (nextIsLower && char.IsUpper(name[i - 1])))
                    sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}

/// <summary>
/// 所有内容（卡 / 眷属 / 遗物 / 怪物 / 附魔 / 灾厄）的根类。
///
/// ═══ canonical / mutable 双态 ═══
///
///   canonical 实例 = ModelRegistry 里那唯一一个，代表"这张卡的定义"，只读
///   mutable  实例 = MemberwiseClone 出来的运行时副本，牌组里每张牌一个
///
/// 这个技巧省掉了整整一层类：不用写 CardDefinition + CardInstance 两套、
/// 不用工厂、不用映射代码。对一个人做几百张卡，这可能就是几千行。
///
/// 代价见 DeepCloneFields / AfterCloned 上的注释——那是这套设计唯一的软肋。
/// </summary>
public abstract class GameModel
{
    public ModelId Id { get; }

    public bool IsMutable { get; private set; }
    public bool IsCanonical => !IsMutable;

    protected GameModel()
    {
        Type type = GetType();

        // 【唯一的注册闸门】canonical 实例只能由 ModelRegistry 创建一次。
        // 注意 MemberwiseClone【不调用构造函数】，所以这个检查不会误伤克隆。
        if (ModelRegistry.IsRegistered(type))
            throw new InvalidOperationException(
                $"{type.Name} 已经有 canonical 实例了。别自己 new 内容对象——" +
                $"要定义用 ModelRegistry.Get<{type.Name}>()，要副本用 ModelRegistry.New<{type.Name}>()。");

        Id = ModelRegistry.DeriveId(type);
    }

    /// <summary>写入路径的第一行都调它。canonical 实例走到这里直接抛。</summary>
    protected void AssertMutable()
    {
        if (!IsMutable)
            throw new InvalidOperationException(
                $"{Id} 是 canonical 定义实例，禁止修改。要改请先取一个副本：ModelRegistry.New<{GetType().Name}>()。");
    }

    protected void AssertCanonical()
    {
        if (IsMutable)
            throw new InvalidOperationException($"{Id} 是运行时副本，这个操作只允许对 canonical 实例做。");
    }

    /// <summary>造一个可变副本。发牌、生成卡、给玩家发眷属时用。</summary>
    public GameModel MutableClone()
    {
        var clone = (GameModel)MemberwiseClone();
        clone.IsMutable = true;
        clone.DeepCloneFields();
        clone.AfterCloned();
        return clone;
    }

    public T MutableCloneAs<T>() where T : GameModel => (T)MutableClone();

    /// <summary>
    /// "复制这一张"的语义：canonical 返回自己（不需要副本），mutable 返回一个新副本。
    /// 战斗中"复制一张手牌"这类效果用它。
    /// </summary>
    public GameModel ClonePreservingMutability() => IsMutable ? MutableClone() : this;

    // ════════════════════════════════════════════════════════════════
    // 【这套设计唯一的软肋，两条都没有编译期保障】
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// MemberwiseClone 是【浅拷贝】——引用类型字段复制的是引用本身，
    /// 两个实例会共享同一个对象。
    ///
    /// 每一个可变的集合 / 可变对象字段，都必须在这里重新 new 一份：
    ///     _keywords = new HashSet&lt;Keyword&gt;(_keywords);
    ///
    /// 漏一个的后果：升级一张卡时加的关键词会漏到 canonical 定义上，
    /// 于是全世界所有这张卡一起变。而且它只在特定操作序列下暴露，极难复现。
    /// </summary>
    protected virtual void DeepCloneFields() { }

    /// <summary>
    /// MemberwiseClone 会把 event 的【委托链】一起复制。
    /// 每一个 event 都必须在这里置 null：
    ///     Drawn = null; Discarded = null;
    ///
    /// 漏一个的后果：新克隆出来的牌继承了旧牌的所有订阅者 → 幽灵回调 + 内存泄漏。
    /// STS2 在 CardModel.AfterCloned 里逐个 null 掉 8 个 event，全靠人记得。
    /// </summary>
    protected virtual void AfterCloned() { }

    public override string ToString() => Id.ToString();
}