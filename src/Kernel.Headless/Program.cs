using Kernel;
using TestContent;
using Kernel.Content.Buffs;
using Kernel.Content.Cards;
using Kernel.Content.Monsters;
using Kernel.Content.Familiars;
using Kernel.Content.Pledges;

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
ModelRegistry.RegisterAllInAssembly(typeof(CardModel).Assembly);   // Kernel 侧内容：4 真 buff + Content/ 真内容

// ── ID 从类名派生 ──
{
    Check(ModelRegistry.Get<TestBasicAttack>().Id.ToString() == "CARD.TEST_BASIC_ATTACK",
        "ID 从类名机械派生", ModelRegistry.Get<TestBasicAttack>().Id.ToString());
    Check(ModelRegistry.Count == 119, $"注册了 {ModelRegistry.Count} 个内容（应为 119）");
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
    Check(ModelRegistry.AllOf<CardModel>().Count() == 83, "AllOf<CardModel> 数量正确（5 旧 + 11 打出测试卡 + 2 回合测试卡 + 65 真卡）");
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
    Check(await BuffCmd.RaiseSearLevel(s, h.Creature, 1, null)
          && h.Creature.GetBuff<SearBuff>() is { Amount: 1, Level: 1 },
        "【裁定5修订】提级对无灼伤目标=生成Ⅰ级·1层（原'无效'作废）");

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
    var strength = a ?? throw new InvalidOperationException("StrengthBuff 应用失败");
    Check(ReferenceEquals(strength, b) && strength.Amount == 5, "Counter 合层：3+2=5，同一实例");
    Check(h.Creature.Buffs.Count == 1, "不产生第二个实例");

    await BuffCmd.ChangeAmount(s, strength, -7, null);
    Check(strength.Amount == -2 && !strength.Removed, "力量可负且负数不移除（AllowNegative）");

    int dmg = Hook.ModifyDamage(s, e, h.Creature, 8m, ValueProp.Move, null, out _);
    Check(dmg == 6, "负力量参与结算：8-2=6", $"实际 {dmg}");

    await BuffCmd.ChangeAmount(s, strength, 2, null);
    Check(strength.Removed && !h.Creature.HasBuff<StrengthBuff>(), "可负 buff 恰好归零才移除（STS2 411 行规则）");
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

// ── Aura → limbo(偏离 #6 翻案:同 STS2 Power——出场即离开牌堆宇宙)──
{
    var (s, h, e) = NewFightR("aura");
    var pcs = h.PlayerCombatState!;
    var aura = s.CreateCard<TestAuraMight>(h);
    pcs.Hand.AddInternal(aura);
    var strike = s.CreateCard<TestStrikePlay>(h);
    pcs.Hand.AddInternal(strike);
    await CardCmd.Play(s, aura, null);
    Check(aura.Pile == null, "Aura 打完不进任何堆(limbo,同 STS2 Power)");
    Check(!pcs.AllCards.Contains(aura), "所有权名单也不含它(战斗内副本,牌库本体无损)");
    Check(!s.Events.Entries.OfType<CardExhausted>().Any(), "不是消耗——燃料/消耗事件都没有");
    await CardCmd.Play(s, strike, e);
    Check(e.CurrentHp == 34, "卡钩随堆籍一起失效:素 6 伤(被动必须活在 buff 里,见 Auras.cs)", $"hp {e.CurrentHp}");
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
    // 打出（手1）→ 效果消耗 other（手0）→ 检查点：空 → 虚无：抽1（手1）
    Check(s.Events.Entries.OfType<NihilityTriggered>().Count() == 1, "虚无触发一次（原例）");
    Check(pcs.Hand.Count == 1, "冒号效果抽回 1 张", $"手 {pcs.Hand.Count}");
    Check(other.Pile == pcs.ExhaustPile && nihility.Pile == pcs.ExhaustPile, "两张都进了消耗堆");

    var drawn = pcs.Hand.Cards[0];
    bool played = drawn is TestStrikePlay
        ? await CardCmd.Play(s, drawn, e)
        : await CardCmd.Play(s, drawn, null);
    // 打出手里唯一一张 → 手牌再度变空 → 无状态检查点 → 第二次触发（"逐笔交易结账"）
    Check(played && s.Events.Entries.OfType<NihilityTriggered>().Count() == 2,
        "同回合第二次变空，再次触发");
}

// ── 三案联测·情况2(裁定15:无状态检查点——每笔空手结账各领各的) ──
{
    var (s, h, e) = NewFightR("vow-case2");
    var pcs = h.PlayerCombatState!;
    var a = s.CreateCard<TestVowEnergy>(h); pcs.Hand.AddInternal(a);
    var b = s.CreateCard<TestVowShield>(h); pcs.Hand.AddInternal(b);
    var c = s.CreateCard<TestDiscardTwoEnergy>(h); pcs.Hand.AddInternal(c);
    int h0 = e.CurrentHp;

    await CardCmd.Play(s, c, null);
    // 能量账:3−1(C)+1(A虚无)+2(C正文)=5;链路:弃A弃B(手空)→遗言A(1伤,检查点:空→+1能)→遗言B(2伤,检查点:空→+1盾)→C正文+2能→C检查点:空→C无冒号
    Check(pcs.Energy == 5, "情况2能量账:3-1+1+2=5(A 的虚无兑现)", $"能量 {pcs.Energy}");
    Check(h.Creature.Block == 1, "B 的虚无也兑现:+1 盾——无状态检查点,每笔空手结账各领各的(武装已撤编)");
    Check(h0 - e.CurrentHp == 3, "遗言双打:1+2=3(裁定14 随机靶=独苗)", $"{h0 - e.CurrentHp}");
    Check(a.Pile == pcs.ExhaustPile && b.Pile == pcs.ExhaustPile && c.Pile == pcs.DiscardPile,
        "归宿:A/B 消耗,C 弃牌堆");
    Check(s.Events.Entries.OfType<NihilityTriggered>().Count() == 3,
        "三笔交易各自空手结账 → 事件三条(旧武装版只有一条,此断言钉死撤编)");
}

// ── 三案联测·情况3(C 自己带冒号:排队最末,照样领到) ──
{
    var (s, h, e) = NewFightR("vow-case3");
    var pcs = h.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<TestDefendPlay>(h));
    var a = s.CreateCard<TestVowEnergy>(h); pcs.Hand.AddInternal(a);
    var b = s.CreateCard<TestVowShield>(h); pcs.Hand.AddInternal(b);
    var c = s.CreateCard<TestDiscardTwoVowDraw>(h); pcs.Hand.AddInternal(c);

    await CardCmd.Play(s, c, null);
    // 链路同上,尾声不同:C 检查点空手 → C 的冒号:抽1(手 0→1)
    Check(pcs.Energy == 3 && h.Creature.Block == 1, "A 能量、B 护盾照旧", $"能量 {pcs.Energy} 盾 {h.Creature.Block}");
    Check(pcs.Hand.Count == 1, "C 自己的虚无:抽 1——冒号在自家检查点兑现", $"手 {pcs.Hand.Count}");
    Check(s.Events.Entries.OfType<NihilityTriggered>().Count() == 3, "仍是三笔三响");
}

// ── 裁定14:全场无活敌,遗言落空直送去向堆 ──
{
    var (s, h, e) = NewFightR("whiff");
    var pcs = h.PlayerCombatState!;
    e.LoseHpInternal(e.CurrentHp, ValueProp.None);            // 独苗敌人裸死(不走 CheckEnd,战斗未终局)
    var a = s.CreateCard<TestVowEnergy>(h); pcs.Hand.AddInternal(a);
    var filler = s.CreateCard<TestDefendPlay>(h); pcs.Hand.AddInternal(filler);
    int energy0 = pcs.Energy;

    await CardCmd.Discard(s, a);                              // 弃 → 遗言自动打出 → 无活敌
    Check(a.Pile == pcs.ExhaustPile, "落空归位:不结算,按消耗词条直送消耗堆(MoveToResultPileWithoutPlaying 同款)");
    Check(!s.Events.Entries.OfType<CardPlayStarted>().Any(ev => ev.IsAutoPlay), "确实没打出:零自动打出事件");
    Check(pcs.Energy == energy0 && e.CurrentHp == 0, "无伤无费:落空是干净的落空");
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
Console.WriteLine("[9] 同伴召唤全流程");

// ── 首次召唤：创建 + 自动舍身 ──
{
    var s = new CombatState(new RngSet("summon-basic"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    s.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    Check(hero.IsPetMissing && hero.Pet == null, "召唤前：无同伴");

    var pet = await PetCmd.Summon<TestPetMonster>(s, hero, 5, null);
    Check(pet != null && pet.MaxHp == 5 && pet.CurrentHp == 5, "召唤5 → 5/5", $"{pet?.CurrentHp}/{pet?.MaxHp}");
    Check(hero.IsPetAlive && hero.Pet == pet, "Player.Pet 三件套就位");
    Check(pet!.PetOwner == hero && pet.Side == CombatSide.Player && s.Allies.Count == 2, "玩家侧入册");
    Check(pet.HasBuff<GuardianBuff>(), "舍身自动挂载（仅首次创建）");
    Check(!s.Events.Entries.OfType<PetSummoned>().Single().WasRevive, "PetSummoned：非复活");
}

// ── 活着再召唤：上限与当前同加 ──
{
    var s = new CombatState(new RngSet("summon-grow"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);

    var pet = await PetCmd.Summon<TestPetMonster>(s, hero, 4, null);
    pet!.LoseHpInternal(2, ValueProp.None);                       // 4/4 → 2/4
    await PetCmd.Summon<TestPetMonster>(s, hero, 3, null);
    Check(pet.MaxHp == 7 && pet.CurrentHp == 5, "带伤 2/4 召唤3 → 5/7（同加）", $"{pet.CurrentHp}/{pet.MaxHp}");
    Check(s.Allies.Count == 2 && pet.Buffs.Count == 1, "没有第二只，也没有第二层舍身");
}

// ── 代受阵亡 → 尸体档案 → 复活（偏离#8 的完整验收）──
{
    var s = new CombatState(new RngSet("summon-revive"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    var enemy = s.CreateCreature(ModelRegistry.New<TestDummyMonster>(), CombatSide.Enemy, 40);

    var pet = await PetCmd.Summon<TestPetMonster>(s, hero, 6, null);
    uint petId = pet!.CombatId!.Value;

    await CreatureCmd.Damage(s, enemy, new[] { hero.Creature }, 10, ValueProp.Move, null);
    Check(pet.IsDead && hero.Creature.CurrentHp == 66, "代受阵亡：实扣6+溢出4回流 70→66", $"hp {hero.Creature.CurrentHp}");
    Check(s.Allies.Count == 1 && hero.PlayerCombatState!.Pets.Count == 1, "尸体出名册、留档案");
    Check(hero.IsPetMissing && hero.Pet == pet, "IsPetMissing=true，Pet 仍指向尸体");
    Check(pet.HasBuff<GuardianBuff>(), "舍身留在尸体上");

    await CreatureCmd.Damage(s, enemy, new[] { hero.Creature }, 5, ValueProp.Move, null);
    Check(hero.Creature.CurrentHp == 61, "尸体出名册=舍身停听：伤害直达主人", $"hp {hero.Creature.CurrentHp}");

    var revived = await PetCmd.Summon<TestPetMonster>(s, hero, 4, null);
    Check(ReferenceEquals(revived, pet) && pet.CombatId == petId, "复活=同一对象、同 CombatId");
    Check(pet.IsAlive && pet.MaxHp == 4 && pet.CurrentHp == 4, "复活血量=召唤量 4/4", $"{pet.CurrentHp}/{pet.MaxHp}");
    Check(s.Allies.Count == 2, "重入名册");
    Check(s.Events.Entries.OfType<PetSummoned>().Count(e => e.WasRevive) == 1, "复活事件标记");

    await CreatureCmd.Damage(s, enemy, new[] { hero.Creature }, 3, ValueProp.Move, null);
    Check(pet.CurrentHp == 1 && hero.Creature.CurrentHp == 61, "复活即恢复代受：3 伤落在同伴", $"pet {pet.CurrentHp}");
}

// ── 召唤量修正钩子 ──
{
    var s = new CombatState(new RngSet("summon-boost"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);
    var boost = ModelRegistry.New<TestSummonBoostBuff>();
    boost.ApplyInternal(hero.Creature, 2);

    var pet = await PetCmd.Summon<TestPetMonster>(s, hero, 3, null);
    Check(pet!.MaxHp == 5, "ModifySummonAmount：3+2=5", $"实际 {pet.MaxHp}");
}

// ── 归零短路 ──
{
    var s = new CombatState(new RngSet("summon-zero"));
    var hero = new Player("测试者", 70);
    s.AddPlayer(hero);

    var pet = await PetCmd.Summon<TestPetMonster>(s, hero, 0, null);
    Check(pet == null && s.Allies.Count == 1 && !s.Events.Entries.OfType<PetSummoned>().Any(),
        "召唤量≤0：无事发生（STS2 同款短路）");
}

Console.WriteLine();
Console.WriteLine("[10] 真内容第一战：Infanta vs 求终者哨兵");

// ── 主战：耳环同款策略整场打完 ──
{
    async Task<(string log, bool victory, int rounds, List<string> intents, int layers, int deathRound, int openingHand)>
        RunInfanta(string seed)
    {
        var s = new CombatState(new RngSet(seed));
        var infanta = new Player("Infanta", 80);
        s.AddPlayer(infanta);
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Defend>());
        infanta.Deck.AddInternal(ModelRegistry.New<Ignite>());
        var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());

        var log = new List<string>();
        var intents = new List<string>();
        int layers = -1, deathRound = -1;
        s.Events.OnEvent += ev =>
        {
            log.Add(ev.ToLogLine());
            if (ev is MonsterIntentRolled ir) intents.Add(ir.MoveName);
            if (ev is CreatureDied d && d.Creature == sentry)
            {   // 事件在爆炸 hook 之前发出，此刻层数还没被死亡流程动过
                layers = sentry.GetBuffAmount<QuietusBuff>();
                deathRound = s.RoundNumber;
            }
        };
        await CombatCmd.StartCombat(s, infanta);
        int openingHand = infanta.PlayerCombatState!.Hand.Count;

        while (!s.IsOver && s.RoundNumber <= 30)
        {
            for (int plays = 0; plays < 13 && !s.IsOver; plays++)
            {
                var pcs = infanta.PlayerCombatState!;
                CardModel? card = pcs.Hand.Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;
                Creature? target = card.TargetType == TargetType.SingleEnemy
                    ? s.Enemies.FirstOrDefault(e => e.IsAlive) : null;
                if (card.TargetType == TargetType.SingleEnemy && target == null) break;
                await CardCmd.Play(s, card, target);
                CombatCmd.CheckEnd(s);
            }
            if (s.IsOver) break;
            await CombatCmd.EndPlayerTurn(s, infanta);
            await CombatCmd.EnemyTurn(s);
            if (!s.IsOver) await CombatCmd.StartPlayerTurn(s, infanta);
        }
        return (string.Join("\n", log), s.Victory, s.RoundNumber, intents, layers, deathRound, openingHand);
    }

    var (log1, win1, rounds1, intents1, layers1, dr1, hand1) = await RunInfanta("INFANTA-1");
    Check(hand1 == 5, "Infanta 首回合抽 5（默认基数）", $"手 {hand1}");
    Check(win1 && rounds1 <= 30, $"整场获胜（第 {rounds1} 回合，死亡爆炸 {layers1} 层）");

    string[] cycle = { "止刃", "急刺", "据守" };
    bool intentsOk = intents1.Count >= 2 && intents1[0] == "终期"
        && intents1.Skip(1).Select((n, i) => n == cycle[i % 3]).All(x => x);
    Check(intentsOk, "选招表兑现：终期开场一次，此后严格三招循环", string.Join(",", intents1));
    Check(layers1 == dr1 - 1, "终期时间线：层数 == 死亡回合 − 1（回合开始+1 版,不变量不动）", $"层 {layers1} / 死于 R{dr1}");

    var (log2, win2, _, _, _, _, _) = await RunInfanta("INFANTA-1");
    Check(win2 && log1 == log2, "同种子两跑，日志逐字节相同");
    var (log3, _, _, _, _, _, _) = await RunInfanta("INFANTA-B");
    Check(log1 != log3, "换种子日志不同");
}

// ── 终期精确账：+1 节奏 / 吃盾 / 吃灼伤 ──
{
    var s = new CombatState(new RngSet("quietus-math"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);            // 空牌库＝抽 0，本景不出牌
    Check(sentry.Monster!.NextMove!.Name == "终期", "R1 意图＝终期");

    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);                       // 终期施放:1 层出生(裁定⑦)
    Check(sentry.GetBuffAmount<QuietusBuff>() == 1, "R1 行动完：终期 1（1 层出生）");

    await CombatCmd.StartPlayerTurn(s, infanta);        // R2
    Check(sentry.Monster!.NextMove!.Name == "止刃", "R2 意图＝止刃");
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);                       // 回合开始 +1 → 终期 2;止刃 7 直击（眷属非生物，无人代受）
    Check(infanta.Creature.CurrentHp == 73 && sentry.GetBuffAmount<QuietusBuff>() == 2,
        "R2：Infanta 80→73，终期 2", $"hp {infanta.Creature.CurrentHp}");

    await CombatCmd.StartPlayerTurn(s, infanta);        // R3：动手
    await BuffCmd.Apply<SearBuff>(s, infanta.Creature, 3, null);   // 契约师带灼伤Ⅰ·3
    infanta.Creature.GainBlockInternal(1);
    int hpBefore = infanta.Creature.CurrentHp;
    await CreatureCmd.Damage(s, infanta.Creature, new[] { sentry }, 999, ValueProp.Move, null);

    Check(sentry.IsDead && s.Enemies.Count == 0, "哨兵死亡出名册");
    Check(infanta.Creature.CurrentHp == hpBefore - 4 && infanta.Creature.Block == 0,
        "爆炸走完整管线：2 层 ×2＝4，×1.25（灼伤Ⅰ）＝5，护盾挡 1、血扣 4", $"hp {infanta.Creature.CurrentHp}");
    Check(CombatCmd.CheckEnd(s) && s.Victory, "胜利");
}

// ── 秒杀：终期未施放 → 零爆炸 ──
{
    var s = new CombatState(new RngSet("quietus-instakill"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);            // R1 玩家回合，它还没行动
    await CreatureCmd.Damage(s, infanta.Creature, new[] { sentry }, 999, ValueProp.Move, null);
    Check(sentry.IsDead && sentry.GetBuff<QuietusBuff>() == null, "秒杀：终期从未上身");
    Check(infanta.Creature.CurrentHp == 80, "零爆炸：Infanta 满血");
}

Console.WriteLine();
Console.WriteLine("[11] 眷属与换步：史书 / 检索 / 踏焰");

// ── 换步：双分支检索 + 无匹配落空（无眷属，顺带证明检索不算摸牌）──
{
    var s = new CombatState(new RngSet("shiftstep-basic"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);                 // 空牌库：抽 0，能量 3

    var d1 = s.CreateCard<TestDefendPlay>(infanta); pcs.DrawPile.AddInternal(d1);
    var d2 = s.CreateCard<TestDefendPlay>(infanta); pcs.DrawPile.AddInternal(d2);
    var a1 = s.CreateCard<TestStrikePlay>(infanta); pcs.DrawPile.AddInternal(a1);
    var strike = s.CreateCard<TestStrikePlay>(infanta); pcs.Hand.AddInternal(strike);
    var shift  = s.CreateCard<Shiftstep>(infanta);      pcs.Hand.AddInternal(shift);

    await CardCmd.Play(s, strike, s.Enemies[0]);             // 本回合上一张 = 攻击
    await CardCmd.Play(s, shift, null);
    Check(shift.Pile == pcs.ExhaustPile, "换步打出后进消耗堆");
    Check(pcs.Hand.Count == 1 && pcs.Hand.Cards[0] is TestDefendPlay,
        "正向分支：上一张是攻击 → 检索到非攻击牌");
    Check(pcs.DrawPile.Count == 2, "抽牌堆少 1", $"余 {pcs.DrawPile.Count}");

    var shift2 = s.CreateCard<Shiftstep>(infanta); pcs.Hand.AddInternal(shift2);
    await CardCmd.Play(s, shift2, null);                     // 上一张 = 换步（法术）
    Check(pcs.Hand.Cards.Contains(a1), "反向分支：上一张非攻击 → 检索到攻击牌");
    Check(pcs.DrawPile.Count == 1, "抽牌堆再少 1");

    var shift3 = s.CreateCard<Shiftstep>(infanta); pcs.Hand.AddInternal(shift3);
    await CardCmd.Play(s, shift3, null);                     // 还要攻击，但堆里只剩法术
    Check(pcs.DrawPile.Count == 1 && pcs.Hand.Count == 2,
        "无匹配 → 落空：堆手都不动（不翻弃牌堆）");
    Check(!s.Events.Entries.OfType<CardDrawn>().Any(), "检索不算摸牌：全程零 CardDrawn");
}

// ── 换步：跨回合落空（窗口只认本回合的证明）──
{
    var s = new CombatState(new RngSet("shiftstep-window"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);

    var strike = s.CreateCard<TestStrikePlay>(infanta); pcs.Hand.AddInternal(strike);
    await CardCmd.Play(s, strike, s.Enemies[0]);             // R1 打了攻击
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    await CombatCmd.StartPlayerTurn(s, infanta);             // R2（洗回并抽回那张攻击）

    var d1 = s.CreateCard<TestDefendPlay>(infanta); pcs.DrawPile.AddInternal(d1);
    var shift = s.CreateCard<Shiftstep>(infanta);   pcs.Hand.AddInternal(shift);
    await CardCmd.Play(s, shift, null);                      // 上一张在上回合 → 落空
    Check(pcs.Hand.Count == 1 && pcs.DrawPile.Count == 1,
        "跨回合落空：上一张牌属于上回合，本回合窗口不认");
}

// ── 换步升级：先抽 1 再检索 ──
{
    var s = new CombatState(new RngSet("shiftstep-upgraded"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);

    var d1 = s.CreateCard<TestDefendPlay>(infanta); pcs.DrawPile.AddInternal(d1);
    var d2 = s.CreateCard<TestDefendPlay>(infanta); pcs.DrawPile.AddInternal(d2);
    var strike = s.CreateCard<TestStrikePlay>(infanta); pcs.Hand.AddInternal(strike);
    var shiftUp = s.CreateCard<Shiftstep>(infanta); shiftUp.Upgrade(); pcs.Hand.AddInternal(shiftUp);
    Check(shiftUp.Title == "换步+", "升级卡名带 +", shiftUp.Title);

    await CardCmd.Play(s, strike, s.Enemies[0]);
    await CardCmd.Play(s, shiftUp, null);                    // 抽 1（摸牌）+ 检索 1（拿牌）
    Check(pcs.Hand.Count == 2 && pcs.DrawPile.Count == 0,
        "升级：先抽 1 再检索，两张法术都到手");
    Check(s.Events.Entries.OfType<CardDrawn>().Count() == 1, "其中恰有 1 次算摸牌（升级的那一抽）");
}

// ── 踏焰：翻转抽 1 / 每回合一次 / 【改判】窗口只认本回合 ──
{
    var s = new CombatState(new RngSet("firestep"));
    var infanta = new Player("Infanta", 80);
    infanta.AddFamiliarInternal(ModelRegistry.New<Flamedance>());
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);

    for (int i = 0; i < 5; i++)                              // 踏焰抽牌的奖池
    { var p = s.CreateCard<TestStrikePlay>(infanta); pcs.DrawPile.AddInternal(p); }
    var atk1 = s.CreateCard<TestStrikePlay>(infanta); pcs.Hand.AddInternal(atk1);
    var sp1  = s.CreateCard<TestDefendPlay>(infanta); pcs.Hand.AddInternal(sp1);
    var atk2 = s.CreateCard<TestStrikePlay>(infanta); pcs.Hand.AddInternal(atk2);

    await CardCmd.Play(s, atk1, s.Enemies[0]);               // 史书第一条：没有"上一张"
    Check(pcs.Hand.Count == 2, "第一张牌不触发踏焰");
    await CardCmd.Play(s, sp1, null);                        // 攻 → 非攻：翻转
    Check(pcs.Hand.Count == 2, "翻转触发：抽 1（一出一进）");
    await CardCmd.Play(s, atk2, s.Enemies[0]);               // 又翻转，但本回合已用过
    Check(pcs.Hand.Count == 1, "每回合限一次：不再抽");

    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    await CombatCmd.StartPlayerTurn(s, infanta);             // R2：上回合最后一张 = 攻击
    var sp2 = s.CreateCard<TestDefendPlay>(infanta); pcs.Hand.AddInternal(sp2);
    int drawBefore = pcs.DrawPile.Count;
    await CardCmd.Play(s, sp2, null);                        // 本回合第一张：与上回合末不构成翻转
    Check(pcs.DrawPile.Count == drawBefore,
        "【改判 followthrough】窗口只认本回合：新回合第一张不与上回合末翻转");
    var atk3 = pcs.Hand.Cards.First(c => c is TestStrikePlay);
    await CardCmd.Play(s, atk3, s.Enemies[0]);               // 非攻 → 攻：本回合内翻转
    Check(pcs.DrawPile.Count == drawBefore - 1,
        "本回合内翻转照常触发（新回合额度已刷新）");
}

// ── 全家桶：Infanta 完整开局（12 卡 + 炎舞）对哨兵，确定性双跑 ──
{
    async Task<(string log, bool victory)> RunKit(string seed)
    {
        var s = new CombatState(new RngSet(seed));
        var infanta = new Player("Infanta", 80);
        infanta.AddFamiliarInternal(ModelRegistry.New<Flamedance>());
        s.AddPlayer(infanta);
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Defend>());
        infanta.Deck.AddInternal(ModelRegistry.New<Ignite>());
        infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
        CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());

        var log = new List<string>();
        s.Events.OnEvent += ev => log.Add(ev.ToLogLine());
        await CombatCmd.StartCombat(s, infanta);
        while (!s.IsOver && s.RoundNumber <= 30)
        {
            for (int plays = 0; plays < 13 && !s.IsOver; plays++)
            {
                var pcs = infanta.PlayerCombatState!;
                CardModel? card = pcs.Hand.Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;
                Creature? target = card.TargetType == TargetType.SingleEnemy
                    ? s.Enemies.FirstOrDefault(e => e.IsAlive) : null;
                if (card.TargetType == TargetType.SingleEnemy && target == null) break;
                await CardCmd.Play(s, card, target);
                CombatCmd.CheckEnd(s);
            }
            if (s.IsOver) break;
            await CombatCmd.EndPlayerTurn(s, infanta);
            await CombatCmd.EnemyTurn(s);
            if (!s.IsOver) await CombatCmd.StartPlayerTurn(s, infanta);
        }
        return (string.Join("\n", log), s.Victory);
    }

    var (kl1, kw1) = await RunKit("KIT-1");
    Check(kw1, "完整开局套装（12 卡 + 炎舞）获胜");
    var (kl2, _) = await RunKit("KIT-1");
    Check(kl1 == kl2, "全家桶同种子日志逐字节相同");
    var (kl3, _) = await RunKit("KIT-B");
    Check(kl1 != kl3, "换种子日志不同");
}

Console.WriteLine();
Console.WriteLine("[12] 信物与赤金王印（灯笼位 hook）");

// ── 王印：第一回合 +1，之后恢复常态 ──
{
    var s = new CombatState(new RngSet("vermeil"));
    var infanta = new Player("Infanta", 80);
    infanta.AddPledgeInternal(ModelRegistry.New<VermeilSeal>());
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    Check(pcs.Energy == 4, "R1：基础 3 + 王印 1 = 4", $"能 {pcs.Energy}");
    Check(s.Events.Entries.OfType<EnergyGained>().Count() == 1, "EnergyGained 入账 1 条");

    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    await CombatCmd.StartPlayerTurn(s, infanta);
    Check(pcs.Energy == 3, "R2：恢复常态 3——只在第一回合", $"能 {pcs.Energy}");
    Check(s.Events.Entries.OfType<EnergyGained>().Count() == 1, "没有第二条 EnergyGained");
}

// ── 设计表①完整落地：80 血 / 3 能 / 6 抽 / 12 卡 / 炎舞 / 赤金王印 ──
{
    async Task<(string log, bool victory, int energyR1)> RunInfantaComplete(string seed)
    {
        var s = new CombatState(new RngSet(seed));
        var infanta = new Player("Infanta", 80);
        infanta.AddFamiliarInternal(ModelRegistry.New<Flamedance>());
        infanta.AddPledgeInternal(ModelRegistry.New<VermeilSeal>());
        s.AddPlayer(infanta);
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Defend>());
        infanta.Deck.AddInternal(ModelRegistry.New<Ignite>());
        infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
        CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());

        var log = new List<string>();
        s.Events.OnEvent += ev => log.Add(ev.ToLogLine());
        await CombatCmd.StartCombat(s, infanta);
        int energyR1 = infanta.PlayerCombatState!.Energy;
        while (!s.IsOver && s.RoundNumber <= 30)
        {
            for (int plays = 0; plays < 13 && !s.IsOver; plays++)
            {
                var pcs = infanta.PlayerCombatState!;
                CardModel? card = pcs.Hand.Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;
                Creature? target = card.TargetType == TargetType.SingleEnemy
                    ? s.Enemies.FirstOrDefault(e => e.IsAlive) : null;
                if (card.TargetType == TargetType.SingleEnemy && target == null) break;
                await CardCmd.Play(s, card, target);
                CombatCmd.CheckEnd(s);
            }
            if (s.IsOver) break;
            await CombatCmd.EndPlayerTurn(s, infanta);
            await CombatCmd.EnemyTurn(s);
            if (!s.IsOver) await CombatCmd.StartPlayerTurn(s, infanta);
        }
        return (string.Join("\n", log), s.Victory, energyR1);
    }

    var (fl1, fw1, fe1) = await RunInfantaComplete("COMPLETE-1");
    Check(fw1 && fe1 == 4, $"完整 Infanta 开局获胜（R1 能量 {fe1}）");
    var (fl2, _, _) = await RunInfantaComplete("COMPLETE-1");
    Check(fl1 == fl2, "【里程碑】设计表①全套同种子日志逐字节相同");
    var (fl3, _, _) = await RunInfantaComplete("COMPLETE-B");
    Check(fl1 != fl3, "换种子日志不同");
}

Console.WriteLine();
Console.WriteLine("[13] 门铃与围栏：属性事件 / 动作级围栏 / 全体爆炸");

// ── 门铃基础：裸改也响、等值不响、(old,new) 对账 ──
{
    var s = new CombatState(new RngSet("bells"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var c = infanta.Creature;

    var rings = new List<(int oldV, int newV)>();
    c.BlockChanged += (o, n) => rings.Add((o, n));
    c.GainBlockInternal(5);                            // 裸改 Internal——不走 Cmd
    Check(rings.Count == 1 && rings[0] == (0, 5), "裸改也响：直捅 Internal 仍触发（setter 全覆盖）");
    c.LoseBlockInternal(0);                            // 等值写入
    Check(rings.Count == 1, "等值不响（STS2 守卫原样）");
    c.DamageBlockInternal(3, ValueProp.None);
    Check(rings.Count == 2 && rings[1] == (5, 2), "(old,new) 载荷对账", $"{rings[1]}");

    int hpRings = 0;
    c.CurrentHpChanged += (_, _) => hpRings++;
    c.LoseHpInternal(4, ValueProp.None);
    c.HealInternal(0);
    Check(hpRings == 1 && c.CurrentHp == 76, "血铃：掉血一响、零治疗静默");
}

// ── buff 四铃：挂→增→减→归零移除（STS2 增减分铃、减不带量）──
{
    var s = new CombatState(new RngSet("buff-bells"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var log = new List<string>();
    sentry.BuffApplied += b => log.Add($"apply:{b.Id.Entry}");
    sentry.BuffIncreased += (b, d, _) => log.Add($"inc:{d}");
    sentry.BuffDecreased += (b, _) => log.Add("dec");
    sentry.BuffRemoved += b => log.Add($"remove:{b.Id.Entry}");

    var venom = await BuffCmd.Apply<TestVenomBuff>(s, sentry, 2, null);
    await BuffCmd.ChangeAmount(s, venom!, 3, null);
    await BuffCmd.ChangeAmount(s, venom!, -5, null);   // 归零 → 自动移除
    Check(string.Join(",", log) == "apply:TEST_VENOM_BUFF,inc:3,dec,remove:TEST_VENOM_BUFF",
        "四铃时序正确", string.Join(",", log));
}

// ── 能量铃 + 牌堆铃 ──
{
    var s = new CombatState(new RngSet("misc-bells"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var pcs = infanta.PlayerCombatState!;
    int energyRings = 0, pileRings = 0;
    pcs.EnergyChanged += (_, _) => energyRings++;
    pcs.Hand.ContentsChanged += () => pileRings++;

    pcs.ResetEnergy();                                  // 0→3：响
    pcs.ResetEnergy();                                  // 3→3：静默
    pcs.GainEnergy(0);                                  // 等值：静默
    pcs.LoseEnergy(1);                                  // 3→2：响
    Check(energyRings == 2, "能量铃：两响两静默", $"{energyRings} 响");

    var card = s.CreateCard<TestStrikePlay>(infanta);
    pcs.Hand.AddInternal(card);
    pcs.Hand.RemoveInternal(card);
    Check(pileRings == 2, "牌堆铃：加、移各一响", $"{pileRings} 响");
}

// ── 动作级围栏：订阅者炸，围栏内死、围栏外活 ──
{
    var s = new CombatState(new RngSet("fence"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());

    int warns = 0;
    TaskHelper.OnWarn = _ => warns++;
    Action<int, int> boom = (_, _) => throw new InvalidOperationException("表现层炸了");
    sentry.CurrentHpChanged += boom;

    int before = sentry.CurrentHp;
    await TaskHelper.RunStepSafely(async () =>
        await CreatureCmd.Damage(s, infanta.Creature, new[] { sentry }, 5, ValueProp.Move, null));
    Check(warns == 1 && sentry.CurrentHp == before - 5,
        "围栏接住：已写完的状态保留、驱动不死、警告入账", $"警 {warns} hp {sentry.CurrentHp}");

    sentry.CurrentHpChanged -= boom;                    // 成对退订示范
    await TaskHelper.RunStepSafely(async () =>
        await CreatureCmd.Damage(s, infanta.Creature, new[] { sentry }, 5, ValueProp.Move, null));
    Check(warns == 1 && sentry.CurrentHp == before - 10, "围栏外照常活：下一步正常结算");
    TaskHelper.OnWarn = null;                           // 清场
}

// ── 全体爆炸：第一条多玩家断言 ──
{
    var s = new CombatState(new RngSet("multi-boom"));
    var a = new Player("InfantaA", 80);
    var b = new Player("InfantaB", 80);
    s.AddPlayer(a); s.AddPlayer(b);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());

    await BuffCmd.Apply<QuietusBuff>(s, sentry, 2, null);   // 手工喂 2 层，不跑回合
    await CreatureCmd.Damage(s, a.Creature, new[] { sentry }, 999, ValueProp.Move, null);
    Check(a.Creature.CurrentHp == 76 && b.Creature.CurrentHp == 76,
        "死亡爆炸炸全体契约师：两名玩家各吃 4（层数×2）", $"A{a.Creature.CurrentHp} B{b.Creature.CurrentHp}");
}

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("[14] 力量与雪傀儡");

// ── 力量走加段:基伤 6 + 3 力 = 9 ──
{
    var s = new CombatState(new RngSet("strength"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var golem = CombatCmd.SpawnEnemy(s, ModelRegistry.New<SnowGolem>());
    await BuffCmd.Apply<StrengthBuff>(s, golem, 3, null);
    int before = infanta.Creature.CurrentHp;
    await CreatureCmd.Damage(s, golem, new[] { infanta.Creature }, 6, ValueProp.Move, null);
    Check(before - infanta.Creature.CurrentHp == 9, "力量加段:6+3=9",
        $"实际 {before - infanta.Creature.CurrentHp}");
}

// ── 循环次序与掷血 ──
{
    var s = new CombatState(new RngSet("golem"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var golem = CombatCmd.SpawnEnemy(s, ModelRegistry.New<SnowGolem>());
    Check(golem.MaxHp >= 38 && golem.MaxHp <= 42, "血量在 38–42 掷定", $"{golem.MaxHp}");

    var names = new List<string>();
    for (int i = 0; i < 4; i++)
    {
        golem.Monster!.RollNextMove(s);
        names.Add(golem.Monster.NextMove!.Name);
    }
    Check(string.Join(",", names) == "抡砸,积雪,横扫,抡砸", "三招严格循环", string.Join(",", names));
}

// ── 积雪吃进全局公式:两招攻击一起变痛,意图与实算同链 ──
{
    var s = new CombatState(new RngSet("golem-strength"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var golem = CombatCmd.SpawnEnemy(s, ModelRegistry.New<SnowGolem>());

    golem.Monster!.RollNextMove(s);                    // 声明 抡砸(不执行)
    golem.Monster.RollNextMove(s);                     // 声明 积雪
    await golem.Monster.TakeTurn(s);                   // 执行:+6 盾 +1 力
    Check(golem.Block == 6, "积雪 +6 盾", $"{golem.Block}");

    golem.Monster.RollNextMove(s);                     // 声明 横扫
    Check(golem.Monster.IntentPreviewDamage(s) == 7, "横扫意图 6+1=7(意图=实算同链)",
        $"{golem.Monster.IntentPreviewDamage(s)}");
    int before = infanta.Creature.CurrentHp;
    await golem.Monster.TakeTurn(s);
    Check(before - infanta.Creature.CurrentHp == 7 && golem.Block == 9,
        "横扫实打 7 且 +3 盾", $"伤 {before - infanta.Creature.CurrentHp} 盾 {golem.Block}");

    golem.Monster.RollNextMove(s);                     // 声明 抡砸
    Check(golem.Monster.IntentPreviewDamage(s) == 13, "抡砸意图 12+1=13——'别只加在抡砸上'反向也验了",
        $"{golem.Monster.IntentPreviewDamage(s)}");
}
Console.WriteLine();
Console.WriteLine("[15] 棘背兽:竖刺只认法术、实时计数");

// ── 裁定①②:攻击牌不喂刺,法术当场涨段 ──
{
    var s = new CombatState(new RngSet("quill"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    for (int i = 0; i < 2; i++) infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
    infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
    var quill = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Quillback>());
    Check(quill.MaxHp >= 50 && quill.MaxHp <= 54, "血量在 50–54 掷定", $"{quill.MaxHp}");

    await CombatCmd.StartCombat(s, infanta);
    Check(quill.Monster!.NextMove!.Name == "蜷伏", "首回合必蜷伏");
    var pcs = infanta.PlayerCombatState!;

    await CardCmd.Play(s, pcs.Hand.Cards.First(c => c is Shiftstep), null);   // 法术 #1
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);                                             // 执行蜷伏
    Check(infanta.Creature.CurrentHp == 80, "蜷伏无动作");
    await CombatCmd.StartPlayerTurn(s, infanta);

    Check(quill.Monster.NextMove!.Name == "抖刺", "此后恒抖刺");
    Check(quill.Monster.IntentPreviewHits(s) == 2, "已打 1 法术:意图 3×2",
        $"×{quill.Monster.IntentPreviewHits(s)}");

    await CardCmd.Play(s, pcs.Hand.Cards.First(c => c is Attack), quill);
    Check(quill.Monster.IntentPreviewHits(s) == 2, "攻击牌不喂刺(裁定①)",
        $"×{quill.Monster.IntentPreviewHits(s)}");

    await CardCmd.Play(s, pcs.Hand.Cards.First(c => c is Shiftstep), null);   // 法术 #2
    Check(quill.Monster.IntentPreviewHits(s) == 3, "法术当场涨段(裁定②)",
        $"×{quill.Monster.IntentPreviewHits(s)}");

    int before = infanta.Creature.CurrentHp;
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    Check(before - infanta.Creature.CurrentHp == 9, "抖刺执行 3×3=9(执行时实算)",
        $"实伤 {before - infanta.Creature.CurrentHp}");
}

// ── 上限 5 ──
{
    var s = new CombatState(new RngSet("quill-cap"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    for (int i = 0; i < 6; i++) infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
    var quill = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Quillback>());

    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    while (pcs.Hand.Cards.FirstOrDefault(c => c is Shiftstep) is { } spell)
        await CardCmd.Play(s, spell, null);                                   // 0 费连打 5 张
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);                                             // 蜷伏
    await CombatCmd.StartPlayerTurn(s, infanta);

    Check(quill.Monster!.IntentPreviewHits(s) == 5, "1+5 打到上限:意图 3×5",
        $"×{quill.Monster.IntentPreviewHits(s)}");
    int before = infanta.Creature.CurrentHp;
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    Check(before - infanta.Creature.CurrentHp == 15, "抖刺执行 3×5=15(封顶)",
        $"实伤 {before - infanta.Creature.CurrentHp}");
}
Console.WriteLine();
Console.WriteLine("[16] 霜蛭:消融与吸附");

// ── 消融 = Frail 判例:只打折卡牌盾,链出口向下取整 ──
{
    var s = new CombatState(new RngSet("ablation"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<Rimeleech>());
    await BuffCmd.Apply<AblationBuff>(s, infanta.Creature, 1, null);

    var defend = ModelRegistry.New<Defend>();
    int fromCard = await CreatureCmd.GainBlock(s, infanta.Creature, 5, ValueProp.Move, defend);
    Check(fromCard == 3, "卡牌盾 5×0.75=3.75 → 3(向下取整在链出口)", $"得 {fromCard}");

    int fromMove = await CreatureCmd.GainBlock(s, infanta.Creature, 5, ValueProp.Move, null);
    Check(fromMove == 5, "非卡牌来源的盾不受影响(Frail 原判)", $"得 {fromMove}");

    await Hook.AfterTurnEnd(s, CombatSide.Enemy);
    Check(infanta.Creature.Buffs.Count == 0, "Duration:1 层 = 1 回合,tick 后整条消失");
}

// ── 吸附两分支 + 裸自伤不吃盾(裁定④断言化)──
{
    var s = new CombatState(new RngSet("latch"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var leech = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Rimeleech>());
    Check(leech.MaxHp >= 40 && leech.MaxHp <= 44, "血量在 40–44 掷定", $"{leech.MaxHp}");

    await CreatureCmd.GainBlock(s, infanta.Creature, 3, ValueProp.Move, null);
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(leech.Buffs.Any(b => b is StrengthBuff { Amount: 2 }), "剩盾喂力量:+2");

    infanta.Creature.LoseBlockInternal(infanta.Creature.Block);
    await CreatureCmd.GainBlock(s, leech, 5, ValueProp.Move, null);
    int hpBefore = leech.CurrentHp;
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(leech.CurrentHp == hpBefore - 5 && leech.Block == 5,
        "零盾自伤 5,且不吃自己的盾(裁定④:裸)", $"HP {hpBefore}→{leech.CurrentHp} 盾 {leech.Block}");
}

// ── 循环次序 ──
{
    var s = new CombatState(new RngSet("leech-cycle"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var leech = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Rimeleech>());
    var names = new List<string>();
    for (int i = 0; i < 4; i++) { leech.Monster!.RollNextMove(s); names.Add(leech.Monster.NextMove!.Name); }
    Check(string.Join(",", names) == "啃噬,蚀甲,硬皮,啃噬", "三招严格循环", string.Join(",", names));
}

// ── 蚀甲上钩 ──
{
    var s = new CombatState(new RngSet("corrode"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var leech = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Rimeleech>());
    leech.Monster!.RollNextMove(s);
    leech.Monster.RollNextMove(s);
    int before = infanta.Creature.CurrentHp;
    await leech.Monster.TakeTurn(s);
    Check(before - infanta.Creature.CurrentHp == 4
          && infanta.Creature.Buffs.Any(b => b is AblationBuff { Amount: 1 }),
        "蚀甲:4 伤 + 1 层消融上身", $"伤 {before - infanta.Creature.CurrentHp}");
}
Console.WriteLine();
Console.WriteLine("[17] 讨食灵:讨要吃牌(原创机制,边界加倍)");

// ── 吃最高费 + 冻僵不参与比价 ──
{
    var s = new CombatState(new RngSet("waif"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var waif = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Waif>());
    Check(waif.MaxHp >= 48 && waif.MaxHp <= 52, "血量在 48–52 掷定", $"{waif.MaxHp}");
    var pcs = infanta.PlayerCombatState!;
    pcs.DiscardPile.AddInternal(s.CreateCard<Shiftstep>(infanta));       // 0 费
    pcs.DiscardPile.AddInternal(s.CreateCard<Attack>(infanta));          // 1 费 ← 该被吃
    var junk = s.CreateCard<Frostbitten>(infanta);
    pcs.DiscardPile.AddInternal(junk);                                   // 无费:不参与
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(pcs.ExhaustPile.Cards.Any(c => c is Attack), "吃走费用最高的攻击(1 费)");
    Check(pcs.DiscardPile.Cards.Contains(junk) && pcs.DiscardPile.Cards.Any(c => c is Shiftstep),
        "冻僵与低费牌原地不动(无费用不参与比价)");
    Check(!junk.CanPlay(), "冻僵不可打出(Unplayable)");
}

// ── 囤积那拍吃 2;空弃牌堆安全落空 ──
{
    var s = new CombatState(new RngSet("hoard"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var waif = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Waif>());
    var pcs = infanta.PlayerCombatState!;
    waif.Monster!.RollNextMove(s);                                       // 抢夺
    waif.Monster.RollNextMove(s);                                        // 回赠
    waif.Monster.RollNextMove(s);                                        // 囤积 ← 亮着
    pcs.DiscardPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DiscardPile.AddInternal(s.CreateCard<Defend>(infanta));
    pcs.DiscardPile.AddInternal(s.CreateCard<Shiftstep>(infanta));
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(pcs.ExhaustPile.Count == 2 && pcs.DiscardPile.Count == 1,
        "囤积那一拍吃 2 张(两张 1 费先走)", $"耗 {pcs.ExhaustPile.Count} 剩 {pcs.DiscardPile.Count}");
    pcs.DiscardPile.RemoveInternal(pcs.DiscardPile.Cards[0]);
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(pcs.ExhaustPile.Count == 2, "空弃牌堆:安全落空不崩");
}

// ── 回赠塞牌 + 循环次序 ──
{
    var s = new CombatState(new RngSet("handout"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var waif = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Waif>());
    var pcs = infanta.PlayerCombatState!;
    waif.Monster!.RollNextMove(s);                                       // 抢夺(声明)
    waif.Monster.RollNextMove(s);                                        // 回赠
    int before = infanta.Creature.CurrentHp;
    await waif.Monster.TakeTurn(s);
    Check(before - infanta.Creature.CurrentHp == 7
          && pcs.DiscardPile.Cards.Any(c => c is Frostbitten),
        "回赠:7 伤 + 1 张冻僵进弃牌堆", $"伤 {before - infanta.Creature.CurrentHp}");
    waif.Monster.RollNextMove(s);
    Check(waif.Monster.NextMove!.Name == "囤积", "循环第三拍是囤积");
    waif.Monster.RollNextMove(s);
    Check(waif.Monster.NextMove!.Name == "抢夺", "循环闭合回抢夺");
}
Console.WriteLine();
Console.WriteLine("[18] 哨兵 rework 与回合钩子致死");

// ── 新循环名与开场一次 ──
{
    var s = new CombatState(new RngSet("rework"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var names = new List<string>();
    for (int i = 0; i < 5; i++) { sentry.Monster!.RollNextMove(s); names.Add(sentry.Monster.NextMove!.Name); }
    Check(string.Join(",", names) == "终期,止刃,急刺,据守,止刃",
        "开场一次终期,循环止刃/急刺/据守", string.Join(",", names));
}

// ── 爆炸 ×2 裸账(无灼伤无盾)──
{
    var s = new CombatState(new RngSet("boom2"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await BuffCmd.Apply<QuietusBuff>(s, sentry, 3, null);
    await CreatureCmd.Damage(s, infanta.Creature, new[] { sentry }, 999, ValueProp.Move, null);
    Check(infanta.Creature.CurrentHp == 74, "3 层 ×2 = 6 点爆炸", $"hp {infanta.Creature.CurrentHp}");
}

// ── 回合钩子致死 → 当场终局(M3 挂账兑现)──
{
    var s = new CombatState(new RngSet("hook-death"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var leech = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Rimeleech>());
    leech.LoseHpInternal(leech.CurrentHp - 5, ValueProp.None);   // 压到 5 血
    await CombatCmd.StartCombat(s, infanta);                     // 空牌库:抽 0
    await CombatCmd.EndPlayerTurn(s, infanta);                   // 0 盾 → 吸附自伤 5 → 死在钩子里
    Check(s.IsOver && s.Victory, "钩子里死人,当场判胜(不再僵尸局)");
}
Console.WriteLine();
Console.WriteLine("[19] 小霜怪:抱团分工与强度守恒");

// ── 比血分工 → 相等左者 → 挤靠叠力量 → 守恒预览 → 独活闭合 ──
{
    var s = new CombatState(new RngSet("frostling"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var f1 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());   // 左(先入场)
    var f2 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());   // 右
    Check(f1.MaxHp >= 20 && f1.MaxHp <= 24 && f2.MaxHp >= 20 && f2.MaxHp <= 24,
        "两只血量各在 20–24 掷定", $"{f1.MaxHp}/{f2.MaxHp}");

    f1.LoseHpInternal(f1.CurrentHp - 20, ValueProp.None);               // 压到已知值
    f2.LoseHpInternal(f2.CurrentHp - 15, ValueProp.None);
    f1.Monster!.RollNextMove(s);
    f2.Monster!.RollNextMove(s);
    Check(f1.Monster.NextMove!.Name == "扑打" && f2.Monster.NextMove!.Name == "挤靠",
        "血多的扑打,血少的挤靠", $"{f1.Monster.NextMove.Name}/{f2.Monster!.NextMove!.Name}");

    f2.HealInternal(5);                                                 // 20 = 20
    f1.Monster.RollNextMove(s);
    f2.Monster.RollNextMove(s);
    Check(f1.Monster.NextMove!.Name == "扑打" && f2.Monster.NextMove!.Name == "挤靠",
        "相等时左者扑打(裁定③:左 = 先入场)", $"{f1.Monster.NextMove.Name}/{f2.Monster.NextMove.Name}");

    await f2.Monster.TakeTurn(s);                                       // 挤靠 #1
    Check(f1.Buffs.Any(b => b is StrengthBuff { Amount: 2 }), "挤靠:队友 +2 力量");
    await f2.Monster.TakeTurn(s);                                       // 挤靠 #2(同声明重复执行)
    Check(f1.Buffs.Any(b => b is StrengthBuff { Amount: 4 }), "再挤:叠到 4(Counter 合并)");

    Check(f1.Monster.IntentPreviewDamage(s) == 11,
        "强度守恒可见:扑打预览 7+4=11——你打进去的伤害变成了对面的力量",
        $"{f1.Monster.IntentPreviewDamage(s)}");

    await CreatureCmd.Damage(s, infanta.Creature, new[] { f1 }, 999, ValueProp.Move, null);
    f2.Monster.RollNextMove(s);
    Check(f2.Monster.NextMove!.Name == "扑打",
        "死剩一只:它天然是血量较高者,恒扑打——零特例代码");
}

// ── 【裁定】队友阵亡瞬间改口:挤靠 → 扑打,不等下回合 ──
{
    var s = new CombatState(new RngSet("lean-whiff"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var f1 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());
    var f2 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());
    f2.LoseHpInternal(f2.CurrentHp - 10, ValueProp.None);
    f1.Monster!.RollNextMove(s);
    f2.Monster!.RollNextMove(s);
    Check(f2.Monster.NextMove!.Name == "挤靠", "f2 声明挤靠");

    await CreatureCmd.Damage(s, infanta.Creature, new[] { f1 }, 999, ValueProp.Move, null);
    Check(f2.Monster.NextMove!.Name == "扑打" && f2.Monster.IntentPreviewDamage(s) == 7,
        "队友阵亡瞬间改口:意图当场变扑打(7)");
    int hp0 = infanta.Creature.CurrentHp;
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    Check(infanta.Creature.CurrentHp == hp0 - 7, "改口兑现:当回合就挨扑打",
        $"hp {infanta.Creature.CurrentHp}");
}
Console.WriteLine();
Console.WriteLine("[20] 拾柴人与野火灵:分工、换岗与遗产");

// ── 站位涌现:先燎后添,当回合的柴下一回合才烧(整轮集成)──
{
    var s = new CombatState(new RngSet("kindling"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var wf = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Wildfire>());    // 左:先动
    var k  = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Kindler>());     // 右:后动
    Check(wf.MaxHp >= 12 && wf.MaxHp <= 16 && k.MaxHp >= 30 && k.MaxHp <= 34,
        "血量各自掷定(12–16 / 30–34)", $"{wf.MaxHp}/{k.MaxHp}");

    await CombatCmd.StartCombat(s, infanta);                            // 空牌库:抽 0
    Check(wf.Monster!.NextMove!.Name == "燎" && k.Monster!.NextMove!.Name == "添柴",
        "分工:火恒燎,人恒添柴");

    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);                                       // 名册序:燎(旧力量 0)→ 添柴 +4
    Check(infanta.Creature.CurrentHp == 77,
        "本回合按旧力量烧:只掉 3(站位涌现,当回合的柴烧不着)", $"hp {infanta.Creature.CurrentHp}");
    Check(wf.Buffs.Any(b => b is StrengthBuff { Amount: 4 }), "柴已添:野火灵 +4 力量");

    await CombatCmd.StartPlayerTurn(s, infanta);                        // R2
    Check(wf.Monster.IntentPreviewDamage(s) == 7, "下一回合柴才烧:燎预览 3+4=7",
        $"{wf.Monster.IntentPreviewDamage(s)}");
}

// ── 【裁定】火灭瞬间改口:不等下回合 ──
{
    var s = new CombatState(new RngSet("douse"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var wf = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Wildfire>());
    var k  = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Kindler>());
    await CombatCmd.StartCombat(s, infanta);                            // k 已声明添柴

    await CreatureCmd.Damage(s, infanta.Creature, new[] { wf }, 999, ValueProp.Move, null);
    Check(k.Monster!.NextMove!.Name == "挥柴" && k.Monster.IntentPreviewDamage(s) == 13,
        "火灭瞬间改口:意图当场变挥柴(13)");
    int hp0 = infanta.Creature.CurrentHp;
    await CombatCmd.EndPlayerTurn(s, infanta);
    await CombatCmd.EnemyTurn(s);
    Check(infanta.Creature.CurrentHp == hp0 - 13, "改口兑现:当回合就挨挥柴",
        $"hp {infanta.Creature.CurrentHp}");
}

// ── 力量遗产:杀拾柴人止得住变旺,止不了血 ──
{
    var s = new CombatState(new RngSet("legacy"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var wf = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Wildfire>());
    var k  = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Kindler>());
    await CombatCmd.StartCombat(s, infanta);
    await BuffCmd.Apply<StrengthBuff>(s, wf, 4, null);                  // 模拟已喂两轮

    await CreatureCmd.Damage(s, infanta.Creature, new[] { k }, 999, ValueProp.Move, null);
    Check(wf.Buffs.Any(b => b is StrengthBuff { Amount: 4 }), "拾柴人死,力量不散——它是野火灵自己的");
    Check(wf.Monster!.IntentPreviewDamage(s) == 7, "燎预览仍 3+4=7:止得住变旺,止不了血",
        $"{wf.Monster.IntentPreviewDamage(s)}");
}
Console.WriteLine();
Console.WriteLine("[21] C1 引火:灼伤开火权与计数族");

// ── 瞄准 / 裁定5 / 火墙三连 ──
{
    var s = new CombatState(new RngSet("c1-sear"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var a = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var b = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var aim = s.CreateCard<TakeAim>(infanta); pcs.Hand.AddInternal(aim);
    await CardCmd.Play(s, aim, a);
    Check(a.GetBuff<SearBuff>() is { Amount: 2, Level: 1 }, "瞄准:2 层Ⅰ级");
    await BuffCmd.RaiseSearLevel(s, b, 1, null);
    Check(b.GetBuff<SearBuff>() is { Amount: 1, Level: 1 }, "裁定5:无灼伤提级=生成Ⅰ级·1层");
    var wall = s.CreateCard<Firewall>(infanta); pcs.Hand.AddInternal(wall);
    await CardCmd.Play(s, wall, a);
    Check(a.GetBuff<SearBuff>() is { Amount: 3, Level: 2 } && infanta.Creature.Block == 5,
        "火墙:加层(3)提级(Ⅱ)举盾(5)三连");
}

// ── 趁热两侧 / 热浪分伤(灼伤乘区一并入账)──
{
    var s = new CombatState(new RngSet("c1-hot"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var a = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var b = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, a, 1, null);
    await BuffCmd.RaiseSearLevel(s, a, 1, null);          // a:Ⅱ级·1层
    int a0 = a.CurrentHp, b0 = b.CurrentHp;
    var iron1 = s.CreateCard<HotIron>(infanta); pcs.Hand.AddInternal(iron1);
    await CardCmd.Play(s, iron1, a);
    Check(a0 - a.CurrentHp == 24, "趁热达标:(7+9)×灼伤Ⅱ1.5 = 24", $"{a0 - a.CurrentHp}");
    var iron2 = s.CreateCard<HotIron>(infanta); pcs.Hand.AddInternal(iron2);
    await CardCmd.Play(s, iron2, b);
    Check(b0 - b.CurrentHp == 7, "趁热未达标(Ⅰ级不算):素 7", $"{b0 - b.CurrentHp}");
    int a1 = a.CurrentHp, b1 = b.CurrentHp;
    var wave = s.CreateCard<HeatWave>(infanta); pcs.Hand.AddInternal(wave);
    await CardCmd.Play(s, wave, null);                     // 全体卡:无目标直出
    Check(a1 - a.CurrentHp == 15 && b1 - b.CurrentHp == 7,
        "热浪分伤:带灼伤 (7+3)×1.5=15,干净的素 7",
        $"a-{a1 - a.CurrentHp} b-{b1 - b.CurrentHp}");
}

// ── 群体烧伤 ──
{
    var s = new CombatState(new RngSet("c1-mass"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var a = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var b = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var mass = s.CreateCard<MassBurn>(infanta); pcs.Hand.AddInternal(mass);
    await CardCmd.Play(s, mass, null);
    Check(a.GetBuffAmount<SearBuff>() == 1 && b.GetBuffAmount<SearBuff>() == 1 && pcs.Hand.Count == 2,
        "群体烧伤:双敌各 1 层 + 抽 2", $"手 {pcs.Hand.Count}");
}

// ── 计数族(裁定12:不含自身;乘风 v2:≤3 奖励前排)──
{
    var s = new CombatState(new RngSet("c1-count"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var wind1 = s.CreateCard<Tailwind>(infanta); pcs.Hand.AddInternal(wind1);
    var s1 = s.CreateCard<Shiftstep>(infanta); pcs.Hand.AddInternal(s1);
    var s2 = s.CreateCard<Shiftstep>(infanta); pcs.Hand.AddInternal(s2);
    var dash = s.CreateCard<Dash>(infanta); pcs.Hand.AddInternal(dash);
    var fu = s.CreateCard<FollowUp>(infanta); pcs.Hand.AddInternal(fu);
    await CardCmd.Play(s, wind1, null);                    // 已打 0 ≤ 3
    Check(pcs.Hand.Count == 6, "乘风前排(已打0):抽 1+1", $"手 {pcs.Hand.Count}");
    await CardCmd.Play(s, s1, null);
    await CardCmd.Play(s, s2, null);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, dash, sentry);
    Check(h0 - sentry.CurrentHp == 9, "突进:3+2×3(已打 3 张,不含自身)", $"{h0 - sentry.CurrentHp}");
    int h1 = sentry.CurrentHp;
    await CardCmd.Play(s, fu, sentry);
    Check(h1 - sentry.CurrentHp == 16, "追击达标(已打 4 张):8+8", $"{h1 - sentry.CurrentHp}");
    var wind2 = s.CreateCard<Tailwind>(infanta); pcs.Hand.AddInternal(wind2);
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    int hand0 = pcs.Hand.Count;
    await CardCmd.Play(s, wind2, null);                    // 已打 5 > 3
    Check(pcs.Hand.Count == hand0, "乘风后排(已打5):只抽 1(一出一进)", $"手 {pcs.Hand.Count}");
    var rush = s.CreateCard<FlameRush>(infanta); pcs.Hand.AddInternal(rush);
    await CardCmd.Play(s, rush, sentry);                   // 12+3×6=30,足以致命
    Check(sentry.IsDead, "火焰冲击(12+3×6=30)收头");
    Check(infanta.Creature.CurrentHp == 80, "终期未上身,零爆炸(M5 语义顺带复验)");
}

// ── 升级三抽查:数值/双数值/关键词 ──
{
    var s = new CombatState(new RngSet("c1-upg"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var aim = s.CreateCard<TakeAim>(infanta);
    aim.Upgrade();
    Check(aim.Title == "瞄准+" && aim.Vars["Sear"].Int == 4, "瞄准+:标题挂号,2→4");
    var iron = s.CreateCard<HotIron>(infanta);
    iron.Upgrade();
    Check(iron.Vars.Damage.Int == 10 && iron.Vars["Bonus"].Int == 10, "趁热+:7/9→10/10");
    var wind = s.CreateCard<Tailwind>(infanta);
    Check(!wind.HasKeyword(CardKeyword.Innate), "乘风素体无本能");
    wind.Upgrade();
    Check(wind.HasKeyword(CardKeyword.Innate), "乘风+:获得本能(AddKeyword 升级首用)");
}
Console.WriteLine();
Console.WriteLine("[22] C2 拳法与常燃");

// ── 奋击语序 / 提气 ──
{
    var s = new CombatState(new RngSet("c2-exert"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    int h0 = sentry.CurrentHp;
    var exert = s.CreateCard<Exert>(infanta); pcs.Hand.AddInternal(exert);
    await CardCmd.Play(s, exert, sentry);
    Check(h0 - sentry.CurrentHp == 10 && exert.Pile?.Type == PileType.Exhaust,
        "奋击:力量在前(9+1=10),打完进消耗堆", $"{h0 - sentry.CurrentHp}");
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var brace = s.CreateCard<Brace>(infanta); pcs.Hand.AddInternal(brace);
    await CardCmd.Play(s, brace, null);
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 2 && pcs.Hand.Count == 1,
        "提气:+1 力 +1 抽");
}

// ── 永续打出流程 + 血战(裸)+ 烈性联动 ──
{
    var s = new CombatState(new RngSet("c2-blood"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;

    var aura = s.CreateCard<HotBlood>(infanta); pcs.Hand.AddInternal(aura);
    await CardCmd.Play(s, aura, null);
    Check(aura.Pile == null && infanta.Creature.GetBuff<HotBloodAura>() is { Amount: 1 },
        "烈性:卡离开牌堆宇宙,被动上身");
    Check(infanta.Creature.CurrentHp == 77 && infanta.Creature.GetBuffAmount<StrengthBuff>() == 1,
        "语序照卡面:入场 3 点自伤触发自己 +1 力", $"hp {infanta.Creature.CurrentHp}");

    await CreatureCmd.GainBlock(s, infanta.Creature, 3, ValueProp.Move, null);
    var blood = s.CreateCard<Bloodletting>(infanta); pcs.Hand.AddInternal(blood);
    await CardCmd.Play(s, blood, null);
    Check(infanta.Creature.CurrentHp == 72 && infanta.Creature.Block == 3,
        "血战:失去生命是裸的——盾纹丝不动", $"hp {infanta.Creature.CurrentHp}");
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 6, "力量 1+1(烈性)+4(血战)=6");
    Check(pcs.DiscardPile.Cards.Any(c => c is Ember) && blood.Pile?.Type == PileType.Exhaust,
        "余烬入弃牌堆,血战本体消耗");

    var bloodPlus = s.CreateCard<Bloodletting>(infanta);
    bloodPlus.Upgrade();
    pcs.Hand.AddInternal(bloodPlus);
    await CardCmd.Play(s, bloodPlus, null);
    Check(bloodPlus.Pile?.Type == PileType.Discard, "血战+:去掉消耗,打完进弃牌堆");
}

// ── 烈性细则:全挡不触发 / 逐跳触发 / 叠层倍增 ──
{
    var s = new CombatState(new RngSet("c2-hotblood"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    await BuffCmd.Apply<HotBloodAura>(s, infanta.Creature, 1, null);

    await CreatureCmd.GainBlock(s, infanta.Creature, 10, ValueProp.Move, null);
    await CreatureCmd.Damage(s, sentry, new[] { infanta.Creature }, 6, ValueProp.Move, null);
    Check(infanta.Creature.GetBuff<StrengthBuff>() == null, "全被挡下:不算失去生命(裁定4)");

    await CreatureCmd.Damage(s, sentry, new[] { infanta.Creature }, 6, ValueProp.Move, null);   // 挡4漏2
    await CreatureCmd.Damage(s, sentry, new[] { infanta.Creature }, 6, ValueProp.Move, null);   // 全进
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 2, "两跳各触发一次");

    await BuffCmd.Apply<HotBloodAura>(s, infanta.Creature, 1, null);                            // 叠到 2 层
    await CreatureCmd.Damage(s, sentry, new[] { infanta.Creature }, 6, ValueProp.Move, null);
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 4, "叠层倍增:2 层 = 每跳 +2");
}

// ── 透支:能量与回合末自灼(一次性) ──
{
    var s = new CombatState(new RngSet("c2-overdraft"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var od = s.CreateCard<Overdraft>(infanta); pcs.Hand.AddInternal(od);
    await CardCmd.Play(s, od, null);
    Check(pcs.Energy == 7, "透支:3+4=7", $"能 {pcs.Energy}");
    pcs.LoseEnergy(1);
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(infanta.Creature.GetBuffAmount<SearBuff>() == 6, "回合末:剩 6 能 → 自灼 6 层");
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(infanta.Creature.GetBuffAmount<SearBuff>() == 6, "一次性:第二个回合末不再触发");
}

// ── 施加量修正链:环(不问来源)+ 炽烈(只认你的) ──
{
    var s = new CombatState(new RngSet("c2-ring"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<EverflameRingAura>(s, infanta.Creature, 1, null);
    await BuffCmd.Apply<IntensifyAura>(s, infanta.Creature, 1, null);
    var aim = s.CreateCard<TakeAim>(infanta); pcs.Hand.AddInternal(aim);
    await CardCmd.Play(s, aim, sentry);
    Check(sentry.GetBuffAmount<SearBuff>() == 4, "瞄准 2 +环 1 +炽烈 1 = 4", $"{sentry.GetBuffAmount<SearBuff>()}");
    await BuffCmd.Apply<SearBuff>(s, infanta.Creature, 1, null);
    Check(infanta.Creature.GetBuffAmount<SearBuff>() == 1,
        "打到你身上的灼伤:环不管(只管敌人),炽烈不管(不是你施加)");
}

// ── 烧刃:漏伤才点火 ──
{
    var s = new CombatState(new RngSet("c2-edge"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<BurningEdgeAura>(s, infanta.Creature, 1, null);
    await CreatureCmd.GainBlock(s, sentry, 20, ValueProp.Move, null);
    var atk1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk1);
    await CardCmd.Play(s, atk1, sentry);
    Check(!sentry.HasBuff<SearBuff>(), "全被挡:不点火");
    sentry.LoseBlockInternal(sentry.Block);
    var atk2 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk2);
    await CardCmd.Play(s, atk2, sentry);
    Check(sentry.GetBuffAmount<SearBuff>() == 1, "漏伤:+1 灼伤");
}

// ── 白热化 + 灼伤精通 ──
{
    var s = new CombatState(new RngSet("c2-white"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var a = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var b = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearMasteryAura>(s, infanta.Creature, 1, null);
    var wh = s.CreateCard<WhiteHeat>(infanta); pcs.Hand.AddInternal(wh);
    await CardCmd.Play(s, wh, a);
    var atk1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk1);
    await CardCmd.Play(s, atk1, a);
    Check(a.GetBuff<SearBuff>() is { Level: 1, Amount: 1 }, "白热化+攻击:裁定5 生成Ⅰ级·1层");
    var atk2 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk2);
    await CardCmd.Play(s, atk2, a);
    Check(a.GetBuff<SearBuff>()!.Level == 2, "再攻击:提到Ⅱ");
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(a.GetBuff<SearBuff>()!.Level == 3 && !b.HasBuff<SearBuff>(),
        "精通:有灼伤的 a 提到Ⅲ,干净的 b 明文排除");
}

// ── 火烧连环:链抽 ──
{
    var s = new CombatState(new RngSet("c2-chain"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var chain = s.CreateCard<ChainBlaze>(infanta); pcs.Hand.AddInternal(chain);
    await CardCmd.Play(s, chain, null);
    Check(pcs.Hand.Count == 1, "连环:抽到第一张并盯住");
    CardModel first = pcs.Hand.Cards[0];
    await CardCmd.Play(s, first, sentry);
    Check(pcs.Hand.Count == 1, "打出被盯的牌:链重燃,又抽一张");
    CardModel second = pcs.Hand.Cards[0];
    await CardCmd.Play(s, second, sentry);
    Check(pcs.Hand.Count == 1 && pcs.DrawPile.Count == 1 && pcs.DiscardPile.Count == 1,
        "第三跳:洗回续链——After 在进弃牌堆之前发(STS2 时序),正结算的攻击B不参与洗回",
        $"手{pcs.Hand.Count} 抽{pcs.DrawPile.Count} 弃{pcs.DiscardPile.Count}");
}

// ── 火烧连环:两堆全干才是真的干 ──
{
    var s = new CombatState(new RngSet("c2-chain-dry"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var chain = s.CreateCard<ChainBlaze>(infanta); pcs.Hand.AddInternal(chain);
    await CardCmd.Play(s, chain, null);               // 能量账:1 ≤ 3 ✓ 抽/弃两堆皆空
    Check(pcs.Hand.Count == 0 && pcs.DrawPile.Count == 0 && pcs.DiscardPile.Count == 1,
        "两堆全干:抽不到,链不武装,不崩(弃牌堆只剩连环自己)");
}

// ── 烈焰审判(裁定9:消灭是真死亡,终期照爆)──
{
    var s = new CombatState(new RngSet("c2-trial"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<QuietusBuff>(s, sentry, 2, null);
    await BuffCmd.Apply<SearBuff>(s, sentry, 1, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 3, null);         // Ⅳ
    var trial = s.CreateCard<TrialByFire>(infanta); pcs.Hand.AddInternal(trial);
    await CardCmd.Play(s, trial, sentry);
    Check(sentry.IsDead, "灼伤Ⅳ:消灭");
    Check(infanta.Creature.CurrentHp == 76, "真死亡:终期 2×2=4 照爆(裁定9)", $"hp {infanta.Creature.CurrentHp}");
}

{
    var s = new CombatState(new RngSet("c2-trial-miss"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 1, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 2, null);         // Ⅲ
    var trial = s.CreateCard<TrialByFire>(infanta); pcs.Hand.AddInternal(trial);
    await CardCmd.Play(s, trial, sentry);
    Check(sentry.IsAlive, "Ⅲ级:审判落空,目标存活");
}

// ── 烈阳神罚:段数与灭杀连锁 ──
{
    var s = new CombatState(new RngSet("c2-sun"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var golem = CombatCmd.SpawnEnemy(s, ModelRegistry.New<SnowGolem>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, golem, 1, null);
    await BuffCmd.RaiseSearLevel(s, golem, 1, null);          // Ⅱ级
    int g0 = golem.CurrentHp;
    var sun = s.CreateCard<Sunwrath>(infanta); pcs.Hand.AddInternal(sun);
    await CardCmd.Play(s, sun, golem);
    Check(g0 - golem.CurrentHp == 36, "Ⅱ级:3 段 ×(8×1.5)=36", $"{g0 - golem.CurrentHp}");
}

{
    var s = new CombatState(new RngSet("c2-sun-kill"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var f1 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());
    var f2 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<Frostling>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    f1.LoseHpInternal(f1.CurrentHp - 15, ValueProp.None);
    await BuffCmd.Apply<SearBuff>(s, f1, 1, null);            // Ⅰ级:2 段 ×10 = 20 ≥ 15
    var sun = s.CreateCard<Sunwrath>(infanta); pcs.Hand.AddInternal(sun);
    await CardCmd.Play(s, sun, f1);
    Check(f1.IsDead, "击杀达成");
    Check(f2.GetBuff<SearBuff>() is { Amount: 2, Level: 2 },
        "灭杀连锁:幸存者 2 层 + 提级(裁定5:0级起步再+Ⅰ=Ⅱ)");
}

// ── 焚化炉 / 复燃核心 / 永燃斗魂 ──
{
    var s = new CombatState(new RngSet("c2-incin"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<IncineratorAura>(s, infanta.Creature, 1, null);
    for (int i = 0; i < 3; i++) pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    await Hook.AfterTurnStarted(s, CombatSide.Player);
    Check(pcs.Hand.Count == 2 && infanta.Creature.GetBuffAmount<StrengthBuff>() == 3,
        "焚化炉开工:抽 2、+3 力");
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(pcs.ExhaustPile.Count == 1, "无余烬:改烧堆顶——堆里只剩 1 张就烧 1 张", $"耗 {pcs.ExhaustPile.Count}");
    var ember = s.CreateCard<Ember>(infanta); pcs.Hand.AddInternal(ember);
    await Hook.AfterTurnEnd(s, CombatSide.Player);
    Check(ember.Pile?.Type == PileType.Exhaust, "有余烬:优先烧余烬");
}

{
    var s = new CombatState(new RngSet("c2-rekindle"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<RekindleAura>(s, infanta.Creature, 1, null);
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var junk = s.CreateCard<Ember>(infanta); pcs.Hand.AddInternal(junk);
    await CardPileCmd.Exhaust(s, junk);
    Check(pcs.Hand.Count == 1, "复燃核心:有牌被消耗 → 抽 1");
    for (int i = 0; i < 4; i++) pcs.ExhaustPile.AddInternal(s.CreateCard<Ember>(infanta));
    var spirit = s.CreateCard<EverburningSpirit>(infanta); pcs.Hand.AddInternal(spirit);
    await CardCmd.Play(s, spirit, null);
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 2,
        "永燃斗魂:消耗堆 5 张(4+余烬)/2 = 2 力", $"力 {infanta.Creature.GetBuffAmount<StrengthBuff>()}");
}

// ── 赤热刺快账 ──
{
    var s = new CombatState(new RngSet("c2-jab"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    int h0 = sentry.CurrentHp;
    var jab = s.CreateCard<RedHotJab>(infanta); pcs.Hand.AddInternal(jab);
    await CardCmd.Play(s, jab, sentry);               // 能量账:2 ≤ 3 ✓
    Check(h0 - sentry.CurrentHp == 14 && sentry.GetBuffAmount<SearBuff>() == 1
          && sentry.GetBuffAmount<WeakenBuff>() == 1, "赤热刺:14/1灼/1弱");
}

// ── 焚身(断言先过血条:44–48 的哨兵只挨这一下)──
{
    var s = new CombatState(new RngSet("c2-combust"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 1, null);        // 预置灼伤Ⅰ·1层(顶替赤热刺那口)
    var burst = s.CreateCard<Combust>(infanta); pcs.Hand.AddInternal(burst);
    int h1 = sentry.CurrentHp;
    await CardCmd.Play(s, burst, sentry);             // 能量账:1 ≤ 3 ✓
    Check(h1 - sentry.CurrentHp == 37 && infanta.Creature.GetBuff<SearBuff>() is { Amount: 3, Level: 2 },
        "焚身:30×1.25(目标灼Ⅰ)=37,自灼 3 层并自提Ⅱ级", $"{h1 - sentry.CurrentHp}");
    Check(sentry.IsAlive, "37 装得进 44–48 的血条(上一版 14+37 溢出被截断成 30)");
}

// ── 引火烧身(活目标 + 足额能量:上一版双闸全关)──
{
    var s = new CombatState(new RngSet("c2-backdraft"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 1, null);            // 敌:Ⅰ级·1层
    await BuffCmd.Apply<SearBuff>(s, infanta.Creature, 3, null);  // 己:3 层……
    await BuffCmd.RaiseSearLevel(s, infanta.Creature, 1, null);   // ……提Ⅱ(顶替焚身的余波)
    var bd = s.CreateCard<Backdraft>(infanta); pcs.Hand.AddInternal(bd);
    int hp0 = infanta.Creature.CurrentHp;
    await CardCmd.Play(s, bd, sentry);                // 能量账:1 ≤ 3 ✓ 目标活着 ✓
    Check(sentry.GetBuff<SearBuff>() is { Amount: 4, Level: 2 },
        "引火烧身:敌灼 1+3=4 层,Ⅰ提Ⅱ", $"层{sentry.GetBuffAmount<SearBuff>()}");
    Check(infanta.Creature.CurrentHp == hp0 - 3
          && infanta.Creature.GetBuff<SearBuff>() is { Amount: 5, Level: 2 },
        "自伤 2 走管线且吃自己的灼Ⅱ(×1.5=3);自灼 3+2=5 层", $"hp {infanta.Creature.CurrentHp}");
}

// ── 升级抽查 ──
{
    var s = new CombatState(new RngSet("c2-upg"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var chain = s.CreateCard<ChainBlaze>(infanta);
    chain.Upgrade();
    Check(chain.Cost == 0, "连环+:费用 1→0");
    var od = s.CreateCard<Overdraft>(infanta);
    od.Upgrade();
    Check(od.Vars["Energy"].Int == 6, "透支+:4→6");
    var burst = s.CreateCard<Combust>(infanta);
    burst.Upgrade();
    Check(burst.Vars["SelfSear"].Int == 1, "焚身+:自灼 3→1(负增量升级首用)");
}
Console.WriteLine("[23] S1 选择器 / S2 费用层 / 多重打出");

// ── S1:空手落空 / 不足自动全拿(免确认零 UI) ──
{
    var s = new CombatState(new RngSet("c3-sel"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;

    var none = await CardSelectCmd.Immolate(s, infanta, 2, null!);
    Check(none.Count == 0 && pcs.ExhaustPile.Count == 0, "焚毁遇空手:落空不崩(燃烧契约边界)");

    var a = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(a);
    var one = await CardSelectCmd.Immolate(s, infanta, 2, null!);
    Check(one.Count == 1 && a.Pile == pcs.ExhaustPile,
        "手牌不足 N:有多少焚多少——min==max 免确认,不弹选择器(STS2 原式)");
}

// ── S1:脚本选择器精确点名 / 独占守卫 ──
{
    var s = new CombatState(new RngSet("c3-sel2"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var a = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(a);
    var b = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(b);
    var c = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(c);

    using (CardSelectCmd.UseSelector(new ScriptedCardSelector().Then(pool => new[] { pool[1] })))
    {
        bool doubled = false;
        try { CardSelectCmd.UseSelector(new ScriptedCardSelector()); }
        catch (InvalidOperationException) { doubled = true; }
        Check(doubled, "选择器独占:二次 UseSelector 抛(同 STS2 守卫)");

        await CardSelectCmd.Immolate(s, infanta, 1, null!);
        Check(b.Pile == pcs.ExhaustPile && a.Pile == pcs.Hand && c.Pile == pcs.Hand,
            "脚本选择器点名第 2 张:只烧 b,a/c 原地");
    }
    Check(CardSelectCmd.Selector == null, "using 退场:选择器栈清空");
}

// ── S2:修正条三性(相对/封底/过期) ──
{
    var s = new CombatState(new RngSet("c3-cost"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var turnCard = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(turnCard);
    var combatCard = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(combatCard);

    turnCard.AddCostThisTurn(-1);
    Check(turnCard.Cost == 0, "本回合 -1:费用 1→0");
    turnCard.AddCostThisTurn(-5);
    Check(turnCard.Cost == 0, "出口封底:负到底也是 0(全局唯一封底点)");
    combatCard.AddCostThisCombat(-1);
    Check(combatCard.Cost == 0, "本场 -1:费用 1→0");

    await CombatCmd.EndPlayerTurn(s, infanta);
    Check(turnCard.Cost == 1, "回合末清扫:「本回合」条过期,费用回 1(STS2 EndOfTurnCleanup)");
    Check(combatCard.Cost == 0, "「本场」条不受回合末清扫");
}

// ── S3:多重打出只认焚毁卡,不消耗则层数不动 ──
{
    var s = new CombatState(new RngSet("c3-dup"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<GreasePotBuff>(s, infanta.Creature, 1, null);
    var atk = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, atk, sentry);
    Check(h0 - sentry.CurrentHp == 6 && infanta.Creature.GetBuffAmount<GreasePotBuff>() == 1,
        "非焚毁卡:单发照旧,油脂一层不掉(修改者名单为空=零回调)");
}

Console.WriteLine("[24] C3 终曲:三十卡收官");

// ── 焚牌:抽了再选 ──
{
    var s = new CombatState(new RngSet("c3-tinder"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var a1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(a1);
    var tinder = s.CreateCard<Tinder>(infanta); pcs.Hand.AddInternal(tinder);

    using (CardSelectCmd.UseSelector(new ScriptedCardSelector()))   // 默认:从头拿满 min
    {
        await CardCmd.Play(s, tinder, null);                        // 能量账:0 ✓
        Check(pcs.Hand.Count == 1 && a1.Pile == pcs.ExhaustPile && tinder.Pile == pcs.DiscardPile,
            "焚牌:抽 1(手 2)再焚 1(烧 a1)——先抽后选,池子含新抽");
    }
}

// ── 掷火:池恰 1 张走自动路 ──
{
    var s = new CombatState(new RngSet("c3-throw"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var filler = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(filler);
    var tf = s.CreateCard<ThrowFire>(infanta); pcs.Hand.AddInternal(tf);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, tf, sentry);                              // 能量账:1 ✓ 未装选择器:自动路必须走通
    Check(h0 - sentry.CurrentHp == 12 && filler.Pile == pcs.ExhaustPile, "掷火:12 + 焚毁唯一候选(零选择器)");
}

// ── 挥霍:抽 3 焚 2;升级焚 1 ──
{
    var s = new CombatState(new RngSet("c3-squander"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    for (int i = 0; i < 3; i++) pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var f1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(f1);
    var f2 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(f2);
    var sq = s.CreateCard<Squander>(infanta); pcs.Hand.AddInternal(sq);

    using (CardSelectCmd.UseSelector(new ScriptedCardSelector()))   // 池 5 张:默认拿前 2(f1,f2)
    {
        await CardCmd.Play(s, sq, null);                            // 能量账:1 ✓
        Check(pcs.Hand.Count == 3 && pcs.ExhaustPile.Count == 2, "挥霍:抽 3 焚 2,手上净 +0 变 3");
    }
    var sq2 = s.CreateCard<Squander>(infanta);
    sq2.Upgrade();
    Check(sq2.Vars["Burn"].Int == 1, "挥霍+:焚毁 2→1(负增量)");
}

// ── 投薪 / 焚牌蓄力:力量账 ──
{
    var s = new CombatState(new RngSet("c3-feed"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var a = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(a);
    var b = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(b);
    var feed = s.CreateCard<FeedTheFire>(infanta); pcs.Hand.AddInternal(feed);
    await CardCmd.Play(s, feed, null);                              // 能量账:1 ✓ 池=min:自动全拿
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 4 && pcs.ExhaustPile.Count == 3,
        "投薪:+4 力,烧 2 + 自耗 = 消耗堆 3");

    var feed2 = s.CreateCard<FeedTheFire>(infanta);
    feed2.Upgrade();
    Check(!feed2.HasKeyword(CardKeyword.Exhaust), "投薪+:去「消耗」");
}

{
    var s = new CombatState(new RngSet("c3-fireup"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var atk = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk);
    var fu = s.CreateCard<FireUp>(infanta); pcs.Hand.AddInternal(fu);
    await CardCmd.Play(s, fu, null);                                // 烧的是攻击牌
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 3, "蓄力烧攻击:2+1=3 力");

    var s2 = new CombatState(new RngSet("c3-fireup2"));
    var infanta2 = new Player("Infanta", 80);
    s2.AddPlayer(infanta2);
    CombatCmd.SpawnEnemy(s2, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s2, infanta2);
    var pcs2 = infanta2.PlayerCombatState!;
    var wind = s2.CreateCard<Tailwind>(infanta2); pcs2.Hand.AddInternal(wind);
    var fu2 = s2.CreateCard<FireUp>(infanta2); pcs2.Hand.AddInternal(fu2);
    await CardCmd.Play(s2, fu2, null);                              // 烧的是法术
    Check(infanta2.Creature.GetBuffAmount<StrengthBuff>() == 2, "蓄力烧法术:只有素 2 力");
}

// ── 孤注:全烧全打 ──
{
    var s = new CombatState(new RngSet("c3-allin"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    for (int i = 0; i < 3; i++) pcs.Hand.AddInternal(s.CreateCard<Attack>(infanta));
    var allin = s.CreateCard<AllIn>(infanta); pcs.Hand.AddInternal(allin);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, allin, sentry);                           // 能量账:2 ✓ 血条账:27<44 ✓
    Check(h0 - sentry.CurrentHp == 27 && pcs.ExhaustPile.Count == 4 && pcs.Hand.Count == 0,
        "孤注:烧 3 张打 3×9=27,含自耗共 4 进消耗堆(不弹选择器)");
}

// ── 油库引爆:燃料加倍/非燃料抵命 ──
{
    var s = new CombatState(new RngSet("c3-keg"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    for (int i = 0; i < 6; i++) pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var wood = s.CreateCard<Cordwood>(infanta); pcs.Hand.AddInternal(wood);
    var atk = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk);
    var keg = s.CreateCard<Powderkeg>(infanta); pcs.Hand.AddInternal(keg);

    using (CardSelectCmd.UseSelector(new ScriptedCardSelector().Then(pool => pool.ToList())))
    {
        await CardCmd.Play(s, keg, null);                           // 能量账:1 ✓ 全选两张
        Check(pcs.Hand.Count == 6, "薪柴燃料 1 原生 + 2 额外 = 抽 6", $"手 {pcs.Hand.Count}");
        Check(infanta.Creature.CurrentHp == 79, "非燃料的攻击牌:抵命 1(走管线)", $"hp {infanta.Creature.CurrentHp}");
        Check(pcs.ExhaustPile.Count == 2 && pcs.DrawPile.Count == 0, "两张都进消耗堆,抽牌堆抽干");
    }
}

// ── 爆燃(双敌全体) / 火海 / 油焰引爆 ──
{
    var s = new CombatState(new RngSet("c3-flash"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var e1 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var e2 = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var s1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s1);
    var s2c = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s2c);
    var fo = s.CreateCard<Flashover>(infanta); pcs.Hand.AddInternal(fo);
    int h1 = e1.CurrentHp, h2 = e2.CurrentHp;
    await CardCmd.Play(s, fo, null);                                // 能量账:2 ✓
    Check(h1 - e1.CurrentHp == 19 && h2 - e2.CurrentHp == 19, "爆燃:全体各 19");
    Check(pcs.DiscardPile.Cards.Count(c => c is Ember) == 2, "余烬 2 入弃牌堆");
    Check(pcs.ExhaustPile.Count == 2, "焚毁 2(池=min 自动)");
}

{
    var s = new CombatState(new RngSet("c3-sea"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var s1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s1);
    var s2c = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s2c);
    var sea = s.CreateCard<SeaOfFire>(infanta); pcs.Hand.AddInternal(sea);
    await CardCmd.Play(s, sea, null);                               // 能量账:2 ✓
    Check(sentry.GetBuff<SearBuff>() is { Amount: 6, Level: 3 }, "火海:6 层 + 提Ⅱ = Ⅲ级·6层(裁定5:0级起步)");

    var oil = s.CreateCard<OilFlash>(infanta); pcs.Hand.AddInternal(oil);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, oil, sentry);                             // 能量账:0 ✓
    Check(h0 - sentry.CurrentHp == 21, "油焰引爆:1+6=7 段 × ⌊2×1.75⌋=3 → 21(逐段取整)", $"{h0 - sentry.CurrentHp}");

    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    await CardPileCmd.Exhaust(s, oil);                              // 从弃牌堆点燃
    Check(pcs.Hand.Count == 2 && sentry.GetBuff<SearBuff>()!.Amount == 9,
        "油焰燃料:抽 2 + 全体 +3 层(6→9)");
}

// ── 火山灰 / 灰烬护盾 / 借火(能量账重点) ──
{
    var s = new CombatState(new RngSet("c3-ember3"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var te = s.CreateCard<Tephra>(infanta); pcs.Hand.AddInternal(te);
    var ash = s.CreateCard<AshShield>(infanta); pcs.Hand.AddInternal(ash);
    var al = s.CreateCard<ALight>(infanta); pcs.Hand.AddInternal(al);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, te, sentry);
    await CardCmd.Play(s, ash, null);
    await CardCmd.Play(s, al, null);                                // 能量账:0+1+0,借火回 1 → 净 3
    Check(h0 - sentry.CurrentHp == 9, "火山灰:9");
    Check(infanta.Creature.Block == 11, "灰烬护盾:11");
    Check(pcs.Energy == 3, "借火:3-1+1=3(能量动词走 CombatCmd.GainEnergy)");
    Check(pcs.DiscardPile.Cards.Count(c => c is Ember) == 4, "余烬账:2+1+1=4 全进弃牌堆");
}

// ── 余烬倾泻 / 灰飞烟灭 ──
{
    var s = new CombatState(new RngSet("c3-ashfall"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var af = s.CreateCard<Ashfall>(infanta); pcs.Hand.AddInternal(af);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, af, null);                                // 能量账:2 ✓ 血条账:28<44 ✓
    Check(h0 - sentry.CurrentHp == 28, "余烬倾泻:全体 28");
    Check(pcs.Hand.Count == 10 && pcs.Hand.Cards.All(c => c is Ember), "手牌填满:10 张全是余烬");
}

{
    var s = new CombatState(new RngSet("c3-smoke"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.Hand.AddInternal(s.CreateCard<Ember>(infanta));
    pcs.Hand.AddInternal(s.CreateCard<Ember>(infanta));
    var keep = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(keep);
    var uis = s.CreateCard<UpInSmoke>(infanta); pcs.Hand.AddInternal(uis);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, uis, null);                               // 能量账:2 ✓ 无指向卡,target=null
    Check(h0 - sentry.CurrentHp == 16 && pcs.ExhaustPile.Count == 2 && keep.Pile == pcs.Hand,
        "灰飞烟灭:吃 2 干扰 → 2×8=16(随机靶=独苗),攻击牌不动");
}

// ── 引火索 / 薪柴 / 油脂弹×掷火 / 不熄之炬 / 薪火长明 ──
{
    var s = new CombatState(new RngSet("c3-fuse"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var fuse = s.CreateCard<Fuse>(infanta); pcs.Hand.AddInternal(fuse);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, fuse, sentry);
    await CardPileCmd.Exhaust(s, fuse);                             // 燃料:随机敌(独苗) 6
    Check(h0 - sentry.CurrentHp == 12, "引火索:6 + 燃料 6(CombatTargets 流选独苗)");
}

{
    var s = new CombatState(new RngSet("c3-wood"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    for (int i = 0; i < 3; i++) pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var wood = s.CreateCard<Cordwood>(infanta); pcs.Hand.AddInternal(wood);
    await CardCmd.Play(s, wood, null);
    Check(pcs.Hand.Count == 1, "薪柴:抽 1");
    await CardPileCmd.Exhaust(s, wood);
    Check(pcs.Hand.Count == 3, "薪柴燃料:再抽 2");
}

{
    var s = new CombatState(new RngSet("c3-grease"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var gp = s.CreateCard<GreasePot>(infanta); pcs.Hand.AddInternal(gp);
    var sac1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(sac1);
    var sac2 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(sac2);
    var tf = s.CreateCard<ThrowFire>(infanta); pcs.Hand.AddInternal(tf);

    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, gp, sentry);                              // 6 伤 + 4 盾
    await CardPileCmd.Exhaust(s, gp);                               // 燃料:油脂 1 层
    Check(infanta.Creature.GetBuffAmount<GreasePotBuff>() == 1 && infanta.Creature.Block == 4,
        "油脂弹:6/4盾,燃料挂 1 层油脂");

    using (CardSelectCmd.UseSelector(new ScriptedCardSelector()))   // 第一发焚毁池 2 张须选;第二发池 1 自动
    {
        await CardCmd.Play(s, tf, sentry);                          // 能量账:1+1=2 ✓
        Check(h0 - sentry.CurrentHp == 6 + 24, "油脂×掷火:整卡结算两次(12×2),共 30", $"{h0 - sentry.CurrentHp}");
        Check(pcs.ExhaustPile.Count == 3, "两发各焚 1(sac1+sac2)+ 油脂弹自耗 = 3");
        Check(infanta.Creature.GetBuff<GreasePotBuff>() == null, "油脂用后即焚:一层耗尽自灭(Duplication 自减)");
        Check(pcs.PlayHistory.Count(r => r.Card == tf) == 2,
            "史书逐轮记账:双发的掷火两条(STS2 双份账同义——女妖之嚎数临时牌,双发数两次)");
        Check(s.Events.Entries.OfType<CardPlayStarted>().Count() == 3,
            "开打事件:油脂弹1+掷火2=3(逐次节拍的外显)");
    }
}

{
    var s = new CombatState(new RngSet("c3-torch"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await CombatCmd.GainEnergy(s, infanta, 1);                      // 能量账:2+2=4 → 3+1
    var torch = s.CreateCard<UnquenchedTorch>(infanta); pcs.Hand.AddInternal(torch);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, torch, sentry);                           // 10
    await CardPileCmd.Exhaust(s, torch);                            // 回手,10→20
    Check(torch.Pile == pcs.Hand && torch.Vars.Damage.Int == 20, "不熄之炬:燃料回手,伤害翻倍");
    await CardCmd.Play(s, torch, sentry);                           // 20
    Check(h0 - sentry.CurrentHp == 30, "两轮共 10+20=30(血条账:30<44)", $"{h0 - sentry.CurrentHp}");
    await CardPileCmd.Exhaust(s, torch);
    Check(torch.Vars.Damage.Int == 40 && !torch.Vars.Damage.WasJustUpgraded,
        "再翻到 40,本场可叠加;高亮标记已随手清掉(不污染升级预览)");
}

{
    var s = new CombatState(new RngSet("c3-undying"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    for (int i = 0; i < 5; i++) pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var uf = s.CreateCard<UndyingFlame>(infanta); pcs.Hand.AddInternal(uf);
    var wood = s.CreateCard<Cordwood>(infanta); pcs.Hand.AddInternal(wood);
    await CardCmd.Play(s, uf, null);                                // 永续:limbo + 本体上身
    Check(uf.Pile == null && infanta.Creature.GetBuffAmount<UndyingFlameAura>() == 1,
        "薪火长明:卡入 limbo,本体挂玩家");
    await CardCmd.Play(s, wood, null);                              // 抽 1
    await CardPileCmd.Exhaust(s, wood);                             // 原生 2 + 长明加班 2
    Check(pcs.Hand.Count == 5, "燃料翻倍:1 + 2 + 2 = 5 张在手", $"手 {pcs.Hand.Count}");
}

// ── 狂涌 / 狂战 / 无双乱舞(S2 实战) ──
{
    var s = new CombatState(new RngSet("c3-surge"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 1, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 2, null);               // Ⅲ级
    var surge = s.CreateCard<Surge>(infanta); pcs.Hand.AddInternal(surge);
    Check(surge.Cost == 0, "狂涌:3 − 全场最高等级Ⅲ = 0(预览=结算同源)");
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, surge, sentry);
    Check(h0 - sentry.CurrentHp == 21 && pcs.Energy == 3,
        "狂涌 0 费打出:12×1.75=21,能量分文未动", $"{h0 - sentry.CurrentHp}/能量{pcs.Energy}");
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 1, "+1 力");
}

{
    var s = new CombatState(new RngSet("c3-frenzy"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var atk = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(atk);
    var fz = s.CreateCard<Frenzy>(infanta); pcs.Hand.AddInternal(fz);
    await CardCmd.Play(s, fz, null);                                // 能量账:2
    Check(atk.Cost == 0, "狂战:攻击牌本回合 1→0");
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, atk, sentry);                             // 免费
    Check(h0 - sentry.CurrentHp == 9 && pcs.Energy == 1, "0 费打出:6+3力=9,能量剩 1");
    await CombatCmd.EndPlayerTurn(s, infanta);
    Check(atk.Cost == 1, "回合落幕:狂战的折扣随 EndOfTurnCleanup 过期");
}

{
    var s = new CombatState(new RngSet("c3-onslaught"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var on = s.CreateCard<Onslaught>(infanta); pcs.Hand.AddInternal(on);
    Check(on.Cost == 20, "无双乱舞素体:20 费(裁定10)");
    for (int i = 0; i < 5; i++)
    {
        var w = s.CreateCard<Tailwind>(infanta);
        pcs.Hand.AddInternal(w);
        await CardCmd.Play(s, w, null);                             // 5 张 0 费进史书
    }
    Check(on.Cost == 15, "全场打过 5 张:20−5=15(费用随史书滑落)");
    await CombatCmd.GainEnergy(s, infanta, 12);                     // 能量账:3+12=15
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, on, sentry);
    Check(h0 - sentry.CurrentHp == 39,
        "6 段(1+本回合5,不含自身)伤害滚力量:4+5+6+7+8+9=39(微裁定3,血条账:39<44)", $"{h0 - sentry.CurrentHp}");
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 6 && pcs.Energy == 0, "+6 力,能量 15 花光");
}

// ── 公式三张(微裁定1 的账) ──
{
    var s = new CombatState(new RngSet("c3-cookoff"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 4, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 1, null);               // {4,Ⅱ}
    var s1 = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s1);
    var s2c = s.CreateCard<Attack>(infanta); pcs.Hand.AddInternal(s2c);
    var cook = s.CreateCard<CookOff>(infanta); pcs.Hand.AddInternal(cook);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, cook, sentry);                            // 能量账:2 ✓
    Check(h0 - sentry.CurrentHp == 37,
        "殉爆{4,Ⅱ}:基础 5+4×(4+2−1)=25,管线再乘 ×1.5 → 37(公式伤照吃乘算,微裁定1)", $"{h0 - sentry.CurrentHp}");
    Check(pcs.ExhaustPile.Count == 2, "焚毁 2");
}

{
    var s = new CombatState(new RngSet("c3-cookoff2"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 2, null);              // {2,Ⅰ}
    var cook = s.CreateCard<CookOff>(infanta);
    cook.Upgrade();
    pcs.Hand.AddInternal(cook);
    Check(cook.Describe().Contains("巨额"), "殉爆+:文案跳档到巨额");
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, cook, sentry);
    Check(h0 - sentry.CurrentHp == 17, "巨额{2,Ⅰ}:(5+2×1)×2=14,×1.25 → 17", $"{h0 - sentry.CurrentHp}");
}

{
    var s = new CombatState(new RngSet("c3-conflag"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<StrengthBuff>(s, infanta.Creature, 2, null);
    await BuffCmd.Apply<SearBuff>(s, sentry, 2, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 1, null);               // {2,Ⅱ}
    var cf = s.CreateCard<Conflagration>(infanta); pcs.Hand.AddInternal(cf);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, cf, sentry);                              // 能量账:3 ✓
    Check(h0 - sentry.CurrentHp == 37,
        "狂炎引爆:基础(15+4×2力)=23,管线 +2力 → 25,×1.5 → 37——力量合计 5 倍(卡面明言)", $"{h0 - sentry.CurrentHp}");
    Check(cf.Pile == pcs.ExhaustPile, "消耗");
}

{
    var s = new CombatState(new RngSet("c3-cleanse"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, sentry, 2, null);
    await BuffCmd.RaiseSearLevel(s, sentry, 1, null);               // {2,Ⅱ}
    var cl = s.CreateCard<CleansingFire>(infanta); pcs.Hand.AddInternal(cl);
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, cl, sentry);
    Check(h0 - sentry.CurrentHp == 30 && !sentry.HasBuff<SearBuff>(),
        "火焰净化:巨额 2×15=30 素伤(灼伤先被吃掉→乘算天然不吃),灼伤蒸发", $"{h0 - sentry.CurrentHp}");

    var cl2 = s.CreateCard<CleansingFire>(infanta);
    cl2.Upgrade();
    Check(!cl2.HasKeyword(CardKeyword.Exhaust), "净化+:去「消耗」");
}

// ── 镜炎(裁定6:叠层取高) ──
{
    var s = new CombatState(new RngSet("c3-mirror"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var ea = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    var eb = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    await BuffCmd.Apply<SearBuff>(s, ea, 3, null);
    await BuffCmd.RaiseSearLevel(s, ea, 1, null);                   // A:{3,Ⅱ}
    await BuffCmd.Apply<WeakenBuff>(s, ea, 2, null);
    await BuffCmd.Apply<SearBuff>(s, eb, 1, null);
    await BuffCmd.RaiseSearLevel(s, eb, 2, null);                   // B:{1,Ⅲ}
    var mf = s.CreateCard<Mirrorflame>(infanta); pcs.Hand.AddInternal(mf);
    int hA = ea.CurrentHp;
    await CardCmd.Play(s, mf, ea);                                  // 能量账:0 ✓
    Check(hA - ea.CurrentHp == 9, "镜炎对 A:6×1.5(A 自己的Ⅱ)=9", $"{hA - ea.CurrentHp}");
    Check(eb.GetBuff<SearBuff>() is { Amount: 4, Level: 3 },
        "B 收到镜像:层数 1+3=4 叠加,等级 max(Ⅲ,Ⅱ)=Ⅲ 取高(裁定6)");
    Check(eb.GetBuffAmount<WeakenBuff>() == 2, "弱化 2 也照进 B(全部负面)");
    Check(ea.GetBuff<SearBuff>() is { Amount: 3, Level: 2 }, "A 自身不动(镜子不回照)");
}

// ── 背水狂炎(珊瑚钳位全链) ──
{
    var s = new CombatState(new RngSet("c3-laststand"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    var ls = s.CreateCard<LastStand>(infanta); pcs.Hand.AddInternal(ls);
    await CardCmd.Play(s, ls, null);                                // 能量账:1 ✓
    Check(infanta.Creature.CurrentHp == 60, "语序照卡面:先裸失 20(挂反会挡自己入场费)", $"hp {infanta.Creature.CurrentHp}");

    await CreatureCmd.Damage(s, null, new[] { infanta.Creature }, 10m, ValueProp.Move, null);
    Check(infanta.Creature.CurrentHp == 60, "管线伤 10 → 钳成 0:生命不会降低");
    await CreatureCmd.LoseHp(s, infanta.Creature, 5);
    Check(infanta.Creature.CurrentHp == 60, "裸掉血 5 → 也钳成 0(LoseHp 新针,珊瑚同层)");

    await Hook.AfterTurnStarted(s, CombatSide.Player);              // 你的下回合开始
    Check(infanta.Creature.GetBuff<LastStandBuff>() == null, "钟响:背水撤场");
    await CreatureCmd.LoseHp(s, infanta.Creature, 5);
    Check(infanta.Creature.CurrentHp == 55, "保护结束,血债照付", $"hp {infanta.Creature.CurrentHp}");

    var ls2 = s.CreateCard<LastStand>(infanta);
    ls2.Upgrade();
    Check(ls2.Vars["Loss"].Int == 15, "背水+:失 20→15");
}

// ── 燎原 / 火神领域(附带流水线) ──
{
    var s = new CombatState(new RngSet("c3-firestorm"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var fs = s.CreateCard<Firestorm>(infanta); pcs.Hand.AddInternal(fs);
    var em = s.CreateCard<Ember>(infanta); pcs.Hand.AddInternal(em);
    await CardCmd.Play(s, fs, null);                                // 能量账:2
    Check(em.Cost == 0, "燎原:余烬 1→0 费(全局费用钩)");
    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, em, null);                                // 免费打出
    Check(h0 - sentry.CurrentHp == 5 && pcs.Hand.Count == 1,
        "余烬附带:5 伤(随机靶=独苗)+ 抽 1(微裁定2)");
    Check(em.Pile == pcs.ExhaustPile, "余烬本性不变:打出即耗");
}

{
    var s = new CombatState(new RngSet("c3-domain"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);
    var sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
    await CombatCmd.StartCombat(s, infanta);
    var pcs = infanta.PlayerCombatState!;
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    pcs.DrawPile.AddInternal(s.CreateCard<Attack>(infanta));
    var d1 = s.CreateCard<Dash>(infanta); pcs.Hand.AddInternal(d1);
    var d2 = s.CreateCard<Dash>(infanta); pcs.Hand.AddInternal(d2);
    var fd = s.CreateCard<FiregodsDomain>(infanta); pcs.Hand.AddInternal(fd);

    await CardCmd.Play(s, fd, null);                            // 能量账:3 ✓
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 0 && pcs.ExhaustPile.Count == 0
          && !sentry.HasBuff<SearBuff>() && pcs.Hand.Count == 2,
        "领域开张不自触发(裁定13:起点不在场,登记簿无名——群蛇形态判例)");

    int h0 = sentry.CurrentHp;
    await CardCmd.Play(s, d2, sentry);                          // 0 费;焚毁池=唯一的 d1,自动路,零选择器
    Check(h0 - sentry.CurrentHp == 5, "突进:3+2×1(领域那张),素 5——灼伤在触发里、伤害之后", $"{h0 - sentry.CurrentHp}");
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 1 && pcs.ExhaustPile.Count == 1
          && sentry.GetBuff<SearBuff>() is { Amount: 1, Level: 1 } && pcs.Hand.Count == 1,
        "第一拍流水线:焚 1(d1)/抽 1/+1 力/独苗 1 层灼");

    await CombatCmd.GainEnergy(s, infanta, 3);                  // 能量账:第二张领域的 3 费
    var fd2 = s.CreateCard<FiregodsDomain>(infanta); pcs.Hand.AddInternal(fd2);
    await CardCmd.Play(s, fd2, null);
    Check(infanta.Creature.GetBuffAmount<StrengthBuff>() == 2 && pcs.ExhaustPile.Count == 2
          && sentry.GetBuff<SearBuff>()!.Amount == 2
          && infanta.Creature.GetBuffAmount<FiregodDomainAura>() == 2,
        "第二张领域:旧的一层照常触发(起点在场),且按起点快照×1 而非合并后×2;本体叠到 2 层");
}

// ── 升级収尾抽查(本批余下诸卡) ──
{
    var s = new CombatState(new RngSet("c3-upg"));
    var infanta = new Player("Infanta", 80);
    s.AddPlayer(infanta);

    var t = s.CreateCard<Tinder>(infanta); t.Upgrade();
    Check(t.Vars["Cards"].Int == 2, "焚牌+:抽 1→2");
    var tf = s.CreateCard<ThrowFire>(infanta); tf.Upgrade();
    Check(tf.Vars.Damage.Int == 15, "掷火+:12→15");
    var fu = s.CreateCard<FireUp>(infanta); fu.Upgrade();
    Check(fu.Vars["Bonus"].Int == 2, "蓄力+:额外 1→2");
    var ai = s.CreateCard<AllIn>(infanta); ai.Upgrade();
    Check(ai.Vars.Damage.Int == 12, "孤注+:9→12");
    var pk = s.CreateCard<Powderkeg>(infanta); pk.Upgrade();
    Check(pk.Cost == 0, "油库引爆+:1→0 费");
    var fo = s.CreateCard<Flashover>(infanta); fo.Upgrade();
    Check(fo.Vars.Damage.Int == 24, "爆燃+:19→24");
    var sea = s.CreateCard<SeaOfFire>(infanta); sea.Upgrade();
    Check(sea.Vars["Sear"].Int == 9, "火海+:6→9");
    var of2 = s.CreateCard<OilFlash>(infanta); of2.Upgrade();
    Check(of2.Vars.Damage.Int == 3, "油焰引爆+:2→3");
    var te = s.CreateCard<Tephra>(infanta); te.Upgrade();
    Check(te.Vars.Damage.Int == 12, "火山灰+:9→12");
    var ash = s.CreateCard<AshShield>(infanta); ash.Upgrade();
    Check(ash.Vars["Shield"].Int == 14, "灰烬护盾+:11→14");
    var al = s.CreateCard<ALight>(infanta); al.Upgrade();
    Check(al.HasKeyword(CardKeyword.Retain), "借火+:加「保留」");
    var af = s.CreateCard<Ashfall>(infanta); af.Upgrade();
    Check(af.Vars.Damage.Int == 34, "余烬倾泻+:28→34");
    var uis = s.CreateCard<UpInSmoke>(infanta); uis.Upgrade();
    Check(uis.Vars.Damage.Int == 11, "灰飞烟灭+:8→11");
    var fst = s.CreateCard<Firestorm>(infanta); fst.Upgrade();
    Check(fst.Cost == 1, "燎原+:2→1 费");
    var fz = s.CreateCard<Frenzy>(infanta); fz.Upgrade();
    Check(fz.Vars["Str"].Int == 4, "狂战+:3→4 力");
    var on = s.CreateCard<Onslaught>(infanta); on.Upgrade();
    Check(on.Cost == 15, "无双乱舞+:20→15(素体口径,无史书)");
    var mf = s.CreateCard<Mirrorflame>(infanta); mf.Upgrade();
    Check(mf.Vars.Damage.Int == 9, "镜炎+:6→9");
    var fe = s.CreateCard<Fuse>(infanta); fe.Upgrade();
    Check(fe.Vars.Damage.Int == 9 && fe.Vars["FuelDamage"].Int == 9, "引火索+:6/6→9/9");
    var cw = s.CreateCard<Cordwood>(infanta); cw.Upgrade();
    Check(cw.HasKeyword(CardKeyword.Retain), "薪柴+:加「保留」");
    var gp = s.CreateCard<GreasePot>(infanta); gp.Upgrade();
    Check(gp.HasKeyword(CardKeyword.Retain), "油脂弹+:加「保留」");
    var ut = s.CreateCard<UnquenchedTorch>(infanta); ut.Upgrade();
    Check(ut.HasKeyword(CardKeyword.Retain), "不熄之炬+:加「保留」");
    var udf = s.CreateCard<UndyingFlame>(infanta); udf.Upgrade();
    Check(udf.HasKeyword(CardKeyword.Innate), "薪火长明+:加「本能」");
    var fdm = s.CreateCard<FiregodsDomain>(infanta); fdm.Upgrade();
    Check(fdm.HasKeyword(CardKeyword.Innate), "火神领域+:加「本能」");
    var sg = s.CreateCard<Surge>(infanta); sg.Upgrade();
    Check(sg.Vars.Damage.Int == 15, "狂涌+:12→15");
    var cfl = s.CreateCard<Conflagration>(infanta); cfl.Upgrade();
    Check(cfl.Cost == 2, "狂炎引爆+:3→2 费");
}
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
