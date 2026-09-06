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

    /// <summary>【裁定13】出牌起点广播:扣费入 Play 堆之后、每轮 OnPlay 之前(逐次)。
    /// 打出型触发在这记名,AfterCardPlayed 摘名结算——起点不在场的听不到,天然不自触发。</summary>
    public virtual Task BeforeCardPlayed(CardModel card, Creature? target) => Task.CompletedTask;

    /// <summary>战斗内生成一张牌之后（STS2: AfterCardGeneratedForCombat；
    /// 跑图层的"获得卡牌"是另一个语义，将来叫 AfterCardAcquired）。</summary>
    public virtual Task AfterCardGenerated(CardModel card) => Task.CompletedTask;
}