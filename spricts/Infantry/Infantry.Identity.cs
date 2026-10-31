using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

// Infantry · Identity（由 Infantry.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Infantry
{
    // ========== ✅ 究极自由系统：所有单位类型特性都可通过Inspector控制 ==========

    // ========== ✅ 兵种身份：不写代码造新兵种 ==========
    // 新兵种 = 复制一个现有单位场景 → 填 unitTypeId（唯一）→ 在检查器里调参数 → 放进 res://Prefabs/CustomUnits/ 即自动进入生产名单
    [ExportGroup("兵种身份（自定义兵种）")]
    [Export] public string unitTypeId = "";          // 兵种ID：留空=脚本类名（原有兵种不用填）；攻防表/过滤名单/运输/补给/AI 全部按它识别
    [Export] public string unitDisplayName = "";     // 生产菜单显示名（留空=兵种ID）
    [Export] public string templateType = "";        // 参照兵种：攻防表查不到本ID条目、视野/默认配置时按此兵种（留空=脚本类名）
    /// <summary>兵种身份ID（替代 GetType().Name 的所有身份判定）</summary>
    public string UnitTypeId => string.IsNullOrEmpty(unitTypeId) ? GetType().Name : unitTypeId;
    /// <summary>参照兵种ID（静态配置表/攻防表兜底）</summary>
    public string TemplateId => string.IsNullOrEmpty(templateType) ? GetType().Name : templateType;
    /// <summary>显示名：unitDisplayName → 兵种配置显示名 → 兵种ID</summary>
    public string DisplayLabel => !string.IsNullOrEmpty(unitDisplayName) ? unitDisplayName : (UnitRegistry.Get(UnitTypeId)?.DisplayName ?? UnitTypeId);
}
