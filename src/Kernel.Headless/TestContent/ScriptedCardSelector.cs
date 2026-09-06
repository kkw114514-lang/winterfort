using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel;

/// <summary>无头脚本选择器:不给脚本时默认"从头拿满 min"(确定性);
/// Then() 逐次入队自定义拣法,一次选择消费一条。</summary>
public sealed class ScriptedCardSelector : ICardSelector
{
    private readonly Queue<Func<IReadOnlyList<CardModel>, IEnumerable<CardModel>>> _picks
        = new Queue<Func<IReadOnlyList<CardModel>, IEnumerable<CardModel>>>();

    public ScriptedCardSelector Then(Func<IReadOnlyList<CardModel>, IEnumerable<CardModel>> pick)
    {
        _picks.Enqueue(pick);
        return this;
    }

    public Task<IEnumerable<CardModel>> GetSelectedCards(IReadOnlyList<CardModel> cards, int min, int max)
    {
        IEnumerable<CardModel> picked = _picks.Count > 0
            ? _picks.Dequeue()(cards)
            : cards.Take(min);
        return Task.FromResult(picked);
    }
}
