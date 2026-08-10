using System.Threading.Tasks;

namespace Kernel;

/// <summary>hook 清单续页：卡牌流。全部 After 族（async、副作用自由）。</summary>
public abstract partial class GameModel
{
    /// <summary>抽到一张牌之后。</summary>
    public virtual Task AfterCardDrawn(CardModel card) => Task.CompletedTask;

    /// <summary>一张牌被【效果】弃掉之后。回合末 Flush 不走这（双动词纪律，Step E）。</summary>
    public virtual Task AfterCardDiscarded(CardModel card) => Task.CompletedTask;

    /// <summary>一张牌被消耗之后（三种途径都走同一个消耗动词，所以都到这）。</summary>
    public virtual Task AfterCardExhausted(CardModel card) => Task.CompletedTask;

    /// <summary>一张牌打出流程完毕之后。</summary>
    public virtual Task AfterCardPlayed(CardModel card, Creature? target) => Task.CompletedTask;

    /// <summary>战斗内生成一张牌之后（STS2: AfterCardGeneratedForCombat；
    /// 跑图层的"获得卡牌"是另一个语义，将来叫 AfterCardAcquired）。</summary>
    public virtual Task AfterCardGenerated(CardModel card) => Task.CompletedTask;

    /// <summary>虚无触发之后（某玩家手牌变空）。消耗堆里"下次触发虚无后…"类卡听这个。</summary>
    public virtual Task AfterNihilityTriggered(Player player) => Task.CompletedTask;
}