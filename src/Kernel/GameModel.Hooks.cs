using System.Threading.Tasks;

namespace Kernel;

/// <summary>
/// GameModel 的另一半：hook 问题清单（对应 STS2 AbstractModel 上的 174 个 virtual）。
///
/// 每个方法 = 引擎墙上的一个挂钩。默认实现全部是恒元（"与我无关"）：
/// 加法钩子返回 0、乘法返回 1、原值钩子原样返回、否决钩子放行、After 钩子空转。
/// 内容（buff/卡/怪/附魔）覆写其中一两个来"举手"。
///
/// 三族纪律：
///   Modify* —— 同步纯函数，零副作用。UI 每帧重跑它做预览，
///              "卡面显示 15 实际打 15"的结构保证系于此。
///   Should* —— 同步，一票否决。
///   Before*/After* —— async，副作用自由（可以在里面发起新结算、await 节拍）。
/// </summary>
public abstract partial class GameModel
{
    // ══ Modify 族：伤害链（decimal 域——唯一取整点在 Hook.ModifyDamage 出口）══

    /// <summary>加段。恒元 0。力量住这。</summary>
    public virtual decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => 0m;

    /// <summary>乘段。恒元 1。灼伤、弱化住这。</summary>
    public virtual decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) => 1m;

    // ══ Modify 族：护盾获得链（同构）══

    public virtual decimal ModifyBlockAdditive(Creature target, decimal amount, ValueProp props, CardModel? cardSource) => 0m;

    public virtual decimal ModifyBlockMultiplicative(Creature target, decimal amount, ValueProp props, CardModel? cardSource) => 1m;

    // ══ Modify 族：护盾之后（int 域——"护盾后不准乘小数"由类型强制，偏离清单 #5）══

    /// <summary>护盾扣完之后、伤害转移之前。target 是【被瞄准的人】。</summary>
    public virtual int ModifyHpLostBeforePet(Creature target, int amount, ValueProp props, Creature? dealer, CardModel? cardSource) => amount;

    /// <summary>伤害转移：换收伤者，不改数值。舍身住这。恒元 = 原目标。</summary>
    public virtual Creature ModifyUnblockedDamageTarget(Creature target, int amount, ValueProp props, Creature? dealer) => target;

    /// <summary>转移之后、扣血之前。target 是【实际挨打的人】——
    /// 同伴替你挨打时蹭不到你的减伤，语义分段就在这。</summary>
    public virtual int ModifyHpLostAfterPet(Creature target, int amount, ValueProp props, Creature? dealer, CardModel? cardSource) => amount;

    // ══ Should 族 ══

    /// <summary>死亡否决。返回 false = 拦下这次死亡（不死类效果）。</summary>
    public virtual bool ShouldDie(Creature creature) => true;

    // ══ Before / After 族 ══

    public virtual Task BeforeDamageReceived(Creature target, int amount, ValueProp props, Creature? dealer, CardModel? cardSource) => Task.CompletedTask;

    /// <summary>受伤结算完毕。反伤类效果的家。</summary>
    public virtual Task AfterDamageReceived(Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource) => Task.CompletedTask;

    /// <summary>造成伤害完毕。吸血类效果的家。</summary>
    public virtual Task AfterDamageGiven(Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource) => Task.CompletedTask;

    public virtual Task AfterCreatureDied(Creature creature, Creature? killer, CardModel? cardSource) => Task.CompletedTask;

    /// <summary>只发给否决死亡的那一个监听者（和 ShouldDie 配对）。</summary>
    public virtual Task AfterPreventingDeath(Creature creature) => Task.CompletedTask;

    /// <summary>实际扣血之后(裁定4:每一跳一次;全挡不发;裸血也发)。烈性之家。恒元:空转。</summary>
    public virtual Task AfterHpLost(Creature target, int amount) => Task.CompletedTask;

    /// <summary>buff 施加量修正(只挂 Apply 正门;canonical 是身份牌,只读)。
    /// 持炎之环/炽烈之家。恒元:原量返回。</summary>
    public virtual int ModifyBuffApplyAmount(Creature target, BuffModel canonical, int amount, CardModel? source) => amount;

    /// <summary>【S2 全局费用钩】折叠改一张卡的当前费用（狂涌/无双乱舞/燎原从这进）。
    /// 出口 Max(0) 在 CardModel.Cost，这里只管折叠。</summary>
    public virtual int ModifyEnergyCost(CardModel card, int cost) => cost;

    /// <summary>【多重打出】折叠一次打出的执行次数（STS2 DuplicationPower 判例）。</summary>
    public virtual int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) => playCount;

    /// <summary>你改了 playCount 之后的消耗回调（Duplication 在这自减一层）。只发给修改者。</summary>
    public virtual Task AfterModifyingCardPlayCount(CardModel card) => Task.CompletedTask;
}