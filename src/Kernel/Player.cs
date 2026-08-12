using System;
using System.Collections.Generic;

namespace Kernel;

/// <summary>
/// 一个玩家【整局】活着的部分。对照：PlayerCombatState（下一批）只活一场战斗，
/// 战斗结束整个扔掉——"忘了重置手牌/能量"这类 bug 被这个划分结构性消灭。
///
/// 本批是最小骨架：牌库、遗物、金币等跑图层字段，等 CardPile 与跑图层再加。
/// </summary>
public sealed class Player
{
    public string Name { get; }

    /// <summary>玩家在战斗里的"身体"。整局同一个——血量跨战斗延续的原因。</summary>
    public Creature Creature { get; }

    public int MaxEnergy { get; internal set; } = 3;
    /// <summary>每回合抽牌基数（默认 5，Infanta 亦用默认）。战斗中的临时增减走 ModifyHandDraw
    /// hook；这里只放"这个角色天生抽几张"。public set：开局配置由 run 层/harness 直接赋。</summary>
    public int BaseHandDraw { get; set; } = 5;
    /// <summary>整局的牌库（局外堆）。开战时它是抽牌堆的复制来源（Step E）。</summary>
    public CardPile Deck { get; } = new CardPile(PileType.Deck);
    
    private readonly List<FamiliarModel> _familiars = new();

    /// <summary>眷属列表（run 寿命，跨战斗）。眷属不是生物——不进战斗名册，只进 hook 名单。
    /// 移除眷属 = 从这里摘除 → 自动停听（名单寿命=监听寿命，铁律的又一次应用）。</summary>
    public IReadOnlyList<FamiliarModel> Familiars => _familiars;

    /// <summary>裸挂载。run 层"获得眷属"的动词（连同绑定卡入库）将来包它。</summary>
    public void AddFamiliarInternal(FamiliarModel familiar)
    {
        if (!familiar.IsMutable)
            throw new ArgumentException(
                $"{familiar.Id} 是 canonical 定义。用 ModelRegistry.New<{familiar.GetType().Name}>() 取副本。");
        if (familiar.Owner != null)
            throw new InvalidOperationException($"{familiar.Id} 已有主人 {familiar.Owner}。");
        familiar.Owner = this;
        _familiars.Add(familiar);
    }
    
    private readonly List<PledgeModel> _pledges = new();

    /// <summary>信物列表（run 寿命，跨战斗）。结构同眷属：只进 hook 名单。</summary>
    public IReadOnlyList<PledgeModel> Pledges => _pledges;

    /// <summary>裸挂载。run 层"获得信物"的动词将来包它。</summary>
    public void AddPledgeInternal(PledgeModel pledge)
    {
        if (!pledge.IsMutable)
            throw new ArgumentException(
                $"{pledge.Id} 是 canonical 定义。用 ModelRegistry.New<{pledge.GetType().Name}>() 取副本。");
        if (pledge.Owner != null)
            throw new InvalidOperationException($"{pledge.Id} 已有主人 {pledge.Owner}。");
        pledge.Owner = this;
        _pledges.Add(pledge);
    }

    /// <summary>本场战斗的战斗态。由 CombatState.AddPlayer 创建；战斗结束整个
    /// 丢掉置 null——"忘了重置"型 bug 的结构性消灭点。</summary>
    public PlayerCombatState? PlayerCombatState { get; internal set; }
    
    /// <summary>我的同伴（单同伴规则：档案里第一只，含尸体）。对应 STS2 的 Osty 三件套。</summary>
    public Creature? Pet
    {
        get
        {
            var pets = PlayerCombatState?.Pets;
            return pets != null && pets.Count > 0 ? pets[0] : null;
        }
    }

    public bool IsPetAlive => Pet?.IsAlive ?? false;
    public bool IsPetMissing => !IsPetAlive;
    
    public Player(string name, int maxHp)
    {
        Name = name;
        Creature = new Creature(this, maxHp);
    }

    public override string ToString() => Name;
}