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
ModelRegistry.RegisterAllInAssembly(typeof(CardModel).Assembly);   // Kernel 侧内容：4 个真 buff

// ── ID 从类名派生 ──
{
    Check(ModelRegistry.Get<TestBasicAttack>().Id.ToString() == "CARD.TEST_BASIC_ATTACK",
        "ID 从类名机械派生", ModelRegistry.Get<TestBasicAttack>().Id.ToString());
    Check(ModelRegistry.Count == 24, $"注册了 {ModelRegistry.Count} 个内容（应为 24）");
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

// ── 掉落池归属 ──
{
    Check(ModelRegistry.Get<TestBasicAttack>().IsInDropPool, "火系卡进掉落池");
    Check(ModelRegistry.AllOf<CardModel>().Count() == 14, "AllOf<CardModel> 数量正确（5 旧 + 7 打出测试卡+ 2 回合测试卡）");
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
Console.WriteLine("[5] 牌堆与战斗全景");

// ── 战斗组装：名册、发号、同伴 ──
{
    var state = new CombatState();
    var hero = new Player("测试者", 70);
    state.AddPlayer(hero);

    Check(hero.PlayerCombatState != null, "AddPlayer 创建了 PlayerCombatState");
    Check(hero.Creature.CombatState == state, "玩家身体挂进了战斗");
    Check(hero.Creature.CombatId == 1u, "第一个入场者 CombatId=1", $"实际 {hero.Creature.CombatId}");

    var e1 = state.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    var e2 = state.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 42);
    Check(e1.CombatId == 2u && e2.CombatId == 3u, "CombatId 按入场顺序递增");
    Check(state.Allies.Count == 1 && state.Enemies.Count == 2, "两侧名册正确");
    Check(state.GetOpponentsOf(hero.Creature).Count == 2, "对手查询");

    var pet = state.AddPet(ModelRegistry.New<TestDummyMonster>(), hero, 12);
    Check(pet.PetOwner == hero && pet.Side == CombatSide.Player, "同伴入场：主人+阵营");
    Check(hero.Creature.Pets.Count == 1 && hero.Creature.Pets[0] == pet, "Creature.Pets 转发可见");

    bool threw = false;
    try { state.AddPlayer(hero); } catch (InvalidOperationException) { threw = true; }
    Check(threw, "同一玩家不能二次入场");
}

// ── 铁律：每张牌任意时刻恰好在一个堆 ──
{
    var state = new CombatState();
    var hero = new Player("测试者", 70);
    state.AddPlayer(hero);
    var pcs = hero.PlayerCombatState!;

    var card = state.CreateCard<TestBasicAttack>(hero);
    Check(card.Owner == hero && card.Pile == null, "CreateCard 设 Owner，还没进堆");

    pcs.Hand.AddInternal(card);
    Check(card.Pile == pcs.Hand && pcs.Hand.Count == 1, "进手牌，Pile 指回手牌");

    bool threw = false;
    try { pcs.DrawPile.AddInternal(card); } catch (InvalidOperationException) { threw = true; }
    Check(threw, "已在手牌的卡不能再进抽牌堆（铁律执法点在 AddInternal）");

    pcs.Hand.RemoveInternal(card);
    Check(card.Pile == null && pcs.Hand.IsEmpty, "移出后 Pile 归 null");

    pcs.RemovedPile.AddInternal(card);
    Check(pcs.AllCards.Count() == 1, "AllCards（所有权名单）包含移出区的牌——战斗结束凭它归还");
    Check(!CardPile.GetCards(hero, PileType.Draw, PileType.Discard, PileType.Hand).Any(),
        "选牌查询点名要堆，不点 Removed 就选不到——『不能被选中』的实现");

    threw = false;
    try { pcs.Hand.AddInternal(ModelRegistry.Get<TestBasicAttack>()); }
    catch (InvalidOperationException) { threw = true; }
    Check(threw, "canonical 定义不能进牌堆");
}

// ── 洗牌走注入的 Rng ──
{
    var state = new CombatState();
    var hero = new Player("测试者", 70);
    state.AddPlayer(hero);
    for (int i = 0; i < 8; i++)
        hero.PlayerCombatState!.DrawPile.AddInternal(state.CreateCard<TestBasicAttack>(hero));

    var rng = new Rng(2026, "shuffle");
    int before = rng.Counter;
    hero.PlayerCombatState!.DrawPile.ShuffleInternal(rng);
    Check(rng.Counter - before == 7, "洗 8 张消耗恰好 7 次（铁律③延伸到牌堆）",
        $"实际 {rng.Counter - before}");
}

// ── CanPlay：STS2 无参形态 ──
{
    var state = new CombatState();
    var hero = new Player("测试者", 70);
    state.AddPlayer(hero);
    var pcs = hero.PlayerCombatState!;

    var strike = state.CreateCard<TestBasicAttack>(hero);
    pcs.Hand.AddInternal(strike);

    pcs.ResetEnergy();
    Check(pcs.Energy == 3, "回合初能量 = MaxEnergy = 3", $"实际 {pcs.Energy}");
    Check(strike.CanPlay(), "能量够：可以打出");

    pcs.LoseEnergy(3);
    bool ok = strike.CanPlay(out UnplayableReason why, out GameModel? who);
    Check(!ok && why == UnplayableReason.EnergyTooHigh, "能量不够：给出确切原因", why.ToString());
    Check(who == null, "hook 未建，preventer 恒 null");

    var curse = state.CreateCard<TestUnplayableCurse>(hero);
    pcs.Hand.AddInternal(curse);
    pcs.ResetEnergy();
    curse.CanPlay(out UnplayableReason curseWhy, out _);
    Check(curseWhy == UnplayableReason.HasUnplayableKeyword, "0 费也打不出：关键词原因", curseWhy.ToString());

    bool threw = false;
    try { ModelRegistry.New<TestBasicAttack>().CanPlay(); }
    catch (InvalidOperationException) { threw = true; }
    Check(threw, "不在战斗中的卡问 CanPlay 直接抛");
}

// ── 事件流 ──
{
    var state = new CombatState();
    var log = new List<string>();
    state.Events.OnEvent += e => log.Add(e.ToLogLine());

    state.Events.Emit(new TestEvent { Round = 1, Side = CombatSide.Player, Note = "开幕" });
    state.Events.Emit(new TestEvent { Round = 2, Side = CombatSide.Enemy, Note = "反击" });

    Check(state.Events.Entries.Count == 2, "事件入账 2 条");
    Check(log.Count == 2 && log[0] == "Rd 1 (Player): 测试事件：开幕",
        "订阅者逐条收到 + 日志行格式", log.Count > 0 ? log[0] : "(空)");
    Check(state.Events.Entries[0].HappenedThisTurn(state), "Rd1/Player 命中当前回合");
    Check(!state.Events.Entries[1].HappenedThisTurn(state), "Rd2/Enemy 不命中");
}

// ── hook 名单：顺序写死 + Removed 可见（你的设计决定）──
{
    var state = new CombatState();
    var hero = new Player("测试者", 70);
    state.AddPlayer(hero);
    var pcs = hero.PlayerCombatState!;

    var might = ModelRegistry.New<TestMightBuff>();
    might.ApplyInternal(hero.Creature, 2);

    var inHand = state.CreateCard<TestBasicAttack>(hero);
    pcs.Hand.AddInternal(inHand);
    var inRemoved = state.CreateCard<TestShieldSkill>(hero);
    pcs.RemovedPile.AddInternal(inRemoved);

    var enemy = state.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    var venom = ModelRegistry.New<TestVenomBuff>();
    venom.ApplyInternal(enemy, 1);

    var listeners = state.IterateHookListeners().ToList();
    Check(listeners.Contains(inRemoved), "移出区的牌在 hook 名单里（被动仍生效，设计故意）");

    var expected = new GameModel[] { might, inHand, inRemoved, venom, enemy.Monster! };
    Check(listeners.SequenceEqual(expected),
        "名单顺序写死：友方buff → 友方牌(堆序) → 敌方buff → 敌方怪",
        string.Join(" | ", listeners.Select(l => l.Id.Entry)));
}

Console.WriteLine();
Console.WriteLine("[6] Hook 总线与伤害管线");

// 快速搭一场战斗的小工具（局部函数，下面每个块都用）
(CombatState state, Player hero, Creature enemy) NewFight()
{
    var s = new CombatState();
    var h = new Player("测试者", 70);
    s.AddPlayer(h);
    var e = s.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    return (s, h, e);
}

// ── 修正链黄金值：力量 + 灼伤（讨论里那个 8→15 的例子）──
{
    var (s, h, e) = NewFight();
    await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 2, null);
    await BuffCmd.Apply<SearBuff>(s, e, 3, null);
    await BuffCmd.RaiseSearLevel(s, e, 1, null);          // Ⅰ→Ⅱ

    int dmg = Hook.ModifyDamage(s, e, h.Creature, 8m, ValueProp.Move, null, out var mods);
    Check(dmg == 15, "8 基伤 +2力量=10，灼伤Ⅱ ×1.5 → 15", $"实际 {dmg}");
    Check(mods.Count == 2, "out modifiers 记录两位贡献者",
        string.Join(",", mods.Select(m => m.Id.Entry)));
}

// ── 弱化截断：那场 6.75 讨论的立碑 ──
{
    var (s, h, e) = NewFight();
    await BuffCmd.Apply<WeakenBuff>(s, h.Creature, 2, null);
    int dmg = Hook.ModifyDamage(s, e, h.Creature, 9m, ValueProp.Move, null, out _);
    Check(dmg == 6, "9 × 0.75 = 6.75 → 6（全局唯一取整点）", $"实际 {dmg}");
}

// ── 灼伤四规则 ──
{
    var (s, h, e) = NewFight();
    Check(!await BuffCmd.RaiseSearLevel(s, e, 1, null), "提级对没有灼伤的目标无效（规则①）");

    var sear = await BuffCmd.Apply<SearBuff>(s, e, 3, null);
    Check(sear != null && sear.Level == 1 && sear.Amount == 3, "首次施加默认Ⅰ级·3层（规则①）");

    await BuffCmd.Apply<SearBuff>(s, e, 2, null);
    Check(sear!.Amount == 5 && sear.Level == 1, "加层只加层：3+2=5，等级不动（规则①）");

    await BuffCmd.RaiseSearLevel(s, e, 2, null);
    Check(sear.Level == 3, "提级累加：Ⅰ+2=Ⅲ（规则①）");

    await BuffCmd.RaiseSearLevel(s, e, 5, null);
    Check(sear.Level == 4, "封顶Ⅳ（规则①）");

    int dmg = Hook.ModifyDamage(s, e, h.Creature, 8m, ValueProp.Move, null, out _);
    Check(dmg == 16, "灼伤Ⅳ ×2.0：8→16（规则②）", $"实际 {dmg}");

    await BuffCmd.ChangeAmount(s, sear, -5, null);
    Check(sear.Removed && !e.HasBuff<SearBuff>(), "层数归零整条消失，等级随之蒸发（规则④）");
}

// ── 叠层与负力量 ──
{
    var (s, h, e) = NewFight();
    var a = await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 3, null);
    var b = await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 2, null);
    Check(ReferenceEquals(a, b) && a!.Amount == 5, "Counter 合层：3+2=5，同一实例");
    Check(h.Creature.Buffs.Count == 1, "不产生第二个实例");

    await BuffCmd.ChangeAmount(s, a, -7, null);
    Check(a.Amount == -2 && !a.Removed, "力量可负且负数不移除（AllowNegative）");

    int dmg = Hook.ModifyDamage(s, e, h.Creature, 8m, ValueProp.Move, null, out _);
    Check(dmg == 6, "负力量参与结算：8-2=6", $"实际 {dmg}");

    await BuffCmd.ChangeAmount(s, a, 2, null);
    Check(a.Removed && !h.Creature.HasBuff<StrengthBuff>(), "可负 buff 恰好归零才移除（STS2 411 行规则）");
}

// ── ValueProp 语义 ──
{
    var (s, h, e) = NewFight();
    await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 2, null);
    await BuffCmd.Apply<SearBuff>(s, e, 3, null);

    int dot = Hook.ModifyDamage(s, e, h.Creature, 5m, ValueProp.None, null, out _);
    Check(dot == 5, "不带 Move（中毒类直伤）：力量、灼伤都不参与");

    int thorns = Hook.ModifyDamage(s, e, h.Creature, 4m, ValueProp.Move | ValueProp.Unpowered, null, out _);
    Check(thorns == 4, "Unpowered（荆棘类）：同样全不参与");
}

// ── 完整管线：破盾 ──
{
    var (s, h, e) = NewFight();
    e.GainBlockInternal(10);
    await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 2, null);
    await BuffCmd.Apply<SearBuff>(s, e, 3, null);
    await BuffCmd.RaiseSearLevel(s, e, 1, null);

    var rs = await CreatureCmd.Damage(s, h.Creature, new[] { e }, 8, ValueProp.Move, null);
    Check(rs.Count == 1 && rs[0].BlockedDamage == 10 && rs[0].UnblockedDamage == 5 && e.CurrentHp == 35,
        "15 伤：破 10 盾 + 掉 5 血", $"挡{rs[0].BlockedDamage} 扣{rs[0].UnblockedDamage} hp{e.CurrentHp}");
    Check(rs[0].WasBlockBroken && !rs[0].WasFullyBlocked, "破盾标记正确");
    Check(s.Events.Entries.OfType<DamageReceived>().Count() == 1, "一条 DamageReceived 事件");
}

// ── Unblockable 走管线 ──
{
    var (s, h, e) = NewFight();
    e.GainBlockInternal(10);
    await CreatureCmd.Damage(s, h.Creature, new[] { e }, 6, ValueProp.Move | ValueProp.Unblockable, null);
    Check(e.Block == 10 && e.CurrentHp == 34, "Unblockable：无视护盾直击血量");
}

// ── 舍身代受 + 溢出回流（本批的主菜）──
{
    var (s, h, e) = NewFight();
    var pet = s.AddPet(ModelRegistry.New<TestDummyMonster>(), h, 12);
    await BuffCmd.Apply<GuardianBuff>(s, pet, 1, null);
    h.Creature.GainBlockInternal(6);

    var rs = await CreatureCmd.Damage(s, e, new[] { h.Creature }, 20, ValueProp.Move, null);
    // 20 → 主人的盾挡 6 → 14 → 舍身转给同伴 → 同伴 12 血：实扣 12、溢出 2、阵亡
    //    → 溢出 2 回流主人
    Check(rs.Count == 2, "一次打击产生两条结果单（同伴 + 回流）");
    Check(rs[0].Receiver == pet && rs[0].UnblockedDamage == 12
          && rs[0].OverkillDamage == 2 && rs[0].WasTargetKilled,
        "同伴代受：实扣 12 + 溢出 2 + 阵亡", $"扣{rs[0].UnblockedDamage} 溢{rs[0].OverkillDamage}");
    Check(rs[1].Receiver == h.Creature && rs[1].UnblockedDamage == 2 && h.Creature.CurrentHp == 68,
        "溢出 2 回流主人：70→68", $"hp {h.Creature.CurrentHp}");
    Check(rs[1].BlockedDamage == 6 && rs[0].BlockedDamage == 0,
        "护盾三项记在主人那条上——盾是主人的（同 STS2）");
    Check(pet.IsDead && s.Allies.Count == 1, "同伴阵亡移出名册");
    Check(h.PlayerCombatState!.Pets.Count == 1, "尸体仍在同伴名单里（Step C 复活的前提）");
    Check(s.Events.Entries.OfType<CreatureDied>().Count() == 1, "一条 CreatureDied");

    var rs2 = await CreatureCmd.Damage(s, e, new[] { h.Creature }, 5, ValueProp.Move, null);
    Check(rs2.Count == 1 && rs2[0].Receiver == h.Creature && h.Creature.CurrentHp == 63,
        "同伴死后不再代受（舍身的 Owner.IsDead 检查）");
}

// ── ShouldDie 一票否决 ──
{
    var (s, h, e) = NewFight();
    var undying = ModelRegistry.New<TestUndyingBuff>();
    undying.ApplyInternal(e, 1);

    await CreatureCmd.Damage(s, h.Creature, new[] { e }, 99, ValueProp.Move, null);
    Check(e.CurrentHp == 1 && s.Enemies.Count == 1, "死亡被否决：回 1 血留场", $"hp{e.CurrentHp}");
    Check(undying.TimesPrevented == 1, "AfterPreventingDeath 只通知否决者");
    Check(!s.Events.Entries.OfType<CreatureDied>().Any(), "没有死亡事件");
}

// ── GainBlock 走 Cmd ──
{
    var (s, h, _) = NewFight();
    int gained = await CreatureCmd.GainBlock(s, h.Creature, 12, ValueProp.Move, null);
    Check(gained == 12 && h.Creature.Block == 12, "GainBlock：hook 链 + Internal + 事件");
    Check(s.Events.Entries.OfType<BlockGained>().Count() == 1, "BlockGained 事件一条");
}

// ── 确定性：同一脚本两跑，事件日志逐字节相同 ──
{
    async Task<string> RunOnce()
    {
        var (s, h, e) = NewFight();
        var log = new List<string>();
        s.Events.OnEvent += ev => log.Add(ev.ToLogLine());
        await BuffCmd.Apply<SearBuff>(s, e, 2, null);
        await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 1, null);
        await CreatureCmd.Damage(s, h.Creature, new[] { e }, 8, ValueProp.Move, null);
        await CreatureCmd.GainBlock(s, h.Creature, 5, ValueProp.Move, null);
        return string.Join("\n", log);
    }
    Check(await RunOnce() == await RunOnce(), "事件日志逐字节可复现（里程碑雏形）");
}

Console.WriteLine();
Console.WriteLine("[7] 打出流程");

// 带随机源的战斗搭建（[7] 专用）
(CombatState state, Player hero, Creature enemy) NewFightR(string seed)
{
    var s = new CombatState(new RngSet(seed));
    var h = new Player("测试者", 70);
    s.AddPlayer(h);
    var e = s.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);
    h.PlayerCombatState!.ResetEnergy();
    return (s, h, e);
}

// ── 基本打出：管线全通 ──
{
    var (s, h, e) = NewFightR("play-basic");
    var pcs = h.PlayerCombatState!;
    var strike = s.CreateCard<TestStrikePlay>(h);
    pcs.Hand.AddInternal(strike);
    await BuffCmd.Apply<StrengthBuff>(s, h.Creature, 2, null);

    bool ok = await CardCmd.Play(s, strike, e);
    Check(ok && e.CurrentHp == 32, "打出：6+2力量=8 伤", $"hp {e.CurrentHp}");
    Check(pcs.Energy == 2, "扣了 1 费", $"实际 {pcs.Energy}");
    Check(strike.Pile == pcs.DiscardPile, "攻击卡落入弃牌堆");
    Check(s.Events.Entries.OfType<CardPlayStarted>().Count() == 1
          && s.Events.Entries.OfType<CardPlayFinished>().Count() == 1, "开打/打完两事件各一");
}

// ── 打不出去的三种拒绝 ──
{
    var (s, h, e) = NewFightR("play-reject");
    var pcs = h.PlayerCombatState!;
    var strike = s.CreateCard<TestStrikePlay>(h);
    pcs.Hand.AddInternal(strike);

    pcs.LoseEnergy(3);
    Check(!await CardCmd.Play(s, strike, e), "能量不足 → false，卡留在手牌");
    Check(strike.Pile == pcs.Hand, "没动");

    pcs.ResetEnergy();
    e.LoseHpInternal(999, ValueProp.None);
    Check(!await CardCmd.Play(s, strike, e), "目标已死 → false");

    var drawPileCard = s.CreateCard<TestDefendPlay>(h);
    pcs.DrawPile.AddInternal(drawPileCard);
    Check(!await CardCmd.Play(s, drawPileCard, null), "不在手牌 → false（主动打出只能从手牌）");
}

// ── 消耗关键词 + 燃料 ──
{
    var (s, h, _) = NewFightR("fuel");
    var pcs = h.PlayerCombatState!;
    var fuel = s.CreateCard<TestFuelBlock>(h);
    pcs.Hand.AddInternal(fuel);
    var filler = s.CreateCard<TestDefendPlay>(h);
    pcs.Hand.AddInternal(filler);                      // 占位，避免触发虚无

    await CardCmd.Play(s, fuel, null);
    Check(fuel.Pile == pcs.ExhaustPile, "自带消耗：打完进消耗堆");
    Check(h.Creature.Block == 3, "燃料触发（途径①：打出自耗）", $"盾 {h.Creature.Block}");
    Check(s.Events.Entries.OfType<CardExhausted>().Count() == 1, "CardExhausted 事件");

    var fuel2 = s.CreateCard<TestFuelBlock>(h);
    pcs.Hand.AddInternal(fuel2);
    await CardPileCmd.Exhaust(s, fuel2);
    Check(h.Creature.Block == 6, "燃料触发（途径②：被效果消耗）", $"盾 {h.Creature.Block}");
}

// ── Aura → Removed（偏离 #6 的落地）──
{
    var (s, h, e) = NewFightR("aura");
    var pcs = h.PlayerCombatState!;
    var aura = s.CreateCard<TestAuraMight>(h);
    pcs.Hand.AddInternal(aura);
    var strike = s.CreateCard<TestStrikePlay>(h);
    pcs.Hand.AddInternal(strike);

    await CardCmd.Play(s, aura, null);
    Check(aura.Pile == pcs.RemovedPile, "Aura 打完进 Removed 堆");
    Check(!s.Events.Entries.OfType<CardExhausted>().Any(), "不是消耗——燃料/消耗事件都没有");

    await CardCmd.Play(s, strike, e);
    Check(e.CurrentHp == 33, "躺在 Removed 的永续卡持续生效：6+1=7 伤", $"hp {e.CurrentHp}");
}

// ── 抽牌：上限 / 洗回 ──
{
    var (s, h, _) = NewFightR("draw-rules");
    var pcs = h.PlayerCombatState!;
    for (int i = 0; i < 2; i++) pcs.DrawPile.AddInternal(s.CreateCard<TestDefendPlay>(h));
    for (int i = 0; i < 3; i++) pcs.DiscardPile.AddInternal(s.CreateCard<TestStrikePlay>(h));

    var got = await CardPileCmd.Draw(s, h, 4);
    Check(got.Count == 4 && pcs.Hand.Count == 4, "抽 4：抽穿抽牌堆后洗回再抽", $"手 {pcs.Hand.Count}");
    Check(s.Events.Entries.OfType<PilesReshuffled>().Count() == 1, "洗回事件一次");
    Check(pcs.DrawPile.Count == 1 && pcs.DiscardPile.IsEmpty, "洗回后余 1 在抽牌堆");

    for (int i = 0; i < 6; i++) pcs.Hand.AddInternal(s.CreateCard<TestDefendPlay>(h));   // 手牌到 10
    int before = pcs.DrawPile.Count;
    Check((await CardPileCmd.Draw(s, h, 1)).Count == 0 && pcs.DrawPile.Count == before,
        "满 10 不抽，牌留在抽牌堆");
}

// ── 生成：满手改道弃牌堆 ──
{
    var (s, h, _) = NewFightR("generate");
    var pcs = h.PlayerCombatState!;
    for (int i = 0; i < 10; i++) pcs.Hand.AddInternal(s.CreateCard<TestDefendPlay>(h));

    var extra = s.CreateCard<TestStrikePlay>(h);
    await CardPileCmd.AddGenerated(s, extra, PileType.Hand);
    Check(extra.Pile == pcs.DiscardPile, "生成进满手 → 改道弃牌堆（STS2 实证规则）");
    Check(s.Events.Entries.OfType<CardGenerated>().Single().To == PileType.Discard,
        "CardGenerated 记录的是实际去向");
}

// ── 遗言：效果弃牌 → 免费打出自己 ──
{
    var (s, h, _) = NewFightR("epitaph");
    var pcs = h.PlayerCombatState!;
    var epitaph = s.CreateCard<TestEpitaphDraw>(h);
    pcs.Hand.AddInternal(epitaph);
    var filler = s.CreateCard<TestDefendPlay>(h);
    pcs.Hand.AddInternal(filler);
    pcs.DrawPile.AddInternal(s.CreateCard<TestStrikePlay>(h));
    int energyBefore = pcs.Energy;

    await CardCmd.Discard(s, epitaph);
    Check(s.Events.Entries.OfType<CardDiscarded>().Count() == 1, "弃牌事件");
    Check(s.Events.Entries.OfType<CardPlayStarted>().Single().IsAutoPlay, "遗言自动打出（IsAutoPlay）");
    Check(pcs.Hand.Count == 2, "遗言效果执行了：抽回 1 张（手上 filler+新抽）", $"手 {pcs.Hand.Count}");
    Check(pcs.Energy == energyBefore, "免费——能量分文未动");
    Check(epitaph.Pile == pcs.DiscardPile, "打完回弃牌堆");
}

// ── 虚无：你举的原例 + 同回合二次触发 ──
{
    var (s, h, e) = NewFightR("nihility");
    var pcs = h.PlayerCombatState!;
    var nihility = s.CreateCard<TestNihilityExhauster>(h);
    pcs.Hand.AddInternal(nihility);
    var other = s.CreateCard<TestStrikePlay>(h);
    pcs.Hand.AddInternal(other);                                  // 手牌恰两张——原例场景
    pcs.DrawPile.AddInternal(s.CreateCard<TestStrikePlay>(h));
    pcs.DrawPile.AddInternal(s.CreateCard<TestDefendPlay>(h));

    await CardCmd.Play(s, nihility, null);
    // 打出（手1）→ 效果消耗 other（手0）→ 检查点：空&已武装 → 虚无：抽1（手1）
    Check(s.Events.Entries.OfType<NihilityTriggered>().Count() == 1, "虚无触发一次（原例）");
    Check(pcs.Hand.Count == 1, "冒号效果抽回 1 张", $"手 {pcs.Hand.Count}");
    Check(other.Pile == pcs.ExhaustPile && nihility.Pile == pcs.ExhaustPile, "两张都进了消耗堆");

    var drawn = pcs.Hand.Cards[0];
    bool played = drawn is TestStrikePlay
        ? await CardCmd.Play(s, drawn, e)
        : await CardCmd.Play(s, drawn, null);
    // 打出手里唯一一张 → 手牌再度变空 → 边沿已重新武装 → 第二次触发（"每次都触发"）
    Check(played && s.Events.Entries.OfType<NihilityTriggered>().Count() == 2,
        "同回合第二次变空，再次触发");
}

// ── 随机源未接线的清晰报错 ──
{
    var s = new CombatState();
    bool threw = false;
    try { _ = s.RngSet; }
    catch (InvalidOperationException ex) when (ex.Message.Contains("随机源")) { threw = true; }
    Check(threw, "没接 RngSet 时报人话错误，而不是 NRE");
}

// ── 全流程确定性：同种子两跑，日志逐字节相同 ──
{
    async Task<string> RunOnce()
    {
        var (s, h, e) = NewFightR("determinism-d");
        var log = new List<string>();
        s.Events.OnEvent += ev => log.Add(ev.ToLogLine());
        var pcs = h.PlayerCombatState!;
        for (int i = 0; i < 5; i++) pcs.DiscardPile.AddInternal(s.CreateCard<TestStrikePlay>(h));
        for (int i = 0; i < 2; i++) pcs.DrawPile.AddInternal(s.CreateCard<TestDrawTwo>(h));
        await CardPileCmd.Draw(s, h, 4);                       // 会触发洗回 → 消耗 Shuffle 流
        var target = s.Enemies[0];
        foreach (var c in pcs.Hand.Cards.ToList())
            if (pcs.Energy > 0)
                await CardCmd.Play(s, c, c.TargetType == TargetType.SingleEnemy ? target : null);
        return string.Join("\n", log);
    }
    Check(await RunOnce() == await RunOnce(), "抽-洗-打全流程日志逐字节可复现");
}

Console.WriteLine();
Console.WriteLine("[8] 回合循环与里程碑");

// ── 开场与首回合 ──
{
    var s = new CombatState(new RngSet("turn-basic"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    var pcs = hero.PlayerCombatState!;
    for (int i = 0; i < 8; i++) hero.Deck.AddInternal(ModelRegistry.New<TestStrikePlay>());
    hero.Deck.AddInternal(ModelRegistry.New<TestInnateRetainCard>());

    var brute = CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());
    Check(brute.CurrentHp >= 26 && brute.CurrentHp <= 30, "血量在 [26,30] 掷定（MonsterHp 流）", $"hp {brute.CurrentHp}");

    await CombatCmd.StartCombat(s, hero);
    Check(pcs.Hand.Count == 5 && pcs.Energy == 3, "开场：抽 5、能量 3", $"手{pcs.Hand.Count} 能{pcs.Energy}");
    Check(pcs.Hand.Cards.Any(c => c is TestInnateRetainCard), "本能卡首回合必上手（置顶）");
    Check(s.Events.Entries.OfType<MonsterIntentRolled>().Count() == 1 && brute.Monster!.NextMove != null,
        "意图在玩家回合开始时已声明");
    Check(hero.Deck.Cards.Count == 9, "牌库本体未动——进战斗的是复制品");
    Check(!s.Events.Entries.OfType<BlockCleared>().Any(), "首回合玩家侧不清盾（豁免）");
}

// ── 回合末：临时 / 保留 / Flush 三线 ──
{
    var s = new CombatState(new RngSet("turn-end"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    var pcs = hero.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());

    var temp = s.CreateCard<TestTemporaryCard>(hero);   pcs.Hand.AddInternal(temp);
    var keep = s.CreateCard<TestInnateRetainCard>(hero); pcs.Hand.AddInternal(keep);
    var epit = s.CreateCard<TestEpitaphDraw>(hero);      pcs.Hand.AddInternal(epit);
    var norm = s.CreateCard<TestStrikePlay>(hero);       pcs.Hand.AddInternal(norm);
    int playsBefore = s.Events.Entries.OfType<CardPlayStarted>().Count();

    await CombatCmd.EndPlayerTurn(s, hero);
    Check(temp.Pile == pcs.ExhaustPile, "临时 → 回合末被消耗");
    Check(keep.Pile == pcs.Hand, "保留 → 留在手上");
    Check(norm.Pile == pcs.DiscardPile && epit.Pile == pcs.DiscardPile, "其余被 Flush 进弃牌堆");
    Check(!s.Events.Entries.OfType<CardDiscarded>().Any(), "Flush 不算弃牌：零 CardDiscarded（双动词纪律）");
    Check(s.Events.Entries.OfType<CardPlayStarted>().Count() == playsBefore, "遗言未被 Flush 触发");
    Check(s.Events.Entries.OfType<CardsFlushed>().Single().Count == 2, "CardsFlushed 记 2 张");
}

// ── 清盾时点 ──
{
    var s = new CombatState(new RngSet("block-clear"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());
    await CombatCmd.StartCombat(s, hero);
    await CombatCmd.EndPlayerTurn(s, hero);
    await CombatCmd.EnemyTurn(s);

    hero.Creature.GainBlockInternal(7);                       // 敌方行动后补的盾
    await CombatCmd.StartPlayerTurn(s, hero);                 // 第 2 回合开始
    Check(hero.Creature.Block == 0, "第 2 回合起玩家清盾");
    Check(s.Events.Entries.OfType<BlockCleared>().Any(e => e.Target == hero.Creature && e.Amount == 7),
        "BlockCleared 事件带清掉的量");
}

// ── 灼伤③④ / 弱化 tick ──
{
    var s = new CombatState(new RngSet("tick"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    var brute = CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());
    await CombatCmd.StartCombat(s, hero);

    var sear = await BuffCmd.Apply<SearBuff>(s, brute, 5, null);
    await BuffCmd.RaiseSearLevel(s, brute, 1, null);          // Ⅱ级 5 层
    await BuffCmd.Apply<WeakenBuff>(s, hero.Creature, 2, null);

    async Task Cycle() { await CombatCmd.EndPlayerTurn(s, hero); await CombatCmd.EnemyTurn(s); await CombatCmd.StartPlayerTurn(s, hero); }

    await Cycle();
    Check(sear!.Amount == 3, "灼伤Ⅱ tick：5-2=3（规则③）", $"实际 {sear.Amount}");
    Check(hero.Creature.GetBuffAmount<WeakenBuff>() == 1, "弱化 -1");

    await Cycle();
    Check(sear.Amount == 1, "再 tick：3-2=1");

    await Cycle();
    Check(sear.Removed && !brute.HasBuff<SearBuff>(), "1-2→0：整条消失，等级蒸发（规则④）");
    Check(!hero.Creature.HasBuff<WeakenBuff>(), "弱化归零移除");
}

// ── ModifyHandDraw（长蛇戒指位）──
{
    var s = new CombatState(new RngSet("hand-draw"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());
    for (int i = 0; i < 12; i++) hero.Deck.AddInternal(ModelRegistry.New<TestStrikePlay>());
    await CombatCmd.StartCombat(s, hero);

    var drawBuff = ModelRegistry.New<TestHandDrawBuff>();
    drawBuff.ApplyInternal(hero.Creature, 1);
    await CombatCmd.EndPlayerTurn(s, hero);
    await CombatCmd.EnemyTurn(s);
    await CombatCmd.StartPlayerTurn(s, hero);
    Check(hero.PlayerCombatState!.Hand.Count == 6, "基数5 经 hook +1 → 抽6", $"实际 {hero.PlayerCombatState!.Hand.Count}");
}

// ── 【里程碑】无头整场战斗 ──
{
    async Task<(string log, bool victory, int rounds)> RunFull(string seed)
    {
        var s = new CombatState(new RngSet(seed));
        var hero = new Player("测试者", 70);
        s.AddPlayer(hero);
        for (int i = 0; i < 6; i++) hero.Deck.AddInternal(ModelRegistry.New<TestStrikePlay>());
        for (int i = 0; i < 3; i++) hero.Deck.AddInternal(ModelRegistry.New<TestDefendPlay>());
        hero.Deck.AddInternal(ModelRegistry.New<TestDrawTwo>());
        CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());
        CombatCmd.SpawnEnemy(s, ModelRegistry.New<TestBruteMonster>());

        var log = new List<string>();
        s.Events.OnEvent += ev => log.Add(ev.ToLogLine());
        await CombatCmd.StartCombat(s, hero);

        while (!s.IsOver && s.RoundNumber <= 30)
        {
            for (int plays = 0; plays < 13 && !s.IsOver; plays++)      // 耳环同款策略 + 官方防呆上限
            {
                var pcs = hero.PlayerCombatState!;
                CardModel? card = pcs.Hand.Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;
                Creature? target = card.TargetType == TargetType.SingleEnemy
                    ? s.Enemies.FirstOrDefault(e => e.IsAlive) : null;
                if (card.TargetType == TargetType.SingleEnemy && target == null) break;
                await CardCmd.Play(s, card, target);
                CombatCmd.CheckEnd(s);
            }
            if (s.IsOver) break;
            await CombatCmd.EndPlayerTurn(s, hero);
            await CombatCmd.EnemyTurn(s);
            if (!s.IsOver) await CombatCmd.StartPlayerTurn(s, hero);
        }
        return (string.Join("\n", log), s.Victory, s.RoundNumber);
    }

    var (log1, win1, rounds1) = await RunFull("MILESTONE");
    Check(win1 && rounds1 <= 30, $"整场自动打完并获胜（第 {rounds1} 回合）");
    var (log2, win2, _) = await RunFull("MILESTONE");
    Check(win2 && log1 == log2, "【里程碑】同种子完整两跑，事件日志逐字节相同");
    var (log3, _, _) = await RunFull("MILESTONE-B");
    Check(log1 != log3, "换种子日志不同——随机源真在参与");
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

/// <summary>事件流测试用的最小事件（真实事件类型随 Cmd 层到来）。</summary>
sealed class TestEvent : Kernel.CombatEvent
{
    public string Note { get; init; } = "";
    public override string Description => $"测试事件：{Note}";
}