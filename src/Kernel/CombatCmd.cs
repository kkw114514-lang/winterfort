using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>回合循环动词层。流程 = 对照表钉死的 v2，每步注明 STS2 出处。</summary>
public static class CombatCmd
{

    /// <summary>开战：牌库逐张克隆入抽牌堆（STS2 PopulateCombatState 同构；
    /// DeckVersion 回链挂账）→ 洗牌 → 第一个玩家回合。</summary>
    public static async Task StartCombat(CombatState state, Player player)
    {
        PlayerCombatState pcs = player.PlayerCombatState
            ?? throw new InvalidOperationException("先 CombatState.AddPlayer 再 StartCombat。");

        foreach (CardModel deckCard in player.Deck.Cards)
        {
            var copy = (CardModel)deckCard.MutableClone();
            copy.SetOwner(player);
            pcs.DrawPile.AddInternal(copy);
        }
        pcs.DrawPile.ShuffleInternal(state.RngSet[RngStream.Shuffle]);
        await StartPlayerTurn(state, player);
    }

    /// <summary>怪物入场：血量用 MonsterHp 流在 [Min,Max] 掷定。</summary>
    public static Creature SpawnEnemy(CombatState state, MonsterModel monster)
    {
        int hp = monster.MinInitialHp
                 + state.RngSet[RngStream.MonsterHp].NextInt(monster.MaxInitialHp - monster.MinInitialHp + 1);
        return state.CreateCreature(monster, CombatSide.Enemy, hp);
    }

    public static async Task StartPlayerTurn(CombatState state, Player player)
    {
        if (state.IsOver) return;
        state.CurrentSide = CombatSide.Player;
        PlayerCombatState pcs = player.PlayerCombatState!;

        // ① 敌人声明意图（STS2：玩家回合开始 PrepareForNextTurn；召唤即掷归 Step C）
        foreach (Creature enemy in state.Enemies)
        {
            if (enemy.IsDead || enemy.Monster == null) continue;
            enemy.Monster.RollNextMove(state);
            state.Events.Emit(new MonsterIntentRolled
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Creature = enemy, MoveName = enemy.Monster.NextMove!.Name,
                Kind = enemy.Monster.NextMove.Kind,
                PreviewDamage = enemy.Monster.IntentPreviewDamage(state),
            });
        }

        // ② 本方清盾——玩家侧首回合豁免（STS2: round>1 || side!=Player）。
        //    ShouldClearBlock/AfterPreventingBlockClear（回头钳的家）挂账待消费者。
        if (state.RoundNumber > 1)
            ClearSideBlock(state, CombatSide.Player);

        // ③ 能量（ShouldPlayerResetEnergy/冰淇淋类挂账）
        pcs.ResetEnergy();

        // ③′ 回合开始分发（STS2 AfterSideTurnStart 位：能量已重置、还没抽牌——赤金王印的家）
        await Hook.AfterTurnStarted(state, CombatSide.Player);
        if (CheckEnd(state)) return;

        // ④ 抽牌：基数 → hook → 首回合本能置顶 + 抽数保底（STS2 SetupPlayerTurn 原样）
        int n = Hook.ModifyHandDraw(state, player, player.BaseHandDraw, out _);
        if (state.RoundNumber == 1)
        {
            List<CardModel> innate = pcs.DrawPile.Cards
                .Where(c => c.HasKeyword(CardKeyword.Innate)).ToList();
            foreach (CardModel c in innate) pcs.DrawPile.MoveToTopInternal(c);
            n = Math.Min(Math.Max(n, innate.Count), CardPile.MaxCardsInHand);
        }
        await CardPileCmd.Draw(state, player, n);

        state.Events.Emit(new TurnStarted { Round = state.RoundNumber, Side = CombatSide.Player });
    }

    public static async Task EndPlayerTurn(CombatState state, Player player)
    {
        if (state.IsOver) return;
        PlayerCombatState pcs = player.PlayerCombatState!;

        // ① 临时 → 消耗动词（燃料第三途径在此闭环）
        foreach (CardModel card in pcs.Hand.Cards.ToList())
            if (card.HasKeyword(CardKeyword.Temporary))
                await CardPileCmd.Exhaust(state, card);

        // ② Flush（双动词纪律：裸移动——不发 CardDiscarded、不触发遗言）
        var retained = new List<CardModel>();
        var toFlush = new List<CardModel>();
        foreach (CardModel card in pcs.Hand.Cards)
            (card.ShouldRetainThisTurn ? retained : toFlush).Add(card);

        if (toFlush.Count > 0 && Hook.ShouldFlush(state, player, out _))
        {
            foreach (CardModel card in toFlush)
            {
                pcs.Hand.RemoveInternal(card);
                pcs.DiscardPile.AddInternal(card);
            }
            state.Events.Emit(new CardsFlushed
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Player = player, Count = toFlush.Count,
            });
        }
        foreach (CardModel card in retained)
            await Hook.AfterCardRetained(state, card);

        // ③
        await Hook.AfterTurnEnd(state, CombatSide.Player);
        if (CheckEnd(state)) return;
        state.Events.Emit(new TurnEnded { Round = state.RoundNumber, Side = CombatSide.Player });
        state.CurrentSide = CombatSide.Enemy;
    }

    public static async Task EnemyTurn(CombatState state)
    {
        if (state.IsOver) return;
        state.CurrentSide = CombatSide.Enemy;
        state.Events.Emit(new TurnStarted { Round = state.RoundNumber, Side = CombatSide.Enemy });

        ClearSideBlock(state, CombatSide.Enemy);   // 敌方回合开始清自己的盾（side!=Player 恒清）
        await Hook.AfterTurnStarted(state, CombatSide.Enemy);   // 对称分发（同 STS2 双侧都发；敌侧首个雇主待来）
        if (CheckEnd(state)) return;

        foreach (Creature enemy in state.Enemies.ToList())          // 快照：行动中可能死人
        {
            if (state.IsOver) return;
            if (enemy.IsDead || !state.Enemies.Contains(enemy)) continue;
            await enemy.Monster!.TakeTurn(state);
            if (CheckEnd(state)) return;
        }

        await Hook.AfterTurnEnd(state, CombatSide.Enemy);           // ← 灼伤③④、弱化 tick
        if (CheckEnd(state)) return;
        state.Events.Emit(new TurnEnded { Round = state.RoundNumber, Side = CombatSide.Enemy });

        state.RoundNumber++;                                        // STS2：敌→玩切换处 round++
        state.CurrentSide = CombatSide.Player;
    }
    
    /// <summary>能量动词壳（对应 STS2 PlayerCmd.GainEnergy）：裸状态在 PCS，事件发在这。</summary>
    public static async Task GainEnergy(CombatState state, Player player, int amount)
    {
        if (amount <= 0) return;
        player.PlayerCombatState!.GainEnergy(amount);
        state.Events.Emit(new EnergyGained
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Player = player, Amount = amount,
        });
        Fx.Vfx("energy_gain", player.Creature);
        await Fx.CustomScaledWait(0.05f, 0.1f);
    }

    /// <summary>胜负判定：敌全灭→胜；玩家死→负。只发一次 CombatEnded。</summary>
    public static bool CheckEnd(CombatState state)
    {
        if (state.IsOver) return true;
        bool enemyAlive = state.Enemies.Any(e => e.IsAlive);
        bool playerAlive = state.Allies.Any(c => c.IsPlayer && c.IsAlive);
        if (enemyAlive && playerAlive) return false;

        state.IsOver = true;
        state.Victory = playerAlive;
        state.Events.Emit(new CombatEnded
        {
            Round = state.RoundNumber, Side = state.CurrentSide, Victory = state.Victory,
        });
        return true;
    }

    private static void ClearSideBlock(CombatState state, CombatSide side)
    {
        foreach (Creature c in state.GetCreaturesOnSide(side))
        {
            if (c.IsDead || c.Block <= 0) continue;
            int amount = c.LoseBlockInternal(c.Block);
            state.Events.Emit(new BlockCleared
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Target = c, Amount = amount,
            });
        }
    }
}