using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel;
using Kernel.Content.Cards;
using Kernel.Content.Monsters;
using Kernel.Content.Familiars;
using Kernel.Content.Pledges;

public partial class NSmokeTest : Control
{
    private Button _runButton = null!;
    private Label _verdict = null!;
    private Label _playerStatus = null!;
    private Label _enemyStatus = null!;
    private RichTextLabel _logText = null!;
    private GodotFxBackend _backend = null!;

    public override void _Ready()
    {
        // 内容注册:每进程一次(headless 台架同款;场景重载再进 _Ready 时用注册表状态守门)
        if (ModelRegistry.Count == 0)
            ModelRegistry.RegisterAllInAssembly(typeof(CardModel).Assembly);

        _runButton    = GetNode<Button>("Layout/RunButton");
        _verdict      = GetNode<Label>("Layout/Verdict");
        _playerStatus = GetNode<Label>("Layout/PlayerStatus");
        _enemyStatus  = GetNode<Label>("Layout/EnemyStatus");
        _logText      = GetNode<RichTextLabel>("Layout/LogText");
        _runButton.Pressed += OnRunPressed;
        _backend = new GodotFxBackend(this);

        Fx.OnWarn         = msg => GD.PushWarning(msg);
        TaskHelper.OnWarn = msg => GD.PushWarning(msg);
    }

    private async void OnRunPressed()
    {
        _runButton.Disabled = true;
        _logText.Text = "";

        // 第一遍:瞬跑拿对账基准(Backend=null → 全部停顿立即完成,正是 headless 模式)
        _verdict.Text = "对账基准(瞬跑)……";
        Fx.Backend = null;
        List<string> baseline = (await RunBattle(toUi: false)).lines;

        // 第二遍:实速逐条演出(同种子)
        _verdict.Text = "实速演出中……";
        Fx.Backend = _backend;
        var (lines, state) = await RunBattle(toUi: true);
        Fx.Backend = null;

        // 节奏对账:内核铁律"Fx 出去的信息永不回来"的直接检验
        bool match = baseline.SequenceEqual(lines);
        _verdict.Text =
            (state.Victory ? "胜利 ✔" : "落败 ✘") +
            $"   第 {state.RoundNumber} 回合终局,事件 {lines.Count} 条" +
            $"   节奏对账 {(match ? "✔(瞬跑=实速,逐字相同)" : "✘ 两跑不一致!截图发工头")}";
        _runButton.Disabled = false;
    }

    private async Task<(List<string> lines, CombatState state)> RunBattle(bool toUi)
    {
        // ── 开局:设计表①全套(5 抽新基线),与 headless [12] 同一副 ──
        var s = new CombatState(new RngSet("SEED-1"));
        var infanta = new Player("Infanta", 80);
        infanta.AddFamiliarInternal(ModelRegistry.New<Flamedance>());
        infanta.AddPledgeInternal(ModelRegistry.New<VermeilSeal>());
        s.AddPlayer(infanta);
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Defend>());
        infanta.Deck.AddInternal(ModelRegistry.New<Ignite>());
        infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
        Creature sentry = CombatCmd.SpawnEnemy(s, ModelRegistry.New<QuietusSentry>());
        PlayerCombatState pcs = infanta.PlayerCombatState!;

        var lines = new List<string>();
        string intent = "—";

        if (toUi)
        {
            // ── 门铃 → 状态条([13] 的铃第一次上产品岗。订阅纪律:只读,恒不改状态;
            //    本批订阅者与战斗对象同生共死,免退订——真 UI 进出树时必须成对退订)──
            void Refresh()
            {
                _playerStatus.Text =
                    $"王女    HP {infanta.Creature.CurrentHp}/{infanta.Creature.MaxHp}    盾 {infanta.Creature.Block}    能量 {pcs.Energy}/{pcs.MaxEnergy}";
                _enemyStatus.Text = sentry.IsAlive
                    ? $"哨兵    HP {sentry.CurrentHp}/{sentry.MaxHp}    盾 {sentry.Block}    意图 {intent}"
                    : "哨兵    已倒下";
            }

            infanta.Creature.CurrentHpChanged += (_, _) => Refresh();
            infanta.Creature.BlockChanged     += (_, _) => Refresh();
            sentry.CurrentHpChanged           += (_, _) => Refresh();
            sentry.BlockChanged               += (_, _) => Refresh();
            pcs.EnergyChanged                 += (_, _) => Refresh();
            s.Events.OnEvent += ev =>
            {
                if (ev is MonsterIntentRolled mi)
                {
                    intent = mi.PreviewDamage > 0 ? $"{mi.MoveName}({mi.PreviewDamage})" : mi.MoveName;
                    Refresh();
                }
            };
            Refresh();
        }

        s.Events.OnEvent += ev =>
        {
            string line = ev.ToLogLine();
            lines.Add(line);
            if (toUi) _logText.AppendText(line + "\n");
        };

        await CombatCmd.StartCombat(s, infanta);
        while (!s.IsOver && s.RoundNumber <= 30)
        {
            for (int plays = 0; plays < 13 && !s.IsOver; plays++)
            {
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
        return (lines, s);
    }
}
