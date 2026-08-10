using System.Threading.Tasks;

namespace Kernel;

/// <summary>hook 清单续页：同伴/召唤流。</summary>
public abstract partial class GameModel
{
    /// <summary>召唤量修正（顺序折叠，int 域）。STS2 原名原位（Hook.cs:1521）。</summary>
    public virtual int ModifySummonAmount(Player summoner, int amount, GameModel? source) => amount;

    /// <summary>召唤完毕——三条路径（新建/活着加上限/复活）都到这。</summary>
    public virtual Task AfterSummon(Player summoner, int amount) => Task.CompletedTask;

    /// <summary>同伴复活之后（STS2: AfterOstyRevived）。</summary>
    public virtual Task AfterPetRevived(Creature pet) => Task.CompletedTask;
}