namespace Kernel;

/// <summary>
/// 一次扣血的结果单（同 STS2 DamageResult）。
/// 两段式填写：掉血三项 LoseHpInternal 当场填（init）；
/// 护盾三项由伤害管线（Step B 的 CreatureCmd）事后补（set）——
/// 挡伤发生在 DamageBlockInternal，那一步这张单子还不存在。
/// </summary>
public sealed class DamageResult
{
    public Creature Receiver { get; }
    public ValueProp Props { get; }

    // ── LoseHpInternal 当场填 ──
    /// <summary>实际扣掉的血。</summary>
    public int UnblockedDamage { get; init; }
    /// <summary>超出目标当前血量的溢出。同伴代受死亡时，管线拿它回流给主人。</summary>
    public int OverkillDamage { get; init; }
    public bool WasTargetKilled { get; init; }

    // ── 管线事后补 ──
    public int BlockedDamage { get; set; }
    public bool WasBlockBroken { get; set; }
    public bool WasFullyBlocked { get; set; }

    /// <summary>打出去的全部（"收割形态：按造成的总伤害施加厄运"用它，同 STS2 TotalDamage）。</summary>
    public int TotalDamage => UnblockedDamage + OverkillDamage;

    public DamageResult(Creature receiver, ValueProp props)
    {
        Receiver = receiver;
        Props = props;
    }
}