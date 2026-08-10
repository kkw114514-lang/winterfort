using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernel;

/// <summary>
/// 一个卡牌数值。
///
/// 【核心设计：数值与文案同源】
/// DynamicVar("Damage", 6) 既是伤害值，也是描述文本 "{Damage}" 的渲染源。
/// 结构上消灭了整个卡牌游戏行业的头号 bug 类型——"改了数值忘了改描述"。
///
/// 一个 var 扛两个值：
///   Base    基础值（含升级增量）
///   Preview 把力量/易伤等实时修正算进去后的显示值
///
/// Preview 将来由【与实际结算完全相同的那条 hook 链】算出来，所以卡面写 9
/// 就一定打 9。战斗层还没建，现在 Preview 恒等于 Base。
/// </summary>
public sealed class DynamicVar
{
    public string Name { get; }

    /// <summary>基础值。只能通过 UpgradeBy / SetBase 改——挡住"从外面随手改定义"。</summary>
    public decimal Base { get; private set; }

    /// <summary>显示值。每帧可重算，改它无副作用。</summary>
    public decimal Preview { get; set; }

    /// <summary>本次升级刚变过 → 升级预览界面里高亮它。</summary>
    public bool WasJustUpgraded { get; private set; }

    public int Int => (int)Base;
    public int PreviewInt => (int)Preview;

    public DynamicVar(string name, decimal value)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("变量必须有名字", nameof(name));
        Name = name;
        Base = value;
        Preview = value;
    }

    /// <summary>升级增量。只在 CardModel.OnUpgrade 里调，上游已经 AssertMutable 过。</summary>
    public void UpgradeBy(decimal addend)
    {
        Base += addend;
        Preview = Base;
        WasJustUpgraded = true;
    }

    /// <summary>引擎内部改值（附魔、临时修正会用到）。内容代码碰不到。</summary>
    internal void SetBase(decimal value)
    {
        Base = value;
        Preview = value;
    }

    /// <summary>升级预览展示完毕，清掉高亮标记。</summary>
    public void FinalizeUpgrade() => WasJustUpgraded = false;

    public DynamicVar Clone() => (DynamicVar)MemberwiseClone();

    public override string ToString() => PreviewInt.ToString();
}

public sealed class DynamicVarSet
{
    private readonly Dictionary<string, DynamicVar> _vars = new Dictionary<string, DynamicVar>();

    public DynamicVarSet(IEnumerable<DynamicVar> vars)
    {
        foreach (DynamicVar v in vars)
        {
            if (_vars.ContainsKey(v.Name))
                throw new InvalidOperationException(
                    $"同名变量 {v.Name}。一张卡有两个同类数值时请显式命名（Shield1 / Shield2）。");
            _vars[v.Name] = v;
        }
    }

    public bool Has(string name) => _vars.ContainsKey(name);

    public DynamicVar this[string name]
    {
        get
        {
            if (!_vars.TryGetValue(name, out DynamicVar? v))
                throw new KeyNotFoundException($"这张卡没有名为 {name} 的数值。检查 CanonicalVars。");
            return v;
        }
    }

    // 高频的两个给快捷方式，其余走字符串索引器
    public DynamicVar Damage => this["Damage"];
    public DynamicVar Shield => this["Shield"];

    /// <summary>固定顺序遍历——描述渲染、导表都靠它。</summary>
    public IEnumerable<DynamicVar> All => _vars.Values.OrderBy(v => v.Name, StringComparer.Ordinal);

    public DynamicVarSet Clone() => new DynamicVarSet(All.Select(v => v.Clone()));

    public void FinalizeUpgrade()
    {
        foreach (DynamicVar v in _vars.Values) v.FinalizeUpgrade();
    }
}