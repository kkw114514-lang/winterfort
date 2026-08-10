namespace Kernel;

/// <summary>
/// 一个玩家【整局】活着的部分。对照：PlayerCombatState（下一批）只活一场战斗，
/// 战斗结束整个扔掉——"忘了重置手牌/能量"这类 bug 被这个划分结构性消灭。
///
/// 本批是最小骨架：牌库、遗物、金币等跑图层字段，等 CardPile 与跑图层再加。
/// </summary>
public sealed class Player
{
    public string Name { get; }

    /// <summary>玩家在战斗里的"身体"。整局同一个——血量跨战斗延续的原因。</summary>
    public Creature Creature { get; }

    public int MaxEnergy { get; internal set; } = 3;
    
    /// <summary>整局的牌库（局外堆）。开战时它是抽牌堆的复制来源（Step E）。</summary>
    public CardPile Deck { get; } = new CardPile(PileType.Deck);

    /// <summary>本场战斗的战斗态。由 CombatState.AddPlayer 创建；战斗结束整个
    /// 丢掉置 null——"忘了重置"型 bug 的结构性消灭点。</summary>
    public PlayerCombatState? PlayerCombatState { get; internal set; }
    public Player(string name, int maxHp)
    {
        Name = name;
        Creature = new Creature(this, maxHp);
    }

    public override string ToString() => Name;
}