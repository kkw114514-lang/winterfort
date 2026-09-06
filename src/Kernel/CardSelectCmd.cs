using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>选择请求的参数面板（STS2 CardSelectorPrefs 同款，裁掉本地化与滚金光）。</summary>
public sealed class CardSelectorPrefs
{
    public string Prompt { get; }
    public int MinSelect { get; }
    public int MaxSelect { get; }

    /// <summary>是否必须人工确认。STS2 原式：MinSelect >= 0 &amp;&amp; MinSelect != MaxSelect——
    /// "焚毁恰好 N 张"（min==max）免确认 → 候选不足 N 时自动全拿、不弹 UI。</summary>
    public bool RequireManualConfirmation { get; }

    public CardSelectorPrefs(string prompt, int selectCount)
        : this(prompt, selectCount, selectCount) { }

    public CardSelectorPrefs(string prompt, int minCount, int maxCount)
    {
        Prompt = prompt;
        MinSelect = minCount;
        MaxSelect = maxCount;
        RequireManualConfirmation = minCount >= 0 && minCount != maxCount;
    }
}

/// <summary>答题人。无头 = 脚本选择器（压栈）；Godot = 手牌多选 UI（总装批接 FallbackSelector）。</summary>
public interface ICardSelector
{
    Task<IEnumerable<CardModel>> GetSelectedCards(IReadOnlyList<CardModel> cards, int min, int max);
}

/// <summary>
/// 结算中途的卡牌选择（STS2 CardSelectCmd 判例，燃烧契约的家）。
/// 选择器栈是【测试的正门】：STS2 自家无头测试就靠 UseSelector 压脚本选择器喂答案。
/// </summary>
public static class CardSelectCmd
{
    private static readonly Stack<ICardSelector> _selectorStack = new Stack<ICardSelector>();

    public static ICardSelector? Selector
        => _selectorStack.Count > 0 ? _selectorStack.Peek() : null;

    /// <summary>常驻答题人（Godot 端总装批赋值一次）。栈上有临时选择器时栈优先。</summary>
    public static ICardSelector? FallbackSelector;

    private sealed class SelectorScope : IDisposable
    {
        private readonly ICardSelector _s;
        private bool _done;
        public SelectorScope(ICardSelector s) => _s = s;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            if (_selectorStack.Count > 0 && _selectorStack.Peek() == _s)
                _selectorStack.Pop();
        }
    }

    /// <summary>独占压栈（同 STS2 守卫：已有在役选择器时抛）。用 using 保证退场。</summary>
    public static IDisposable UseSelector(ICardSelector selector)
    {
        if (_selectorStack.Count > 0)
            throw new InvalidOperationException("已有卡牌选择器在役（同 STS2 UseSelector 守卫）。嵌套请用 PushSelector。");
        _selectorStack.Push(selector);
        return new SelectorScope(selector);
    }

    /// <summary>叠加压栈（选择器里再开选择器的场合，STS2 StackedSelectorScope）。</summary>
    public static IDisposable PushSelector(ICardSelector selector)
    {
        _selectorStack.Push(selector);
        return new SelectorScope(selector);
    }

    /// <summary>
    /// 从手牌选牌（STS2 FromHand 三段式原样）：
    ///   候选为空 → 落空返回空表；
    ///   免确认且候选 ≤ MinSelect → 自动全拿（不弹 UI）；
    ///   否则问选择器（栈 → 常驻 → 都没有就报人话错误）。
    /// </summary>
    public static async Task<List<CardModel>> FromHand(CombatState state, Player player,
        CardSelectorPrefs prefs, Func<CardModel, bool>? filter, GameModel source)
    {
        List<CardModel> pool = player.PlayerCombatState!.Hand.Cards
            .Where(filter ?? (_ => true)).ToList();
        if (pool.Count == 0) return new List<CardModel>();
        if (!prefs.RequireManualConfirmation && pool.Count <= prefs.MinSelect)
            return pool;

        ICardSelector picker = Selector ?? FallbackSelector
            ?? throw new InvalidOperationException(
                "本场战斗没接卡牌选择器。无头：CardSelectCmd.UseSelector(脚本选择器)；" +
                "Godot：总装批把手牌多选接到 CardSelectCmd.FallbackSelector。");

        int min = Math.Min(prefs.MinSelect, pool.Count);
        int max = Math.Min(prefs.MaxSelect, pool.Count);
        return (await picker.GetSelectedCards(pool, min, max)).ToList();
    }

    /// <summary>焚毁 N（裁定1）：选 N 张手牌逐张走消耗动词——燃料照触发、复燃核心照听。
    /// 手牌不足 N → 有多少焚多少；空手 → 落空（燃烧契约边界原样）。返回实际焚掉的牌。</summary>
    public static async Task<List<CardModel>> Immolate(CombatState state, Player player,
        int count, GameModel source)
    {
        List<CardModel> picked = await FromHand(state, player,
            new CardSelectorPrefs("选择要焚毁的牌", count), null, source);
        foreach (CardModel card in picked)
            await CardPileCmd.Exhaust(state, card);
        return picked;
    }
}
