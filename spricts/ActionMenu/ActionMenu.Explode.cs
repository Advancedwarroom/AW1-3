// ActionMenu.cs - 完整修复版，移除所有unloadButton直接引用
using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;

// ActionMenu · Explode（由 ActionMenu.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class ActionMenu
{

    // ========== ✅ 自爆按钮处理 ==========
    private void OnExplodePressed()
    {
        // ✅ 防重入：如果正在处理爆炸，直接忽略重复信号
        if (isProcessingExplosion) return;
        if (currentUnit == null || !currentUnit.canExplode) return;

        var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
        if (gm == null) return;

        if (!isExplosionPreview)
        {
            // 第一阶段：显示爆炸范围预览
            isExplosionPreview = true;
            gm.gridManager?.ShowExplosionRange(currentUnit, currentUnit.explosionMinRange, currentUnit.explosionMaxRange);

            moveButton.Visible = false;
            attackButton.Visible = false;
            waitButton.Visible = false;
            infoButton.Visible = false;
            captureButton.Visible = false;
            if (loadButton != null) loadButton.Visible = false;
            if (unloadContainer != null) unloadContainer.Visible = false;
            if (supplyButton != null) supplyButton.Visible = false;
        }
        else
        {
            // 第二阶段：执行爆炸
            isProcessingExplosion = true;
            isExplosionPreview = false;
            gm.gridManager?.HideExplosionRange();
            gm.ExecuteExplosion(currentUnit);
            // ✅ 清理引用，防止已销毁单位被重复操作
            currentUnit = null;
            Hide();
            isProcessingExplosion = false;
        }
    }

    public void CancelExplosionPreview()
    {
        if (isProcessingExplosion) return;
        if (isExplosionPreview)
        {
            isExplosionPreview = false;
            var gm = GetTree().GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager?.HideExplosionRange();
            // 恢复按钮状态
            if (currentUnit != null)
            {
                ShowMenu(currentUnit, currentUnit.isMoved);
            }
        }
    }
}
