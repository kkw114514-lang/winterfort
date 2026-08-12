using System.Threading.Tasks;

namespace Kernel;

/// <summary>hook 清单续页：回合流。</summary>
public abstract partial class GameModel
{
    /// <summary>回合起始抽牌数（顺序折叠，基数 5 在调用点）。长蛇戒指/机器学习类效果之家。</summary>
    public virtual int ModifyHandDraw(Player player, int count) => count;

    /// <summary>一侧回合开始后（STS2 AfterSideTurnStart 位：能量已重置、玩家侧还没抽牌）。
    /// 灯笼/赤金王印类"第一回合能量"效果之家。</summary>
    public virtual Task AfterTurnStarted(CombatSide side) => Task.CompletedTask;
    
    /// <summary>一侧回合结束后。递减 tick 之家（灼伤/弱化查 side==Enemy）。</summary>
    public virtual Task AfterTurnEnd(CombatSide side) => Task.CompletedTask;

    /// <summary>回合末清手否决（"回合末不弃牌"类效果）。</summary>
    public virtual bool ShouldFlush(Player player) => true;

    /// <summary>一张牌被保留（未被 Flush）之后。</summary>
    public virtual Task AfterCardRetained(CardModel card) => Task.CompletedTask;
}