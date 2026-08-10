using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel;

/// <summary>
/// 一场战斗的全景根：名册 + CombatId 发号机 + 事件流 + hook 监听名单。
/// 回合推进（RoundNumber/CurrentSide 的流转）是 Step E 的事，本批只有字段。
/// （STS2 另有 _allCards 总登记服务网络同步，单机不需要，暂省。）
/// </summary>
public sealed class CombatState
{
    private readonly List<Creature> _allies = new();
    private readonly List<Creature> _enemies = new();
    private uint _nextCreatureId = 1;

    public CombatEvents Events { get; } = new();

    public int RoundNumber { get; internal set; } = 1;
    public CombatSide CurrentSide { get; internal set; } = CombatSide.Player;

    public IReadOnlyList<Creature> Allies => _allies;
    public IReadOnlyList<Creature> Enemies => _enemies;

    /// <summary>全体，顺序恒为：友方（按入场序）→ 敌方（按入场序）。</summary>
    public IEnumerable<Creature> Creatures => _allies.Concat(_enemies);

    // ══ 入场 ══

    public void AddPlayer(Player player)
    {
        if (player.PlayerCombatState != null)
            throw new InvalidOperationException($"{player} 已在战斗中。");
        player.PlayerCombatState = new PlayerCombatState(player);
        Attach(player.Creature, _allies);
    }

    /// <summary>怪物入场。maxHp 由调用方掷好传入（Step B 接 MonsterHp 流后由开战流程包掉）。</summary>
    public Creature CreateCreature(MonsterModel monster, CombatSide side, int maxHp)
    {
        var creature = new Creature(monster, side, maxHp);   // ctor 已挡 canonical
        Attach(creature, side == CombatSide.Player ? _allies : _enemies);
        return creature;
    }

    /// <summary>同伴入场 = 玩家侧怪物 + 挂到主人的同伴名单。召唤全流程（事件/表现）Step C。</summary>
    public Creature AddPet(MonsterModel monster, Player owner, int maxHp)
    {
        if (owner.PlayerCombatState == null)
            throw new InvalidOperationException($"主人 {owner} 不在战斗中，不能给他添同伴。");
        Creature pet = CreateCreature(monster, CombatSide.Player, maxHp);
        owner.PlayerCombatState.AddPetInternal(pet);
        return pet;
    }

    private void Attach(Creature creature, List<Creature> sideList)
    {
        if (creature.CombatState != null)
            throw new InvalidOperationException($"{creature} 已经在一场战斗里了。");
        creature.CombatState = this;
        creature.CombatId = _nextCreatureId++;   // 【确定性】入场顺序发号，永不复用
        sideList.Add(creature);
    }
    
    /// <summary>死亡/离场时移出名册。CombatId 不清除、不复用——事件日志里留痕。
    /// 同伴尸体仍留在主人的 PlayerCombatState.Pets 里（Step C 复活用）。</summary>
    public void RemoveCreature(Creature creature)
    {
        if (!_allies.Remove(creature) && !_enemies.Remove(creature))
            throw new InvalidOperationException($"{creature} 不在本场战斗名册里。");
    }

    // ══ 造牌 ══

    /// <summary>战斗内造牌的正门：取 mutable 副本 + 登记归属。（进哪个堆由调用方决定。）</summary>
    public T CreateCard<T>(Player owner) where T : CardModel
    {
        T card = ModelRegistry.New<T>();
        card.SetOwner(owner);
        return card;
    }

    // ══ 查询 ══

    public Creature? GetCreature(uint combatId)
        => Creatures.FirstOrDefault(c => c.CombatId == combatId);

    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side)
        => side == CombatSide.Player ? Allies : Enemies;

    public IReadOnlyList<Creature> GetOpponentsOf(Creature creature)
        => creature.Side == CombatSide.Player ? Enemies : Allies;

    public IEnumerable<Creature> GetTeammatesOf(Creature creature)
        => GetCreaturesOnSide(creature.Side).Where(c => c != creature);

    // ══ hook 监听名单 ══

    /// <summary>
    /// 谁能听到 hook、按什么顺序听（对应 STS2 IterateHookListeners）。
    /// 【顺序即数值语义】先加力量还是先乘易伤，结果不同——顺序写死并有断言钉住：
    ///
    ///   逐生物（友方按入场序 → 敌方按入场序）：
    ///     该生物的 buff（按获得序）
    ///     玩家 → 六个堆里的每张牌（堆序 = AllPiles 序）+ 牌上的附魔/附疫
    ///     怪物/同伴 → 它的 MonsterModel
    ///
    /// 【你的设计决定】Removed 堆在名单里：移出区的牌被动仍生效。
    /// （STS2 在玩家段还有 遗物→药水→充能球，等那些系统落地时插回对应位置。）
    /// </summary>
    public IEnumerable<GameModel> IterateHookListeners()
    {
        foreach (Creature creature in Creatures)
        {
            foreach (BuffModel buff in creature.Buffs)
                yield return buff;

            if (creature.Player != null)
            {
                PlayerCombatState? pcs = creature.Player.PlayerCombatState;
                if (pcs == null) continue;
                foreach (CardPile pile in pcs.AllPiles)
                    foreach (CardModel card in pile.Cards)
                    {
                        yield return card;
                        if (card.Enchantment != null) yield return card.Enchantment;
                        if (card.Affliction != null) yield return card.Affliction;
                    }
            }
            else if (creature.Monster != null)
            {
                yield return creature.Monster;
            }
        }
    }
}