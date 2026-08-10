using Kernel;
using TestContent;
Console.OutputEncoding = System.Text.Encoding.UTF8;

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
Console.WriteLine("[2] RngSet");

// ── 断言 1：同种子，19 条流逐条复现 ──
{
    var a = new RngSet("SEED-ALPHA");
    var b = new RngSet("SEED-ALPHA");

    RngStream? bad = null;
    foreach (RngStream s in RngSet.AllStreams)
    {
        for (int i = 0; i < 100; i++)
            if (a[s].NextInt(10000) != b[s].NextInt(10000)) { bad = s; break; }
        if (bad != null) break;
    }
    Check(bad == null, "同种子：19 条流各取 100 个数全部一致",
        bad != null ? $"{RngSet.NameOf(bad.Value)} 流不一致" : "");
}

// ── 断言 2：存档往返（最关键的一条）──
{
    var live = new RngSet("SAVE-TEST");
    // 在不同流上消耗不同次数，模拟真实一局跑到中途
    for (int i = 0; i < 37; i++) live[RngStream.Shuffle].NextInt(50);
    for (int i = 0; i < 12; i++) live[RngStream.CardReward].NextInt(50);
    for (int i = 0; i < 5;  i++) live[RngStream.Shop].NextInt(50);

    var snapshot = live.Snapshot();            // ← 这就是存档里的全部内容

    var restored = new RngSet("SAVE-TEST");    // 读档 = 重建
    restored.Restore(snapshot);                //      + 恢复

    Check(RngSet.AllStreams.All(s => restored[s].Counter == live[s].Counter),
        "恢复后 19 条流的 Counter 全部一致");

    RngStream? bad = null;
    foreach (RngStream s in RngSet.AllStreams)
    {
        for (int i = 0; i < 50; i++)
            if (live[s].NextInt(10000) != restored[s].NextInt(10000)) { bad = s; break; }
        if (bad != null) break;
    }
    Check(bad == null, "恢复后，后续序列与原局逐个相同",
        bad != null ? $"{RngSet.NameOf(bad.Value)} 流分叉" : "");
}

// ── 断言 3：流互不干扰 ──
{
    var quiet = new RngSet("ISOLATION");
    var noisy = new RngSet("ISOLATION");
    for (int i = 0; i < 500; i++) noisy[RngStream.Shuffle].NextInt(100);   // 只狂用 Shuffle

    Check(noisy[RngStream.Shop].Counter == 0, "狂用 Shuffle 后 Shop 的 Counter 仍为 0");

    bool same = true;
    for (int i = 0; i < 100; i++)
        if (quiet[RngStream.Shop].NextInt(1000) != noisy[RngStream.Shop].NextInt(1000))
            { same = false; break; }
    Check(same, "Shuffle 消耗 500 次，Shop 流序列纹丝不动");
}

// ── 断言 4：整条派生链黄金值 ──
// 【第一次跑先留 0，把 detail 里打印的实际值粘回来】
{
    var golden = new RngSet("golden-test");
    int s1 = golden[RngStream.Shop].NextInt(1000);
    int s2 = golden[RngStream.Shop].NextInt(1000);
    int m1 = golden[RngStream.MonsterAi].NextInt(100);

    Check(s1 == 981 && s2 == 823 && m1 == 12, "整条派生链黄金值", $"实际 {s1},{s2},{m1}");
}

// ── 断言 5：流名往返 ──
{
    Check(RngSet.AllStreams.All(s =>
              RngSet.TryParseStream(RngSet.NameOf(s), out var parsed) && parsed == s),
          "19 条流名都能正确反查");

    Check(!RngSet.TryParseStream("no_such_stream", out _),
          "未知流名返回 false 而不是崩溃");
}
Console.WriteLine();
Console.WriteLine("[3] CardModel");

ModelRegistry.Clear();
ModelRegistry.RegisterAllInAssembly(typeof(TestBasicAttack).Assembly);

// ── ID 从类名派生 ──
{
    Check(ModelRegistry.Get<TestBasicAttack>().Id.ToString() == "CARD.TEST_BASIC_ATTACK",
        "ID 从类名机械派生", ModelRegistry.Get<TestBasicAttack>().Id.ToString());
    Check(ModelRegistry.Count == 8, $"注册了 {ModelRegistry.Count} 个内容（应为 8）");
}

// ── canonical 只读 ──
{
    var canonical = ModelRegistry.Get<TestBasicAttack>();
    bool threw = false;
    try { canonical.Upgrade(); } catch (InvalidOperationException) { threw = true; }
    Check(threw, "canonical 实例调用 Upgrade 抛异常");
    Check(!canonical.IsUpgraded, "canonical 状态未被污染");
}

// ── 数值深拷贝隔离 ──
{
    var a = ModelRegistry.New<TestBasicAttack>();
    var b = ModelRegistry.New<TestBasicAttack>();
    a.Upgrade();

    Check(a.Vars.Damage.Int == 9, "升级后 a 的伤害是 9", $"实际 {a.Vars.Damage.Int}");
    Check(b.Vars.Damage.Int == 6, "另一个副本 b 不受影响", $"实际 {b.Vars.Damage.Int}");
    Check(ModelRegistry.Get<TestBasicAttack>().Vars.Damage.Int == 6, "canonical 定义未被污染");
}

// ── 关键词集合深拷贝隔离【DeepCloneFields 的守门员】──
{
    var a = ModelRegistry.New<TestKeywordCard>();
    var b = ModelRegistry.New<TestKeywordCard>();
    a.Upgrade();   // 加了 Retain

    Check(a.HasKeyword(CardKeyword.Retain), "升级后 a 获得保留");
    Check(!b.HasKeyword(CardKeyword.Retain), "副本 b 没被波及");
    Check(!ModelRegistry.Get<TestKeywordCard>().HasKeyword(CardKeyword.Retain),
        "canonical 的关键词集合没被污染");
    Check(a.HasKeyword(CardKeyword.Exhaust) && b.HasKeyword(CardKeyword.Exhaust),
        "原有关键词都还在");
}

// ── 费用升级 ──
{
    var c = ModelRegistry.New<TestKeywordCard>();
    Check(c.Cost == 2, "升级前 2 费", $"实际 {c.Cost}");
    c.Upgrade();
    Check(c.Cost == 1, "升级后 1 费", $"实际 {c.Cost}");
    Check(ModelRegistry.Get<TestKeywordCard>().Cost == 2, "canonical 费用未变");
}

// ── 数值与文案同源 ──
{
    var e = ModelRegistry.New<TestBasicAttack>();
    Check(e.Describe() == "造成 6 点伤害。", "描述渲染基础值", e.Describe());
    e.Upgrade();
    Check(e.Describe() == "造成 9 点伤害。", "升级后描述自动跟着变", e.Describe());
    Check(e.Title == "余烬+", "单级卡标题带 +", e.Title);
}

// ── 多级升级（灼热打击式）──
{
    var w = ModelRegistry.New<TestMultiUpgrade>();
    Check(w.Title == "灼烧" && w.Vars.Damage.Int == 12, "初始 12", $"{w.Title} / {w.Vars.Damage.Int}");

    w.Upgrade();
    Check(w.Title == "灼烧+1" && w.Vars.Damage.Int == 16, "第 1 级 +4 → 16", $"{w.Title} / {w.Vars.Damage.Int}");

    w.Upgrade();
    Check(w.Vars.Damage.Int == 21, "第 2 级 +5 → 21", $"实际 {w.Vars.Damage.Int}");

    w.Upgrade();
    Check(w.Vars.Damage.Int == 27, "第 3 级 +6 → 27", $"实际 {w.Vars.Damage.Int}");

    w.Upgrade();
    Check(w.Vars.Damage.Int == 34, "第 4 级 +7 → 34", $"实际 {w.Vars.Damage.Int}");

    Check(w.Title == "灼烧+4", "多级卡标题带数字", w.Title);
    Check(ModelRegistry.Get<TestMultiUpgrade>().Vars.Damage.Int == 12, "canonical 仍是 12");
}

// ── 升级上限 ──
{
    var e = ModelRegistry.New<TestBasicAttack>();
    e.Upgrade();
    Check(!e.IsUpgradable, "单级卡升过一次后不可再升");
    e.Upgrade();
    Check(e.CurrentUpgradeLevel == 1 && e.Vars.Damage.Int == 9, "超上限再升无效（数值没变）");
}

// ── 升级预览 = 克隆一份升一次（STS2 的做法）──
{
    var card = ModelRegistry.New<TestMultiUpgrade>();
    card.Upgrade();                                    // 现在是 +1，16 伤害

    var preview = card.MutableCloneAs<CardModel>();    // 克隆
    preview.Upgrade();                                 // 升一次

    Check(preview.Vars.Damage.Int == 21, "预览显示下一级是 21", $"实际 {preview.Vars.Damage.Int}");
    Check(card.Vars.Damage.Int == 16, "原卡不受预览影响", $"实际 {card.Vars.Damage.Int}");
    Check(preview.Vars.Damage.WasJustUpgraded, "预览的数值带『刚变过』标记，供 UI 高亮");
}

// ── CanPlay 的多来源收口 ──
{
    var e = ModelRegistry.New<TestBasicAttack>();
    Check(e.CanPlay(3), "能量够时可以打出");

    bool ok = e.CanPlay(0, out UnplayableReason why, out GameModel? who);
    Check(!ok && why == UnplayableReason.EnergyTooHigh, "能量不够时给出确切原因", why.ToString());
    Check(who == null, "hook 系统还没建，preventer 恒为 null");

    var curse = ModelRegistry.New<TestUnplayableCurse>();
    curse.CanPlay(99, out UnplayableReason curseWhy, out _);
    Check(curseWhy == UnplayableReason.HasUnplayableKeyword,
        "能量充足也打不出：原因是关键词", curseWhy.ToString());
}

// ── 掉落池归属 ──
{
    Check(ModelRegistry.Get<TestBasicAttack>().IsInDropPool, "火系卡进掉落池");
    Check(ModelRegistry.AllOf<CardModel>().Count() == 5, "AllOf<CardModel> 数量正确");
}
Console.WriteLine();
Console.WriteLine("[4] Creature 身体层");

Fx.OnWarn = msg => Console.Error.WriteLine(msg);

// ── ID 派生延伸到新类别 ──
// BUFF/MONSTER 两个 Category 是这批新出现的，钉死一次防手滑。
{
    Check(ModelRegistry.Get<TestVenomBuff>().Id.ToString() == "BUFF.TEST_VENOM_BUFF",
        "buff 的 ID 派生", ModelRegistry.Get<TestVenomBuff>().Id.ToString());
    Check(ModelRegistry.Get<TestDummyMonster>().Id.ToString() == "MONSTER.TEST_DUMMY_MONSTER",
        "怪物的 ID 派生", ModelRegistry.Get<TestDummyMonster>().Id.ToString());
}

// ── 身份三分法 ──
{
    var hero = new Player("测试者", 70);
    Check(hero.Creature.IsPlayer && !hero.Creature.IsMonster && !hero.Creature.IsPet,
        "玩家身体：IsPlayer 且非怪非宠");
    Check(hero.Creature.Side == CombatSide.Player && hero.Creature.CurrentHp == 70,
        "玩家侧、满血入场");

    var pet = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Player, 12)
        { PetOwner = hero };
    Check(pet.IsMonster && pet.IsPet && pet.Side == CombatSide.Player,
        "同伴 = 玩家侧的怪物 + PetOwner 非空");

    bool threw = false;
    try { pet.PetOwner = new Player("篡位者", 1); }
    catch (InvalidOperationException) { threw = true; }
    Check(threw, "PetOwner 一经设置不许改（同 STS2）");
}

// ── 拿 canonical 定义当身体 → 拒绝 ──
{
    bool threw = false;
    try { _ = new Creature(ModelRegistry.Get<TestDummyMonster>(), CombatSide.Enemy, 40); }
    catch (ArgumentException) { threw = true; }
    Check(threw, "canonical 怪物定义不能入场，必须 New<>() 副本");
}

// ── 护盾 ──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    Check(m.GainBlockInternal(10) == 10 && m.Block == 10, "加 10 盾");
    Check(m.GainBlockInternal(2000) == Creature.MaxBlock - 10 && m.Block == Creature.MaxBlock,
        "护盾封顶 999，返回实际加上的量");

    int blocked = m.DamageBlockInternal(300, ValueProp.Move);
    Check(blocked == 300 && m.Block == 699, "盾够时全挡", $"挡 {blocked}，剩 {m.Block}");

    blocked = m.DamageBlockInternal(800, ValueProp.Move);
    Check(blocked == 699 && m.Block == 0, "盾不够时挡掉全部存量", $"挡 {blocked}");

    m.GainBlockInternal(50);
    blocked = m.DamageBlockInternal(30, ValueProp.Unblockable);
    Check(blocked == 0 && m.Block == 50, "Unblockable：一点不挡，盾原封不动");
}

// ── 扣血与溢出（同伴代受回流的地基）──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    var r = m.LoseHpInternal(15, ValueProp.Move);
    Check(r.UnblockedDamage == 15 && m.CurrentHp == 25 && !r.WasTargetKilled, "普通扣血 15");

    r = m.LoseHpInternal(30, ValueProp.Move);
    Check(r.UnblockedDamage == 25 && r.OverkillDamage == 5 && r.WasTargetKilled,
        "打死：25 实扣 + 5 溢出 + 击杀标记", $"实扣{r.UnblockedDamage} 溢出{r.OverkillDamage}");
    Check(m.CurrentHp == 0 && m.IsDead, "血止于 0，不为负");

    r = m.LoseHpInternal(10, ValueProp.Move);
    Check(r.UnblockedDamage == 0 && r.OverkillDamage == 10 && !r.WasTargetKilled,
        "鞭尸：0 实扣、全额溢出、不再触发击杀（同 STS2 的 CurrentHp>0 前置）");

    Check(r.TotalDamage == 10, "TotalDamage = 实扣 + 溢出");
}

// ── 治疗封顶 ──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    m.LoseHpInternal(30, ValueProp.None);
    Check(m.HealInternal(100) == 30 && m.CurrentHp == 40, "治疗封顶在 MaxHp，返回实际回复量");
}

// ── 非法输入守卫（抽查一个，其余同构）──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    bool threw = false;
    try { m.LoseHpInternal(-1, ValueProp.None); }
    catch (ArgumentException) { threw = true; }
    Check(threw, "负数伤害直接抛（守卫在最底层）");
}

// ── buff 生命周期 ──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    var venom = ModelRegistry.New<TestVenomBuff>();
    venom.ApplyInternal(m, 3);
    Check(m.HasBuff<TestVenomBuff>() && m.GetBuffAmount<TestVenomBuff>() == 3,
        "挂 3 层毒，查得到");
    Check(venom.Owner == m, "buff 认得自己的主人");

    venom.ChangeAmountInternal(2);
    Check(m.GetBuffAmount<TestVenomBuff>() == 5, "改层数 +2 → 5");

    venom.RemoveInternal();
    Check(!m.HasBuff<TestVenomBuff>() && venom.Removed, "移除后查不到，Removed 标记置位");

    Check(m.GetBuffAmount<TestMightBuff>() == 0, "没有的 buff 层数按 0 读");

    bool threw = false;
    try { ModelRegistry.Get<TestVenomBuff>().ApplyInternal(m, 1); }
    catch (InvalidOperationException) { threw = true; }
    Check(threw, "canonical buff 定义不能直接挂人（双态规则管到 buff）");
}

// ── Fx：无头空转 + 吞异常 ──
{
    var m = new Creature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    Fx.Backend = null;
    Fx.Sfx("hit"); Fx.Vfx("spark", m); Fx.Anim(m, "hurt");
    Check(Fx.Wait(0.25f).IsCompleted, "无后端：四通道全部空转，Wait 立即完成");

    Fx.Backend = new ExplodingBackend();
    string? warned = null;
    Fx.OnWarn = msg => warned = msg;
    Fx.Sfx("boom");
    Check(warned != null && warned.Contains("Sfx"), "后端炸了被吞掉，只留警告，结算不中断");
    Fx.Backend = null;
    Fx.OnWarn = msg => Console.Error.WriteLine(msg);
}
Console.WriteLine();
Console.WriteLine(failures == 0 ? "全部通过 ✔" : $"{failures} 条断言失败 ✘");
return failures == 0 ? 0 : 1;

/// <summary>专门用来验证"表现层的 bug 不许打断结算"的假后端。</summary>
sealed class ExplodingBackend : Kernel.IFxBackend
{
    public void Sfx(string id) => throw new InvalidOperationException("音效系统炸了");
    public void Vfx(string id, Kernel.Creature on) => throw new InvalidOperationException("特效系统炸了");
    public void Anim(Kernel.Creature who, string animId) => throw new InvalidOperationException("动画系统炸了");
    public System.Threading.Tasks.Task Wait(float seconds) => throw new InvalidOperationException("计时器炸了");
}