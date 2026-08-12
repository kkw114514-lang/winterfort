using System.Threading.Tasks;
namespace Kernel.Content.Familiars;
/// <summary>
/// 炎舞 Flamedance｜火系眷属（Infanta 开局自带）。绑定卡：换步。
/// 特性「踏焰」：每回合第一次在本回合内构成"攻击↔非攻击"翻转时，抽 1 张牌。
///   · 【判例史】原采腰带抽打判例（记忆跨回合）；试玩后改判，现随跟进判例：
///     窗口只认本回合——与绑定卡换步同窗，一家人一个时钟。
///   · "每回合第一次"靠回合号+战斗引用做门闩（眷属跨战斗存活，回合号每场
///     从 1 重来，只记回合号会把上一场的触发记录带进下一场同号回合）。
/// </summary>
public sealed class Flamedance : FamiliarModel
{
    private CombatState? _lastTriggeredCombat;
    private int _lastTriggeredRound;
    public override async Task AfterCardPlayed(CardModel card, Creature? target)
    {
        if (Owner == null || card.Owner != Owner) return;
        if (Owner.Creature.CombatState is not { } state) return;
        if (_lastTriggeredCombat == state && _lastTriggeredRound == state.RoundNumber)
            return;                                                // 每回合第一次
        var history = Owner.PlayerCombatState!.PlayHistory;
        if (history.Count < 2) return;                             // 没有"上一张"
        PlayRecord prevRec = history[history.Count - 2];           // 最后一条是刚打完的这张
        if (prevRec.Round != state.RoundNumber) return;            // 【改判】窗口只认本回合
        bool prevIsAttack = prevRec.Card.Type == CardType.Attack;
        bool currIsAttack = card.Type == CardType.Attack;
        if (prevIsAttack == currIsAttack) return;                  // 没有翻转
        _lastTriggeredCombat = state;
        _lastTriggeredRound = state.RoundNumber;
        await CardPileCmd.Draw(state, Owner, 1);
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();                                        // 别忘了：基类要清 Owner
        _lastTriggeredCombat = null;
        _lastTriggeredRound = 0;
    }
}
