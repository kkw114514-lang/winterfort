namespace Kernel;

// ══════════════════════════════════════════════════════════════════
// 具体事件类型。铁律复述：状态改完才 Emit；Description 里禁止一切
// 每次运行会变的东西（引用哈希/地址）——日志要逐字节可比。
// ══════════════════════════════════════════════════════════════════

public sealed class DamageReceived : CombatEvent
{
    public required Creature Target { get; init; }
    public Creature? Dealer { get; init; }
    public int UnblockedDamage { get; init; }
    public int BlockedDamage { get; init; }
    public int OverkillDamage { get; init; }
    public bool WasFullyBlocked { get; init; }
    public bool WasKilled { get; init; }
    public ValueProp Props { get; init; }
    public CardModel? Source { get; init; }

    public override string Description =>
        $"{Target.Name} 受到 {UnblockedDamage} 点伤害（挡 {BlockedDamage}，溢 {OverkillDamage}）" +
        (WasKilled ? "，阵亡" : "");
}

public sealed class BlockGained : CombatEvent
{
    public required Creature Target { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Target.Name} 获得 {Amount} 点护盾";
}

public sealed class Healed : CombatEvent
{
    public required Creature Target { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Target.Name} 回复 {Amount} 点生命";
}

public sealed class BuffApplied : CombatEvent
{
    public required Creature Target { get; init; }
    public required BuffModel Buff { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Target.Name} 获得 {Buff.Id.Entry} ×{Amount}";
}

public sealed class BuffAmountChanged : CombatEvent
{
    public required Creature Target { get; init; }
    public required BuffModel Buff { get; init; }
    public int OldAmount { get; init; }
    public int NewAmount { get; init; }
    public override string Description => $"{Target.Name} 的 {Buff.Id.Entry}：{OldAmount}→{NewAmount}";
}

public sealed class BuffRemoved : CombatEvent
{
    public required Creature Target { get; init; }
    public required BuffModel Buff { get; init; }
    public override string Description => $"{Target.Name} 失去 {Buff.Id.Entry}";
}

public sealed class SearLevelRaised : CombatEvent
{
    public required Creature Target { get; init; }
    public int OldLevel { get; init; }
    public int NewLevel { get; init; }
    public override string Description => $"{Target.Name} 的灼伤提级：{OldLevel}→{NewLevel}";
}

public sealed class CreatureDied : CombatEvent
{
    public required Creature Creature { get; init; }
    public Creature? Killer { get; init; }
    public override string Description => $"{Creature.Name} 阵亡";
}

public sealed class HpLost : CombatEvent
{
    public required Creature Target { get; init; }
    public int Amount { get; init; }
    public override string Description => $"{Target.Name} 失去 {Amount} 点生命";
}
