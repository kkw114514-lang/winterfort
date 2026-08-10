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
    /// 打完去哪。Aura → Removed（偏离清单 #6：有户口的离场，替代 STS2 的无堆 limbo）；
    /// 消耗关键词 → 消耗堆；其余 → 弃牌堆。
    /// 以后"打出后置于抽牌堆顶"类效果来了，再把这里升级成 hook 可改（同 STS2）。
    /// </summary>
    public virtual PileType DestinationPileAfterPlay =>
        Type == CardType.Aura ? PileType.Removed :
        HasKeyword(CardKeyword.Exhaust) ? PileType.Exhaust :
        PileType.Discard;
}