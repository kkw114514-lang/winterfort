using System;
using System.Threading.Tasks;

namespace Kernel;

/// <summary>同伴动词层。召唤管线抄 STS2 OstyCmd.Summon 的三分支结构。</summary>
public static class PetCmd
{
    /// <summary>
    /// 召唤（量=血量上限）：
    ///   活着 → 上限与当前同加；
    ///   有尸体 → 复活（同一对象、同 CombatId 重入名册，舍身随之自动复效——偏离#8 的红利）；
    ///   从未有过 → 创建 T 并自动挂舍身（仅此一次）。
    /// T 只在首次创建时生效（单同伴规则下，加上限/复活不认类型）。
    /// 修正到 ≤0 → 无事发生（STS2 同款短路）。
    /// </summary>
    public static async Task<Creature?> Summon<T>(CombatState state, Player player, int amount, GameModel? source)
        where T : MonsterModel
    {
        if (player.PlayerCombatState == null)
            throw new InvalidOperationException($"{player} 不在战斗中，召唤不了同伴。");

        amount = Hook.ModifySummonAmount(state, player, amount, source, out _);
        if (amount <= 0) return player.Pet;

        Creature? pet = player.Pet;
        bool wasRevive = false;

        if (pet is { IsAlive: true })
        {
            pet.GainMaxHpInternal(amount);          // 上限与当前同加（STS2 GainMaxHp 语义）
            Fx.Vfx("pet_grow", pet);
        }
        else if (pet != null)
        {
            wasRevive = true;
            state.ReattachCreature(pet);            // 先回名册（舍身从这一刻恢复监听）
            pet.SetMaxHpInternal(amount);
            pet.HealInternal(amount);
            Fx.Anim(pet, "revive");
        }
        else
        {
            pet = state.AddPet(ModelRegistry.New<T>(), player, amount);
            await BuffCmd.Apply<GuardianBuff>(state, pet, 1, null);   // 只在首次创建时挂
        }

        Fx.Sfx("pet_summon");
        await Fx.Wait(0.2f);

        state.Events.Emit(new PetSummoned
        {
            Round = state.RoundNumber, Side = state.CurrentSide,
            Player = player, Pet = pet, Amount = amount, WasRevive = wasRevive,
        });
        if (wasRevive)
            await Hook.AfterPetRevived(state, pet);
        await Hook.AfterSummon(state, player, amount);
        return pet;
    }
}