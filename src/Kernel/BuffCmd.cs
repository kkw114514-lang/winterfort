using System;
using System.Threading.Tasks;

namespace Kernel;

public static class BuffCmd
{
    /// <summary>
    /// 施加 buff（同 STS2 PowerCmd.Apply 的叠层规则）：
    ///   已有同类且非 Single → 合层（走 ChangeAmount）
    ///   已有同类且 Single   → 不动，返回现有实例
    ///   没有 → 新建挂载
    ///   负数首次施加 → 无效（没有可减的层）
    /// </summary>
    public static async Task<T?> Apply<T>(CombatState state, Creature target, int amount, CardModel? source)
        where T : BuffModel
    {
        if (target.IsDead) return null;
        if (amount == 0) return target.GetBuff<T>();

        T? existing = target.GetBuff<T>();
        if (existing != null)
        {
            if (existing.StackType == BuffStackType.Single) return existing;
            await ChangeAmount(state, existing, amount, source);
            return existing.Removed ? null : existing;
        }

        if (amount < 0) return null;

        T buff = ModelRegistry.New<T>();
        buff.ApplyInternal(target, amount);
        state.Events.Emit(new BuffApplied
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Target = target, Buff = buff, Amount = amount,
        });
        Fx.Vfx("buff_apply", target);
        await Fx.CustomScaledWait(0.05f, 0.1f);
        return buff;
    }

    /// <summary>改层数。钳制、归零移除等政策全在这一层——Internal 保持愚蠢。</summary>
    public static async Task<int> ChangeAmount(CombatState state, BuffModel buff, int delta, CardModel? source)
    {
        if (buff.Removed || buff.Owner == null) return buff.Amount;

        int old = buff.Amount;
        int next = old + delta;
        if (!buff.AllowNegative && next < 0) next = 0;

        if (next != old)
        {
            buff.ChangeAmountInternal(next - old);
            state.Events.Emit(new BuffAmountChanged
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Target = buff.Owner, Buff = buff, OldAmount = old, NewAmount = next,
            });
        }

        if (ShouldRemoveDueToAmount(buff))
            await Remove(state, buff);

        return next;
    }

    /// <summary>归零移除规则，STS2 PowerModel.cs:411 原样：
    /// 可负的（力量）只在恰好为 0 时移除（负数保留）；不可负的 ≤0 移除。</summary>
    private static bool ShouldRemoveDueToAmount(BuffModel buff)
        => buff.RemoveAtZero && (buff.AllowNegative ? buff.Amount == 0 : buff.Amount <= 0);

    public static async Task Remove(CombatState state, BuffModel buff)
    {
        if (buff.Removed || buff.Owner == null) return;
        Creature owner = buff.Owner;
        buff.RemoveInternal();
        state.Events.Emit(new BuffRemoved
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Target = owner, Buff = buff,
        });
        await Fx.CustomScaledWait(0.02f, 0.05f);
    }

    /// <summary>
    /// 灼伤提级（规则①）：目标没有灼伤 → 整个无效，返回 false；
    /// 有 → 等级累加、封顶Ⅳ。加层走通用 Apply&lt;SearBuff&gt;，两条轨道互不相碰。
    /// </summary>
    public static async Task<bool> RaiseSearLevel(CombatState state, Creature target, int levels, CardModel? source)
    {
        if (levels <= 0) throw new ArgumentException("提级数必须为正", nameof(levels));

        SearBuff? sear = target.GetBuff<SearBuff>();
        if (sear == null) return false;

        int old = sear.Level;
        sear.RaiseLevelInternal(levels);
        if (sear.Level != old)
        {
            state.Events.Emit(new SearLevelRaised
            {
                Round = state.RoundNumber, Side = state.CurrentSide,
                Target = target, OldLevel = old, NewLevel = sear.Level,
            });
            Fx.Vfx("sear_up", target);
            await Fx.CustomScaledWait(0.05f, 0.1f);
        }
        return true;
    }
}