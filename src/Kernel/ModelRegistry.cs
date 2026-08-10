using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Kernel;

/// <summary>
/// 全局内容注册表。每个内容类在这里有且只有一个 canonical 实例。
/// 它不是数据库——不碰磁盘、不查 SQL，就是内存里的一张总表。
/// </summary>
public static class ModelRegistry
{
    private static readonly Dictionary<Type, GameModel> _byType = new Dictionary<Type, GameModel>();
    private static readonly Dictionary<string, GameModel> _byId = new Dictionary<string, GameModel>();
    private static readonly List<GameModel> _registered = new List<GameModel>();
    private static List<GameModel>? _sortedCache;

    // ── 注册 ──────────────────────────────────────────────────

    public static void Register(GameModel model)
    {
        Type type = model.GetType();
        if (_byType.ContainsKey(type))
            throw new InvalidOperationException($"{type.Name} 重复注册。");

        string idKey = model.Id.ToString();
        if (_byId.TryGetValue(idKey, out GameModel? existing))
            throw new InvalidOperationException(
                $"ID 冲突：{type.Name} 和 {existing.GetType().Name} 都推导出 {idKey}。" +
                $"两个类名 Slugify 之后撞了，改其中一个的类名。");

        _byType[type] = model;
        _byId[idKey] = model;
        _registered.Add(model);
        _sortedCache = null;
    }

    /// <summary>
    /// 用反射自动收集一个程序集里所有内容类。
    ///
    /// 【必须排序】反射返回的类型顺序不保证稳定——它会随编译顺序、增量编译、
    /// runtime 版本而变。注册顺序一旦影响到任何东西（默认排序、序列化下标、
    /// 网络 ID 分配），不排序就是一个"在我机器上是对的"的经典 bug。
    ///
    /// 卡超过 100 张之后建议换成源生成器：编译期生成注册表，顺带拿到 AOT/trim 安全。
    /// （STS2 就是这么做的，425KB / 1600 个类，全自动生成。）
    /// </summary>
    public static void RegisterAllInAssembly(Assembly assembly)
    {
        IEnumerable<Type> types = assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(GameModel).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (Type t in types)
        {
            ConstructorInfo? ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                throw new InvalidOperationException($"{t.Name} 没有无参构造函数，无法注册为内容。");
            Register((GameModel)ctor.Invoke(null));
        }
    }

    public static void Clear()
    {
        _byType.Clear();
        _byId.Clear();
        _registered.Clear();
        _sortedCache = null;
    }

    // ── 查询 ──────────────────────────────────────────────────

    public static bool IsRegistered(Type type) => _byType.ContainsKey(type);

    /// <summary>拿 canonical 定义实例（只读）。打错类名编译不过。</summary>
    public static T Get<T>() where T : GameModel
    {
        if (!_byType.TryGetValue(typeof(T), out GameModel? m))
            throw new KeyNotFoundException($"{typeof(T).Name} 未注册。检查 RegisterAllInAssembly 有没有跑。");
        return (T)m;
    }

    /// <summary>拿一个新的可变副本。发牌、生成卡、给玩家发眷属时用。</summary>
    public static T New<T>() where T : GameModel => Get<T>().MutableCloneAs<T>();

    public static GameModel GetById(ModelId id)
    {
        if (!_byId.TryGetValue(id.ToString(), out GameModel? m))
            throw new KeyNotFoundException($"找不到 ID 为 {id} 的内容。存档里可能有已删除的条目。");
        return m;
    }

    /// <summary>读档专用：存档里可能有当前版本已删除的内容，不该崩溃。</summary>
    public static bool TryGetById(ModelId id, out GameModel? model)
        => _byId.TryGetValue(id.ToString(), out model);

    /// <summary>全量，按 Id 排序。做卡池、算校验和、导出数据表都走它——顺序确定。</summary>
    public static IReadOnlyList<GameModel> All
        => _sortedCache ??= _registered.OrderBy(m => m.Id).ToList();

    public static IEnumerable<T> AllOf<T>() where T : GameModel => All.OfType<T>();

    public static int Count => _registered.Count;

    // ── ID 推导 ───────────────────────────────────────────────

    /// <summary>
    /// Category = 继承链上 GameModel 的直接子类的名字（去掉 _MODEL 后缀）
    /// Entry    = 类名本身
    /// 例：Fireball : CardModel : GameModel  →  CARD.FIREBALL
    /// </summary>
    public static ModelId DeriveId(Type type)
    {
        Type categoryType = CategoryTypeOf(type);
        string category = ModelId.Slugify(categoryType.Name);
        const string suffix = "_MODEL";
        if (category.EndsWith(suffix, StringComparison.Ordinal))
            category = category.Substring(0, category.Length - suffix.Length);

        return new ModelId(category, ModelId.Slugify(type.Name));
    }

    public static ModelId DeriveId<T>() where T : GameModel => DeriveId(typeof(T));

    private static Type CategoryTypeOf(Type type)
    {
        Type? t = type;
        while (t != null && t.BaseType != typeof(GameModel))
            t = t.BaseType;
        if (t == null)
            throw new InvalidOperationException($"{type.Name} 不是 GameModel 的子类。");
        return t;
    }
}