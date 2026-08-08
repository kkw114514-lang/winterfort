using Kernel;

int failures = 0;

void Check(bool ok, string label, string detail = "")
{
    if (ok) Console.WriteLine($"  ✔ {label}");
    else { failures++; Console.WriteLine($"  ✘ {label}   {detail}"); }
}

Console.WriteLine("[1] 随机数确定性");

// ── 断言 1：可复现 ────────────────────────────────────────────
// 铁律①的直接检验：同一个种子必须产出同一条序列。
{
    var a = new Rng(12345);
    var b = new Rng(12345);

    int mismatch = -1;
    for (int i = 0; i < 1000; i++)
        if (a.NextInt(1000) != b.NextInt(1000)) { mismatch = i; break; }

    Check(mismatch < 0, "同种子两个 Rng：1000 个数逐个相同",
        mismatch >= 0 ? $"第 {mismatch} 个开始分叉" : "");

    Check(a.Counter == 1000, "Counter 准确等于取值次数", $"实际 {a.Counter}");
}

// ── 断言 2：可恢复 ────────────────────────────────────────────
// 【最关键的一条】它证明"存档只存两个整数"这个设计真的成立。
// 如果它红了，说明某个取值入口的 Counter++ 数目不对（铁律③被破坏）。
{
    var live = new Rng(777);
    for (int i = 0; i < 500; i++) live.NextInt(100);

    uint savedSeed = live.Seed;        // ← 存档里
    int savedCounter = live.Counter;   // ← 只有这两个数

    var restored = new Rng(savedSeed, savedCounter);

    Check(restored.Counter == savedCounter, "恢复后 Counter 一致",
        $"{restored.Counter} vs {savedCounter}");

    int mismatch = -1;
    for (int i = 0; i < 500; i++)
        if (live.NextInt(100) != restored.NextInt(100)) { mismatch = i; break; }

    Check(mismatch < 0, "从 (Seed, Counter) 恢复后，后续 500 个数与原流逐个相同",
        mismatch >= 0 ? $"第 {mismatch} 个开始分叉" : "");
}

// ── 断言 3：流隔离 ────────────────────────────────────────────
{
    const uint master = 20260809u;

    var combat = new Rng(master, "combat");
    var shop = new Rng(master, "shop");

    Check(combat.Seed != shop.Seed, "不同流名派生出不同的实际种子",
        $"combat={combat.Seed}, shop={shop.Seed}");

    bool diverged = false;
    for (int i = 0; i < 200; i++)
        if (combat.NextInt(1_000_000) != shop.NextInt(1_000_000)) { diverged = true; break; }
    Check(diverged, "两条流的序列不同");

    // 反过来也要成立：同一个流名重建，必须完全复现
    var again1 = new Rng(master, "combat");
    var again2 = new Rng(master, "combat");
    bool same = true;
    for (int i = 0; i < 200; i++)
        if (again1.NextInt(1000) != again2.NextInt(1000)) { same = false; break; }
    Check(same, "同一流名重建，序列完全一致");
}

// ── 附加 4：哈希黄金值 ────────────────────────────────────────
// 【黄金值测试】把哈希函数的输出钉死成常量。
// 它是种子派生的一部分——哪天有人手滑改成 string.GetHashCode()，
// 或者优化了一下算法，这三条会立刻变红。
// 如果没有这层保护，那种改动在单次运行里毫无症状，
// 但所有已分享的种子和已存的存档会全部对不上。
{
    int hCombat = Rng.DeterministicHash("combat");
    int hShop = Rng.DeterministicHash("shop");
    int hShuffle = Rng.DeterministicHash("shuffle");

    Check(hCombat == -1966090442, "哈希黄金值 \"combat\"", $"实际 {hCombat}");
    Check(hShop == 1013778026, "哈希黄金值 \"shop\"", $"实际 {hShop}");
    Check(hShuffle == -1986686621, "哈希黄金值 \"shuffle\"", $"实际 {hShuffle}");
}

// ── 附加 5：只能前进 ──────────────────────────────────────────
{
    var r = new Rng(1);
    for (int i = 0; i < 10; i++) r.NextInt(10);

    bool threw = false;
    try { r.FastForward(5); }
    catch (InvalidOperationException) { threw = true; }

    Check(threw, "FastForward 回退时抛异常（铁律②）");
}

// ── 附加 6：洗牌消耗次数精确 ──────────────────────────────────
{
    var r = new Rng(42);
    var list = new List<int>();
    for (int i = 0; i < 10; i++) list.Add(i);

    int before = r.Counter;
    r.ShuffleInPlace(list);
    Check(r.Counter - before == 9, "洗 10 张牌消耗恰好 9 次（铁律③）",
        $"实际 {r.Counter - before}");

    var l1 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
    var l2 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
    new Rng(999).ShuffleInPlace(l1);
    new Rng(999).ShuffleInPlace(l2);
    Check(l1.SequenceEqual(l2), "同种子洗牌结果一致");
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? "全部通过 ✔" : $"{failures} 条断言失败 ✘");
return failures == 0 ? 0 : 1;