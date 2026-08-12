using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel;
using Kernel.Content.Cards;
using Kernel.Content.Monsters;
using Kernel.Content.Familiars;
using Kernel.Content.Pledges;

public partial class NManualBattle : Control
{
    private const string Seed = "edeed";

    // ── 阵容:想打谁、打几只,在这排(§4)──
    private static readonly Func<MonsterModel>[] Lineup =
    {
        () => ModelRegistry.New<Wildfire>(),   // 左:野火灵(先动)
        () => ModelRegistry.New<Kindler>(),    // 右:拾柴人(后动)
    };

    private Label _playerStatus = null!;
    private VBoxContainer _enemiesArea = null!;
    private Label _verdict = null!;
    private RichTextLabel _logText = null!;
    private HBoxContainer _handArea = null!;
    private Button _startButton = null!;
    private Button _endTurnButton = null!;

    private CombatState? _state;
    private Player? _infanta;
    private readonly Dictionary<Creature, Button> _enemyRows = new();
    private readonly Dictionary<Creature, string> _intents = new();
    private CardModel? _pendingCard;      // 选目标中的单体卡
    private bool _resolving;              // 结算围栏

    public override void _Ready()
    {
        if (ModelRegistry.Count == 0)
            ModelRegistry.RegisterAllInAssembly(typeof(CardModel).Assembly);

        _playerStatus  = GetNode<Label>("Layout/PlayerStatus");
        _enemiesArea   = GetNode<VBoxContainer>("Layout/EnemiesArea");
        _verdict       = GetNode<Label>("Layout/Verdict");
        _logText       = GetNode<RichTextLabel>("Layout/LogText");
        _handArea      = GetNode<HBoxContainer>("Layout/HandArea");
        _startButton   = GetNode<Button>("Layout/Buttons/StartButton");
        _endTurnButton = GetNode<Button>("Layout/Buttons/EndTurnButton");

        _startButton.Pressed   += OnStartPressed;
        _endTurnButton.Pressed += OnEndTurnPressed;

        Fx.Backend        = new GodotFxBackend(this);
        Fx.OnWarn         = msg => GD.PushWarning(msg);
        TaskHelper.OnWarn = msg => GD.PushWarning(msg);
    }

    private async void OnStartPressed()
    {
        _startButton.Disabled = true;
        _logText.Text = "";
        _pendingCard = null;
        foreach (Button row in _enemyRows.Values) row.QueueFree();
        _enemyRows.Clear();
        _intents.Clear();

        var s = new CombatState(new RngSet(Seed));
        var infanta = new Player("Infanta", 80);
        infanta.AddFamiliarInternal(ModelRegistry.New<Flamedance>());
        infanta.AddPledgeInternal(ModelRegistry.New<VermeilSeal>());
        s.AddPlayer(infanta);
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Attack>());
        for (int i = 0; i < 5; i++) infanta.Deck.AddInternal(ModelRegistry.New<Defend>());
        infanta.Deck.AddInternal(ModelRegistry.New<Ignite>());
        infanta.Deck.AddInternal(ModelRegistry.New<Shiftstep>());
        _state = s; _infanta = infanta;

        // ── 阵容入场:逐敌建行、逐敌订铃(入场序 = 站位序 = 行动序)──
        foreach (Func<MonsterModel> make in Lineup)
        {
            Creature enemy = CombatCmd.SpawnEnemy(s, make());
            _intents[enemy] = "—";
            var row = new Button { Alignment = HorizontalAlignment.Left };
            Creature captured = enemy;
            row.Pressed += () => OnEnemyPressed(captured);
            _enemiesArea.AddChild(row);
            _enemyRows[enemy] = row;

            enemy.CurrentHpChanged += (_, _) => RefreshEnemyRow(captured);
            enemy.BlockChanged     += (_, _) => RefreshEnemyRow(captured);
            enemy.BuffApplied      += _ => RefreshEnemyRow(captured);
            enemy.BuffIncreased    += (_, _, _) => RefreshEnemyRow(captured);
            enemy.BuffDecreased    += (_, _) => RefreshEnemyRow(captured);
            enemy.BuffRemoved      += _ => RefreshEnemyRow(captured);
            RefreshEnemyRow(captured);
        }

        infanta.Creature.CurrentHpChanged        += (_, _) => RefreshPlayer();
        infanta.Creature.BlockChanged            += (_, _) => RefreshPlayer();
        infanta.Creature.BuffApplied             += _ => RefreshPlayer();
        infanta.Creature.BuffIncreased           += (_, _, _) => RefreshPlayer();
        infanta.Creature.BuffDecreased           += (_, _) => RefreshPlayer();
        infanta.Creature.BuffRemoved             += _ => RefreshPlayer();
        infanta.PlayerCombatState!.EnergyChanged += (_, _) => RefreshPlayer();

        s.Events.OnEvent += ev =>
        {
            if (ev is MonsterIntentRolled) UpdateIntents();
            if (ev is CreatureDied died && _enemyRows.ContainsKey(died.Creature))
                RefreshEnemyRow(died.Creature);
            _logText.AppendText(ev.ToLogLine() + "\n");
        };

        RefreshPlayer();
        _resolving = true;
        await CombatCmd.StartCombat(s, infanta);
        _resolving = false;
        _verdict.Text = $"你的回合（第 {s.RoundNumber} 回合）。";
        RebuildHand();
        _endTurnButton.Disabled = false;
    }

    // ── 出牌与选目标 ──

    private async void OnCardPressed(CardModel card)
    {
        if (_resolving || _state is not { IsOver: false } s) return;

        if (card.TargetType != TargetType.SingleEnemy)
        {
            _pendingCard = null;                                  // 直出牌顺手清掉待选
            await ResolveCard(card, null);
            return;
        }

        List<Creature> living = s.Enemies.Where(e => e.IsAlive).ToList();
        if (living.Count == 0) return;
        if (living.Count == 1)                                    // 单敌:自动锁定,无摩擦
        {
            _pendingCard = null;
            await ResolveCard(card, living[0]);
            return;
        }

        _pendingCard = card;                                      // 多敌:进入选目标
        _verdict.Text = $"为「{card.Title}」选择目标——点击一名敌人（点其他手牌可换弹）";
    }

    private async void OnEnemyPressed(Creature enemy)
    {
        if (_resolving || _pendingCard is not { } card) return;
        if (_state is not { IsOver: false } || enemy.IsDead) return;
        _pendingCard = null;
        await ResolveCard(card, enemy);
    }

    private async Task ResolveCard(CardModel card, Creature? target)
    {
        if (_state is not { IsOver: false } s) return;
        SetResolving(true);
        await CardCmd.Play(s, card, target);
        CombatCmd.CheckEnd(s);
        SetResolving(false);
        if (s.IsOver) { Finish(); return; }
        _verdict.Text = $"你的回合（第 {s.RoundNumber} 回合）。";
        RebuildHand();
        UpdateIntents();
    }

    private async void OnEndTurnPressed()
    {
        if (_resolving || _infanta == null || _state is not { IsOver: false } s) return;
        _pendingCard = null;
        SetResolving(true);
        ClearHand();
        _verdict.Text = "敌方回合……";
        await CombatCmd.EndPlayerTurn(s, _infanta);
        CombatCmd.CheckEnd(s);
        await CombatCmd.EnemyTurn(s);
        if (!s.IsOver) await CombatCmd.StartPlayerTurn(s, _infanta);
        SetResolving(false);
        if (s.IsOver) { Finish(); return; }
        _verdict.Text = $"你的回合（第 {s.RoundNumber} 回合）。";
        RebuildHand();
    }

    // ── 显示 ──

    private void RebuildHand()
    {
        ClearHand();
        if (_state is not { IsOver: false } || _infanta?.PlayerCombatState is not { } pcs) return;
        foreach (CardModel card in pcs.Hand.Cards)
        {
            var b = new Button
            {
                Text        = card.HasCost ? $"{card.Title}｜{card.Cost} 费" : card.Title,
                TooltipText = card.Describe(),
                Disabled    = !card.CanPlay(),
            };
            b.Pressed += () => OnCardPressed(card);
            _handArea.AddChild(b);
        }
    }

    private void ClearHand()
    {
        foreach (Node child in _handArea.GetChildren())
            child.QueueFree();
    }

    private void SetResolving(bool on)
    {
        _resolving = on;
        _endTurnButton.Disabled = on;
        foreach (Button b in _handArea.GetChildren().OfType<Button>())
            b.Disabled = on || b.Disabled;
    }

    private void UpdateIntents()
    {
        if (_state is not { } s) return;
        foreach (Creature enemy in _enemyRows.Keys)
        {
            if (enemy.IsDead || enemy.Monster?.NextMove is not { } move) continue;
            int dmg  = enemy.Monster.IntentPreviewDamage(s);
            int hits = enemy.Monster.IntentPreviewHits(s);
            _intents[enemy] = dmg > 0
                ? (hits > 1 ? $"{move.Name}({dmg}×{hits})" : $"{move.Name}({dmg})")
                : move.Name;
            RefreshEnemyRow(enemy);
        }
    }

    private void RefreshEnemyRow(Creature enemy)
    {
        if (!_enemyRows.TryGetValue(enemy, out Button? row)) return;
        if (enemy.IsDead)
        {
            row.Text = $"{enemy.Name}    已倒下";
            row.Disabled = true;
            return;
        }
        row.Text = $"{enemy.Name}    HP {enemy.CurrentHp}/{enemy.MaxHp}    盾 {enemy.Block}    意图 {_intents[enemy]}{BuffLine(enemy)}";
    }

    private void RefreshPlayer()
    {
        if (_infanta is not { } inf) return;
        PlayerCombatState? pcs = inf.PlayerCombatState;
        _playerStatus.Text = pcs == null
            ? "王女    —"
            : $"王女    HP {inf.Creature.CurrentHp}/{inf.Creature.MaxHp}    盾 {inf.Creature.Block}    能量 {pcs.Energy}/{pcs.MaxEnergy}{BuffLine(inf.Creature)}";
    }

    private static string BuffLine(Creature c)
        => c.Buffs.Count == 0 ? "" : "    [" + string.Join("  ", c.Buffs.Select(b => b.Describe())) + "]";

    private void Finish()
    {
        if (_state is not { } s) return;
        _pendingCard = null;
        _verdict.Text = s.Victory
            ? $"胜利 ✔   第 {s.RoundNumber} 回合终局，王女 HP {_infanta!.Creature.CurrentHp}/80——这局是你亲手打的。"
            : $"落败 ✘   第 {s.RoundNumber} 回合终局。同种子重开,换个打法。";
        ClearHand();
        _endTurnButton.Disabled = true;
        _startButton.Text = "重开一局（同种子）";
        _startButton.Disabled = false;
    }
}