namespace Kernel;

/// <summary>
/// 怪物（含同伴）的定义基类。本批只有骨架——意图与招式状态机是 Step E。
/// 同伴 = 一只 Side=Player、Creature.PetOwner 非空的怪物（同 STS2 的 Osty）。
/// </summary>
public abstract class MonsterModel : GameModel
{
    /// <summary>进战斗后由 Creature 的构造反向填充。canonical 实例上恒为 null。</summary>
    public Creature? Creature { get; internal set; }

    /// <summary>初始血量区间，开战时用 MonsterHp 流在 [Min, Max] 内掷定（同 STS2）。</summary>
    public abstract int MinInitialHp { get; }
    public abstract int MaxInitialHp { get; }

    public virtual string Describe() => Id.Entry;

    protected override void AfterCloned() => Creature = null;
}