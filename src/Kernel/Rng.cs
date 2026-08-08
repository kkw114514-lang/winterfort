using System;
using System.Collections.Generic;

namespace Kernel;

/// <summary>
/// 一条确定性随机流。
///
/// 【核心设计】状态 = (Seed, Counter) 两个整数，而不是发生器的内部状态。
/// 存档只写这 8 字节；读档时重建发生器再快进 Counter 次追上去。
/// 代价是恢复为 O(Counter)，收益是存档格式永远不会因为换算法而失效。
///
/// 【铁律】（编译器不管，全靠下面的实现纪律 + Program.cs 里的断言）
///   ① 状态是 (Seed, Counter)，不保存 Random 实例
///   ② 只能前进，不能后退
///   ③ 每个取值入口准确 Counter++ 一次，不多不少
///   ④ 字符串种子用自己的哈希，绝不用 string.GetHashCode()
///   ⑤ 表现层随机走 Chaotic，绝不消耗 gameplay 的 Counter
/// </summary>
public sealed class Rng
{
    // 【铁律①】这个字段是"派生状态"，不是"权威状态"。
    // 权威状态只有 Seed 和 Counter，_random 随时可以从它们重建。
    private readonly Random _random;

    public uint Seed { get; }

    /// <summary>已消耗的随机数个数。它和 _random 的推进次数必须严格 1:1。</summary>
    public int Counter { get; private set; }

    /// <summary>
    /// 用 System.Random 作为底层发生器。
    /// 注意：只有【带种子】的构造函数走兼容算法，序列跨 .NET 版本稳定；
    /// 无参构造用的是 xoshiro，每次都不同——这里绝不能用。
    /// </summary>
    public Rng(uint seed, int counter = 0)
    {
        Seed = seed;
        _random = new Random(unchecked((int)seed));
        FastForward(counter);
    }

    /// <summary>按流名派生一条独立流。流名 = 命名空间。</summary>
    public Rng(uint seed, string streamName)
        : this(DeriveSeed(seed, streamName))
    {
    }

    /// <summary>
    /// 【铁律⑤】表现层专用：动画抖动、粒子方向、皮肤挑选走这条。
    /// 它用时间戳做种，每次运行都不同，且绝不参与存档。
    /// 一旦你用它做了任何影响游戏结果的判定，整个确定性就废了。
    /// </summary>
    public static Rng Chaotic { get; } =
        new Rng(unchecked((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    public static uint DeriveSeed(uint seed, string streamName)
        => unchecked(seed + (uint)DeterministicHash(streamName));

    /// <summary>
    /// 【铁律④】跨进程稳定的字符串哈希。
    /// 绝对不能用 string.GetHashCode()——.NET Core 每个进程都会随机化它，
    /// 结果是同一个种子在两次启动里生成完全不同的局，而且单次运行内一切正常，
    /// 极难发现。
    /// </summary>
    public static int DeterministicHash(string s)
    {
        unchecked
        {
            int h1 = 352654597;
            int h2 = h1;
            for (int i = 0; i < s.Length; i += 2)
            {
                h1 = ((h1 << 5) + h1) ^ s[i];
                if (i == s.Length - 1) break;
                h2 = ((h2 << 5) + h2) ^ s[i + 1];
            }
            return h1 + h2 * 1566083941;
        }
    }

    /// <summary>
    /// 【铁律②】快进到指定 Counter。只能前进。
    /// 想"倒带"说明状态管理已经出错了，让它响亮地失败，而不是悄悄给出错误结果。
    /// </summary>
    public void FastForward(int targetCounter)
    {
        if (targetCounter < Counter)
            throw new InvalidOperationException(
                $"Rng 只能前进：当前 Counter={Counter}，目标={targetCounter}。" +
                "要回到更早的状态请重建一个 Rng 再快进。");

        while (Counter < targetCounter)
        {
            Counter++;
            _random.Next();
        }
    }

    // ---------------------------------------------------------------
    // 取值入口。【铁律③】每个都必须准确 Counter++ 一次。
    //
    // "不多"和"不少"同样重要：
    //   少了 → 存档读回来时 _random 落后，后续序列全错位
    //   多了 → 快进时空转次数过多，同样错位
    // 所以下面 NextItem / ShuffleInPlace 自己【不】做 Counter++，
    // 它们把计数完全委托给 NextInt。
    // ---------------------------------------------------------------

    /// <summary>返回 [0, maxExclusive) 内的整数。</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "上界必须为正。");

        Counter++;
        return _random.Next(maxExclusive);
    }

    /// <summary>返回 [minInclusive, maxExclusive) 内的整数。</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "下界必须小于上界。");

        // 【一个真实的地雷】System.Random.Next(min, max) 在区间宽度超过
        // int.MaxValue 时，内部会消耗【两次】底层采样而不是一次。
        // 那样 Counter 和实际推进次数就不再 1:1，FastForward 会算错。
        // 卡牌游戏永远用不到这么大的区间，所以直接禁掉，而不是留个隐患。
        long range = (long)maxExclusive - minInclusive;
        if (range > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "区间宽度不得超过 int.MaxValue：底层发生器在超大区间上会消耗两次采样，破坏 Counter 的 1:1 对应。");

        Counter++;
        return _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>等概率取一个元素。消耗 1 次。</summary>
    public T NextItem<T>(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("NextItem 收到空列表。");

        return items[NextInt(items.Count)];   // Counter 由 NextInt 负责，这里不重复加
    }

    /// <summary>
    /// 原地洗牌。Durstenfeld 版 Fisher-Yates，从后往前。
    /// 【永久冻结】方向和区间开闭一旦改变，同一个种子的抽牌顺序就全变了，
    /// 所有已分享的种子和已存的存档全部作废。别改。
    /// 消耗恰好 n-1 次。
    /// </summary>
    public void ShuffleInPlace<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(i + 1);            // j ∈ [0, i]
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public override string ToString() => $"Rng(seed={Seed}, counter={Counter})";
}