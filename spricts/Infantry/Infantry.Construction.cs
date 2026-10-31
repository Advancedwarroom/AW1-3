// Infantry.cs - 建造模块：单位可在舱内/外部建造建筑
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Infantry · Construction（建造模块）
// 由 Infantry.cs 按模块拆分；partial class，Export 分组已显式标注
// ✅ 与生产模块对称，差异：
//    建造的是建筑（白名单填建筑类型名：City/Base/AirPort/Port…，见 FacilityDatabase）。
//    内部建造与搭载模块无绑定：建好的建筑存在舱内数据里，之后用「部署」放到场上。
//    外部建造：范围 [constructionMinRange, constructionMaxRange]，自动随机或手选目标格。
//    进度制：总进度 N / 每回合进度 Z。「内部建造时可否移动」= 移动会不会打断进度：
//    关=移动即中断；外部建造移动即中断。
//    自动化：开启后点一次建造，之后每回合自动推进（只显示中断按钮），不消耗单位行动。
//    冷却：constructionCooldownTurns 回合。
//    建造弹药：currentConstructionAmmo>0 才显示建造按钮；开工消耗 1 发（统一弹药系统）。
public partial class Infantry
{
    [ExportGroup("建造模块")]
    [Export] public bool canBuildFacilities = false;                                  // ① 是否可建造
    [Export(PropertyHint.Enum, "关闭,内部,外部,可选内外")] public int constructionLocationMode = 0;  // ② 建造位置
    [Export] public int constructionMinRange = 1;                                     // ③ 外部建造最小范围
    [Export] public int constructionMaxRange = 2;                                     // ③ 外部建造最大范围
    [Export] public bool constructionAutoPlace = false;                                // ④ 外部自动随机放置（false=手选目标格）
    [Export] public Godot.Collections.Array<string> constructionWhitelist = new();   // ⑤ 建造白名单（建筑类型名）
    [Export] public int maxConstructionAmmo = 0;                                      // ⑥ 建造弹药上限
    [Export] public int currentConstructionAmmo = 0;                                  // ⑥ 当前建造弹药
    [Export] public int constructionTotalProgress = 20;                                // ⑦ 建造总进度 N
    [Export] public int constructionProgressPerTurn = 10;                              // ⑧ 每回合进度 Z
    [Export] public bool canMoveWhileBuilding = false;                                // ⑨ 内部建造时可否移动
    [Export] public bool constructionAutomation = false;                               // ⑩ 自动化
    [Export] public int constructionCooldownTurns = 0;                                  // ⑪ 冷却回合数

    // ===== 运行时状态（不导出） =====
    public bool isConstructing = false;                 // 建造中
    public int constructionProgress = 0;                // 当前累计进度
    public string constructionTargetFacilityType = ""; // 本次建造的建筑
    public bool constructionInternal = true;            // 本次建造：内部/外部
    public int constructionCooldownRemaining = 0;       // 剩余冷却
    public Grids constructionTargetGrid = null;         // 外部建造目标格

    /// <summary>舱内待部署建筑（内部建造的产出，类型名列表）</summary>
    public List<string> carriedFacilityTypes = new List<string>();

    public bool ConstructionAllowsInternal => constructionLocationMode == 1 || constructionLocationMode == 3;
    public bool ConstructionAllowsExternal => constructionLocationMode == 2 || constructionLocationMode == 3;

    /// <summary>能否开始一次建造（按钮显示条件）</summary>
    public bool CanStartConstruction(bool internalMode)
    {
        if (!canBuildFacilities) return false;
        if (isConstructing) return false;
        if (constructionCooldownRemaining > 0) return false;
        if (constructionWhitelist == null || constructionWhitelist.Count == 0) return false;
        if (currentConstructionAmmo <= 0) return false;                    // ⑥ 建造弹药决定能否建造
        if (internalMode && !ConstructionAllowsInternal) return false;
        if (!internalMode && !ConstructionAllowsExternal) return false;
        return true;
    }

    /// <summary>开始建造</summary>
    public bool StartConstruction(string facilityType, bool internalMode, Grids externalTargetGrid = null)
    {
        if (!CanStartConstruction(internalMode)) return false;
        if (FacilityDatabase.GetScenePath(facilityType) == null) return false;

        if (!TryConsumeAmmo(AmmoKind.Construction)) return false;  // 开工消耗 1 发建造弹药

        constructionTargetFacilityType = facilityType;
        constructionInternal = internalMode;
        constructionTargetGrid = externalTargetGrid;
        constructionProgress = 0;
        isConstructing = true;

        // ⑩ 自动化=建造不消耗行动；非自动化=本回合被建造占用（待机）
        if (!constructionAutomation)
        {
            isMoved = true;
            isAttacked = true;
            hasActed = true;
            state = UnitState.Acted;
            SetWaitVisual(true);
        }
        return true;
    }

    /// <summary>每回合推进（由 GameManager.Turn 调用）</summary>
    public void ProcessTurnConstruction()
    {
        if (constructionCooldownRemaining > 0) constructionCooldownRemaining--;
        if (!isConstructing) return;
        constructionProgress += Math.Max(0, constructionProgressPerTurn);
        if (constructionProgress >= constructionTotalProgress) CompleteConstruction();
    }

    /// <summary>中断建造（进度清零，不返还弹药，冷却照常开始）</summary>
    public void CancelConstruction()
    {
        if (!isConstructing) return;
        isConstructing = false;
        constructionProgress = 0;
        constructionTargetFacilityType = "";
        constructionTargetGrid = null;
        constructionCooldownRemaining = Math.Max(0, constructionCooldownTurns);
    }

    private void CompleteConstruction()
    {
        isConstructing = false;
        constructionCooldownRemaining = Math.Max(0, constructionCooldownTurns);
        string facilityType = constructionTargetFacilityType;
        bool internalMode = constructionInternal;
        Grids target = constructionTargetGrid;
        constructionProgress = 0;
        constructionTargetFacilityType = "";
        constructionTargetGrid = null;

        if (string.IsNullOrEmpty(facilityType)) return;

        if (internalMode)
        {
            // 内部建造与搭载模块无绑定：建好的建筑存进舱内数据，之后「部署」到场上
            carriedFacilityTypes.Add(facilityType);
            return;
        }

        // 外部：直接放到目标格（自动放置时由调用方选好格）
        if (target == null || !IsInstanceValid(target))
            target = FindValidExternalConstructionGrid(facilityType);
        if (target == null) return;
        SpawnFacilityOnGrid(facilityType, target);
    }

    /// <summary>在指定格子上生成建筑（部署/外部建造共用）</summary>
    public bool SpawnFacilityOnGrid(string facilityType, Grids target)
    {
        if (target == null || !IsInstanceValid(target)) return false;
        if (target.city != null && IsInstanceValid(target.city)) return false;   // 已有建筑
        string scenePath = FacilityDatabase.GetScenePath(facilityType);
        if (scenePath == null) return false;

        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null) return false;
        var facility = scene.Instantiate() as Facility;
        if (facility == null) return false;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null) { facility.QueueFree(); return false; }

        facility.facilityTeam = team;
        target.AddChild(facility);   // Facility._Ready 会以父节点 Grids 自动绑定 ParentGrid
        return true;
    }

    /// <summary>部署舱内建筑：从 carriedFacilityTypes 取一个放到目标格</summary>
    public bool DeployCarriedFacility(Grids target)
    {
        if (carriedFacilityTypes.Count == 0) return false;
        if (!SpawnFacilityOnGrid(carriedFacilityTypes[0], target)) return false;
        carriedFacilityTypes.RemoveAt(0);
        return true;
    }

    /// <summary>外部建造自动放置：在范围内随机挑一个可放格子</summary>
    public Grids FindValidExternalConstructionGrid(string facilityType)
    {
        var candidates = GetValidExternalConstructionGrids();
        if (candidates.Count == 0) return null;
        var rng = new Random();
        return candidates[rng.Next(candidates.Count)];
    }

    /// <summary>外部建造有效目标格：范围内 + 空位 + 无既有建筑</summary>
    public List<Grids> GetValidExternalConstructionGrids()
    {
        var result = new List<Grids>();
        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm?.gridManager == null || gm.gridManager.map == null) return result;

        int x = grid != null ? grid.GridIndex.X : -1;
        int y = grid != null ? grid.GridIndex.Y : -1;
        if (x < 0) return result;

        int minX = Math.Max(0, x - constructionMaxRange);
        int maxX = Math.Min(gm.gridManager.map.GetLength(0) - 1, x + constructionMaxRange);
        int minY = Math.Max(0, y - constructionMaxRange);
        int maxY = Math.Min(gm.gridManager.map.GetLength(1) - 1, y + constructionMaxRange);

        for (int gx = minX; gx <= maxX; gx++)
        {
            for (int gy = minY; gy <= maxY; gy++)
            {
                int dist = Math.Abs(gx - x) + Math.Abs(gy - y);
                if (dist < constructionMinRange || dist > constructionMaxRange) continue;
                var g = gm.gridManager.map[gx, gy];
                if (g == null) continue;
                if (g.city != null && IsInstanceValid(g.city)) continue;   // 已有建筑
                if (g.weapon != null) continue;                            // 兵器占位
                result.Add(g);
            }
        }
        return result;
    }

}
