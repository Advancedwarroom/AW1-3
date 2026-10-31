// MapEditorHelper.cs - 编辑器大更新：图形填充、区域填充、地形填充、覆盖全图、选区复制粘贴（矩形+不规则）、对称放置
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// MapEditorHelper · Symmetry（由 MapEditorHelper.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class MapEditorHelper
{

    // ========== 对称放置 ==========
    public bool IsSymmetryMode { get; private set; } = false;
    public SymmetryAxis CurrentSymmetryAxis { get; private set; } = SymmetryAxis.None;
    public int symmetryAxisLine = -1;
    private List<Grids> symmetryPreviewGrids = new();

    // ========== 对称放置 ==========
    private void ToggleSymmetryMode()
    {
        if (IsSymmetryMode)
        {
            IsSymmetryMode = false;
            CurrentSymmetryAxis = SymmetryAxis.None;
            symmetryAxisLine = -1;
            ClearSymmetryPreview();
            UpdateStatusHint(TranslateGroup.T("对称放置已关闭"));
            var btn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
            if (btn != null) btn.Text = TranslateGroup.T("🔀 对称: 关");
        }
        else
        {
            var dialog = new AcceptDialog();
            dialog.Title = TranslateGroup.T("选择对称轴");
            dialog.Size = new Vector2I(300, 150);
            var vbox = new VBoxContainer();
            dialog.AddChild(vbox);

            var vertBtn = new Button();
            TranslateGroup.SetText(vertBtn, "竖轴对称 (左右镜像)");
            vertBtn.Pressed += () => { EnableSymmetry(SymmetryAxis.Vertical); dialog.QueueFree(); };
            vbox.AddChild(vertBtn);

            var horizBtn = new Button();
            TranslateGroup.SetText(horizBtn, "横轴对称 (上下镜像)");
            horizBtn.Pressed += () => { EnableSymmetry(SymmetryAxis.Horizontal); dialog.QueueFree(); };
            vbox.AddChild(horizBtn);

            AddChild(dialog);
            dialog.PopupCentered();
        }
    }

    private void EnableSymmetry(SymmetryAxis axis)
    {
        IsSymmetryMode = true;
        CurrentSymmetryAxis = axis;
        UpdateStatusHint($"{TranslateGroup.T("对称放置已开启")}: {axis} | {TranslateGroup.T("点击格子设置对称轴")}");
        var btn = helperPanel?.GetNodeOrNull<Button>("HelperVBox/SymmetryBtn");
        if (btn != null) btn.Text = $"🔀 {TranslateGroup.T("对称")}: {axis}";
    }

    public void OnGridClickForSymmetry(Grids grid)
    {
        if (!IsSymmetryMode || grid == null) return;
        if (symmetryAxisLine < 0)
        {
            symmetryAxisLine = CurrentSymmetryAxis == SymmetryAxis.Vertical ? grid.GridIndex.X : grid.GridIndex.Y;
            UpdateStatusHint($"{TranslateGroup.T("对称轴已设置在")} {symmetryAxisLine}，{TranslateGroup.T("现在放置单位会自动镜像")}");
            ShowSymmetryPreview();
        }
    }

    public Vector2I GetMirroredPosition(Vector2I original)
    {
        if (!IsSymmetryMode || symmetryAxisLine < 0) return original;
        if (CurrentSymmetryAxis == SymmetryAxis.Vertical)
        {
            int dist = original.X - symmetryAxisLine;
            return new Vector2I(symmetryAxisLine - dist, original.Y);
        }
        else
        {
            int dist = original.Y - symmetryAxisLine;
            return new Vector2I(original.X, symmetryAxisLine - dist);
        }
    }

    public void OnEntityPlacedWithSymmetry(Vector2I pos, Action<Vector2I> placeAction)
    {
        placeAction(pos);
        var mirrored = GetMirroredPosition(pos);
        if (mirrored != pos) placeAction(mirrored);
    }

    private void ShowSymmetryPreview()
    {
        ClearSymmetryPreview();
        if (gridManager?.map == null || symmetryAxisLine < 0) return;
        int w = gridManager.map.GetLength(0);
        int h = gridManager.map.GetLength(1);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                bool onAxis = false;
                if (CurrentSymmetryAxis == SymmetryAxis.Vertical && x == symmetryAxisLine) onAxis = true;
                if (CurrentSymmetryAxis == SymmetryAxis.Horizontal && y == symmetryAxisLine) onAxis = true;
                if (onAxis)
                {
                    var g = gridManager.map[x, y];
                    if (g != null)
                    {
                        g.SelfModulate = new Color(1, 0, 1, 0.5f);
                        symmetryPreviewGrids.Add(g);
                    }
                }
            }
        }
    }

    private void ClearSymmetryPreview()
    {
        foreach (var g in symmetryPreviewGrids)
        {
            if (g != null && IsInstanceValid(g))
                g.SelfModulate = Colors.White;
        }
        symmetryPreviewGrids.Clear();
    }
}
