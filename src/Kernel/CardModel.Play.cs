using System.Threading.Tasks;

namespace Kernel;

public abstract partial class CardModel
{
    /// <summary>
    /// 卡牌效果本体。命令式协程——没有 DSL、没有效果树，就是一段调 Cmd 的代码。
    /// 【跨程序集覆写注意】这是 protected internal；在 Kernel 以外的程序集覆写时
    /// C# 规定只写 protected override（CS0507 的坑，测试卡里有示范）。
    /// </summary>
    protected internal virtual Task OnPlay(CombatState state, CardPlay play) => Task.CompletedTask;

    /// <summary>燃料：被消耗时的冒号效果。三种消耗途径都汇于消耗动词，故都触发。</summary>
    protected internal virtual Task OnFuel(CombatState state) => Task.CompletedTask;

    /// <summary>虚无：手牌变空且本卡【正在打出中】时的冒号效果。</summary>
    protected internal virtual Task OnNihility(CombatState state) => Task.CompletedTask;

    /// <summary>
    /// 本回合已打出的牌数,【不含正在结算的这一轮】(裁定12:计数不含自身)。
    /// 史书逐轮记账,末条恒为本轮——天然排除;重放牌第二轮会数到自己的第一轮(同 STS2)。
    /// </summary>
    protected int PlayedThisTurnBesidesThis(CombatState state)
    {
        var history = Owner!.PlayerCombatState!.PlayHistory;
        int n = 0;
        for (int i = 0; i < history.Count - 1; i++)
            if (history[i].Round == state.RoundNumber) n++;
        return n;
    }

    /// <summary>
    /// 打完去哪。永续 → None(【偏离#6 已翻案,回归 STS2】打出即离开牌堆宇宙,
    /// 被动由 OnPlay 施加的 Aura buff 承载;None 哨兵当年就是照 STS2 备下的);
    /// 消耗关键词 → 消耗堆;其余 → 弃牌堆。
    /// </summary>
    public virtual PileType DestinationPileAfterPlay =>
        Type == CardType.Aura ? PileType.None :
        HasKeyword(CardKeyword.Exhaust) ? PileType.Exhaust :
        PileType.Discard;

    /// <summary>回合末是否留在手上。目前 = 保留关键词（"本回合保留"类 hook 到内容出现时加，STS2 有）。</summary>
    public bool ShouldRetainThisTurn => HasKeyword(CardKeyword.Retain);
}
