using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel;

/// <summary>一张牌能待的全部位置。战斗六堆 + 局外 Deck + 哨兵 None。</summary>
public enum PileType
{
    /// <summary>"不去任何堆"的哨兵（同 STS2）：作目的地参数时表示丢弃/无处可去。</summary>
    None,
    Draw,       // 抽牌堆
    Hand,       // 手牌
    Discard,    // 弃牌堆
    Exhaust,    // 消耗堆 —— 被消耗的牌仍在听 hook（STS2 语义，遗言/回收类效果的前提）
    Play,       // 结算堆 —— 卡"正在打出"期间的物理隔离位：既不在手牌也不在弃牌堆，
                //          防止"打出时抽牌/洗牌"这类效果把自己卷进去
    /// <summary>移出区（你的设计，STS2 没有）：卡暂时离场——抽不到、选不到、
    /// 弃不掉，但【被动仍然生效】（hook 名单包含它，设计故意）。战斗结束回牌库。</summary>
    Removed,
    Deck,       // 局外牌库 —— 整局层，不参与战斗
}

/// <summary>
/// 一摞有序的牌。职责就三件：维护顺序、执行"每张牌任意时刻恰好在一个堆"的
/// 铁律、提供查询。跨堆移动的完整语义（去哪、触发什么）住在 CardPileCmd（Step D）。
///
/// 【偏离 STS2 #3 的体现】STS2 的 CardPile 挂了 5 个 C# 事件给 UI（CardAdded 等），
/// 我们不挂——牌的进出由 Cmd 层发进事件流。
/// </summary>
public sealed class CardPile
{
    /// <summary>手牌上限。STS2 在 4 个地方硬编码 10，我们收拢成一处。
    /// 执法点不在这（AddInternal 不查）——"抽满了不抽""生成进满手改进弃牌堆"
    /// 这些政策在 CardPileCmd（Step D）。</summary>
    public const int MaxCardsInHand = 10;

    private readonly List<CardModel> _cards = new();

    public PileType Type { get; }

    public CardPile(PileType type) => Type = type;

    /// <summary>约定：下标 0 是堆顶（抽牌从 0 抽，同 STS2 MoveToTop=Insert(0)）。</summary>
    public IReadOnlyList<CardModel> Cards => _cards;
    public int Count => _cards.Count;
    public bool IsEmpty => _cards.Count == 0;
    
    //// <summary>粗粒度门铃：集合【成员】变化时响（加/移）；顺序变动不响——STS2 原样：
    /// 牌堆顺序是隐藏信息，响了也不该被看见（查看界面按自己的规则重排显示）。
    /// 逐张铃（CardAdded/Removed）、批次铃、silent 参数等雇主。</summary>
    public event Action? ContentsChanged;

    // ══ Internal 层：只改状态，不跑 hook，不发事件 ══

    public void AddInternal(CardModel card, int index = -1)
    {
        if (!card.IsMutable)
            throw new InvalidOperationException(
                $"{card.Id} 是 canonical 定义，不能进牌堆——牌堆里只放运行时副本。");
        if (card.Pile != null)
            throw new InvalidOperationException(
                $"{card.Id} 已经在 {card.Pile.Type} 堆里。铁律：每张牌任意时刻恰好在一个堆——" +
                "先 RemoveInternal 再 AddInternal。");
        if (_cards.Contains(card))
            throw new InvalidOperationException($"{Type} 堆已包含这张 {card.Id}（状态不一致，必有 bug）。");

        if (index >= 0) _cards.Insert(index, card);
        else _cards.Add(card);
        card.SetPile(this);
        ContentsChanged?.Invoke();
    }

    public void RemoveInternal(CardModel card)
    {
        if (card.Pile != this || !_cards.Remove(card))
            throw new InvalidOperationException($"{Type} 堆里没有这张 {card.Id}。");
        card.SetPile(null);
        ContentsChanged?.Invoke();
        // 移动 = Remove + Add，两步之间 Pile 短暂为 null——这发生在单个 Cmd 内部，
        // 没有观测点，不算破坏铁律。
    }

    public void MoveToTopInternal(CardModel card)
    {
        if (card.Pile != this) throw new InvalidOperationException($"{card.Id} 不在 {Type} 堆。");
        _cards.Remove(card);
        _cards.Insert(0, card);
    }

    public void MoveToBottomInternal(CardModel card)
    {
        if (card.Pile != this) throw new InvalidOperationException($"{card.Id} 不在 {Type} 堆。");
        _cards.Remove(card);
        _cards.Add(card);
    }

    /// <summary>洗牌。RNG 从外面传进来——这个类自己永远不碰随机源（流的选择是调用方的责任）。</summary>
    public void ShuffleInternal(Rng rng) => rng.ShuffleInPlace(_cards);

    // ══ 静态查询 ══

    /// <summary>枚举 → 具体堆的唯一查表（同 STS2）。None 返回 null。</summary>
    public static CardPile? Get(PileType type, Player player) => type switch
    {
        PileType.None    => null,
        PileType.Draw    => player.PlayerCombatState?.DrawPile,
        PileType.Hand    => player.PlayerCombatState?.Hand,
        PileType.Discard => player.PlayerCombatState?.DiscardPile,
        PileType.Exhaust => player.PlayerCombatState?.ExhaustPile,
        PileType.Play    => player.PlayerCombatState?.PlayPile,
        PileType.Removed => player.PlayerCombatState?.RemovedPile,
        PileType.Deck    => player.Deck,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>
    /// 选牌查询：调用方【点名】要哪几个堆（同 STS2 GetCards）。
    /// "移出区的牌不能被选中"就在这实现——不点 Removed 就选不到，
    /// 不需要任何特判。
    /// </summary>
    public static IEnumerable<CardModel> GetCards(Player player, params PileType[] piles)
        => piles.Select(p => Get(p, player))
                .Where(p => p != null)
                .SelectMany(p => p!.Cards);
}