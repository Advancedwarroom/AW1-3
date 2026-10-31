using Godot;
using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

// TerrainEditor · TerrainMatrixEditor（由 TerrainEditor.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class TerrainEditor
{
private void OpenUnitTerrainMatrixEditor()
{
    if (unitMatrixEditorPanel == null) CreateUnitMatrixEditorPanel();

    var container = unitMatrixEditorPanel.GetNodeOrNull<VBoxContainer>("MatrixContainer");
    if (container != null)
    {
        foreach (var child in container.GetChildren()) 
            child.QueueFree();

        var allUnitTypes = new List<string>(VisionConfig.GetAllUnitVisionConfigs().Keys);
        var allTerrainTypes = new List<GridType>();
        foreach (GridType gt in Enum.GetValues(typeof(GridType)))
            allTerrainTypes.Add(gt);

        if (allUnitTypes.Count == 0)
        {
            var emptyLabel = new Label();
            TranslateGroup.SetText(emptyLabel, "⚠️ 无单位类型配置");
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            emptyLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(emptyLabel);
        }
        else
        {
            foreach (var unitType in allUnitTypes)
            {
                var unitLabel = new Label();
                unitLabel.Text = $"=== {unitType} ===";
                unitLabel.AddThemeFontSizeOverride("font_size", 14);
                unitLabel.AddThemeColorOverride("font_color", Colors.Yellow);
                container.AddChild(unitLabel);

                var table = VisionConfig.GetUnitTerrainBonusTable(unitType);
                foreach (var terrain in allTerrainTypes)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 8);

                    var terrainLabel = new Label();
                    terrainLabel.Text = terrain.ToString();
                    terrainLabel.CustomMinimumSize = new Vector2(100, 24);
                    terrainLabel.AddThemeFontSizeOverride("font_size", 11);
                    hbox.AddChild(terrainLabel);

                    int currentValue = table.ContainsKey(terrain) ? table[terrain] : 0;

                    var spinBox = new SpinBox();
                    spinBox.MinValue = -10;
                    spinBox.MaxValue = 10;
                    spinBox.Value = currentValue;
                    spinBox.CustomMinimumSize = new Vector2(70, 26);
                    spinBox.AddThemeFontSizeOverride("font_size", 11);

                    string capturedUnit = unitType;
                    GridType capturedTerrain = terrain;
                    spinBox.ValueChanged += (newVal) => {
                        VisionConfig.SetUnitTerrainBonus(capturedUnit, capturedTerrain, (int)newVal);
                        RefreshAllUnitVision();
                    };

                    hbox.AddChild(spinBox);

                    var signLabel = new Label();
                    string sign = currentValue > 0 ? "+" : "";
                    signLabel.Text = $"({sign}{currentValue})";
                    signLabel.AddThemeFontSizeOverride("font_size", 10);
                    signLabel.AddThemeColorOverride("font_color",
                        currentValue > 0 ? new Color(0.3f, 1, 0.3f) :
                        currentValue < 0 ? new Color(1, 0.3f, 0.3f) : new Color(0.7f, 0.7f, 0.7f));
                    hbox.AddChild(signLabel);

                    container.AddChild(hbox);
                }
            }
        }
    }

    unitMatrixEditorPanel.Visible = true;
    isMenuOpen = true;
    container?.QueueSort();
}

private void OpenWeaponTerrainMatrixEditor()
{
    if (weaponMatrixEditorPanel == null) CreateWeaponMatrixEditorPanel();

    var container = weaponMatrixEditorPanel.GetNodeOrNull<VBoxContainer>("MatrixContainer");
    if (container != null)
    {
        foreach (var child in container.GetChildren()) 
            child.QueueFree();

        var allWeaponTypes = new List<string>(VisionConfig.GetAllWeaponVisionConfigs().Keys);
        var allTerrainTypes = new List<GridType>();
        foreach (GridType gt in Enum.GetValues(typeof(GridType)))
            allTerrainTypes.Add(gt);

        if (allWeaponTypes.Count == 0)
        {
            var emptyLabel = new Label();
            TranslateGroup.SetText(emptyLabel, "⚠️ 无兵器类型配置");
            emptyLabel.AddThemeFontSizeOverride("font_size", 14);
            emptyLabel.AddThemeColorOverride("font_color", Colors.Red);
            container.AddChild(emptyLabel);
        }
        else
        {
            foreach (var weaponType in allWeaponTypes)
            {
                var weaponLabel = new Label();
                weaponLabel.Text = $"=== {weaponType} ===";
                weaponLabel.AddThemeFontSizeOverride("font_size", 14);
                weaponLabel.AddThemeColorOverride("font_color", Colors.Yellow);
                container.AddChild(weaponLabel);

                foreach (var terrain in allTerrainTypes)
                {
                    var hbox = new HBoxContainer();
                    hbox.AddThemeConstantOverride("separation", 8);

                    var terrainLabel = new Label();
                    terrainLabel.Text = terrain.ToString();
                    terrainLabel.CustomMinimumSize = new Vector2(100, 24);
                    terrainLabel.AddThemeFontSizeOverride("font_size", 11);
                    hbox.AddChild(terrainLabel);

                    int currentVal = VisionConfig.GetWeaponTerrainVisionBonus(weaponType, terrain);

                    var spinBox = new SpinBox();
                    spinBox.MinValue = -10;
                    spinBox.MaxValue = 10;
                    spinBox.Value = currentVal;
                    spinBox.CustomMinimumSize = new Vector2(70, 26);
                    spinBox.AddThemeFontSizeOverride("font_size", 11);

                    string capturedWeapon = weaponType;
                    GridType capturedTerrain = terrain;
                    spinBox.ValueChanged += (newVal) => {
                        VisionConfig.SetWeaponTerrainBonus(capturedWeapon, capturedTerrain, (int)newVal);
                        RefreshAllUnitVision();
                    };

                    hbox.AddChild(spinBox);
                    container.AddChild(hbox);
                }
            }
        }
    }

    weaponMatrixEditorPanel.Visible = true;
    isMenuOpen = true;
    container?.QueueSort();
}

    private void CreateUnitMatrixEditorPanel()
    {
        unitMatrixEditorPanel = new Panel();
        unitMatrixEditorPanel.Name = "UnitMatrixEditorPanel";
        unitMatrixEditorPanel.CustomMinimumSize = new Vector2(450, 600);
        unitMatrixEditorPanel.SetAnchorsPreset(LayoutPreset.Center);
        unitMatrixEditorPanel.Visible = false;
        unitMatrixEditorPanel.ZIndex = 1400;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.5f, 0.7f, 0.9f);
        unitMatrixEditorPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var titleLabel = new Label();
        TranslateGroup.SetText(titleLabel, "📊 单位类型×地形 视野加成矩阵");
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.9f, 1f));
        titleLabel.CustomMinimumSize = new Vector2(0, 40);
        titleLabel.Position = new Vector2(0, 15);
        titleLabel.Size = new Vector2(450, 40);
        unitMatrixEditorPanel.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(410, 12);
        closeBtn.Pressed += () => { unitMatrixEditorPanel.Visible = false; isMenuOpen = false; };
        unitMatrixEditorPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(20, 60);
        scroll.Size = new Vector2(410, 520);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var container = new VBoxContainer();
        container.Name = "MatrixContainer";
        container.AddThemeConstantOverride("separation", 6);
        container.Size = new Vector2(410, 0);

        scroll.AddChild(container);
        unitMatrixEditorPanel.AddChild(scroll);

        AddChild(unitMatrixEditorPanel);
        MakeMenuDraggable(unitMatrixEditorPanel);
    }
    private void CreateWeaponMatrixEditorPanel()
    {
        weaponMatrixEditorPanel = new Panel();
        weaponMatrixEditorPanel.Name = "WeaponMatrixEditorPanel";
        weaponMatrixEditorPanel.CustomMinimumSize = new Vector2(450, 400);
        weaponMatrixEditorPanel.SetAnchorsPreset(LayoutPreset.Center);
        weaponMatrixEditorPanel.Visible = false;
        weaponMatrixEditorPanel.ZIndex = 1400;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.08f, 0.1f, 0.15f, 0.98f);
        panelStyle.SetCornerRadiusAll(16);
        panelStyle.SetBorderWidthAll(3);
        panelStyle.BorderColor = new Color(0.7f, 0.5f, 0.9f);
        weaponMatrixEditorPanel.AddThemeStyleboxOverride("panel", panelStyle);

        var titleLabel = new Label();
        TranslateGroup.SetText(titleLabel, "📊 兵器类型×地形 视野加成矩阵");
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 1f));
        titleLabel.CustomMinimumSize = new Vector2(0, 40);
        titleLabel.Position = new Vector2(0, 15);
        titleLabel.Size = new Vector2(450, 40);
        weaponMatrixEditorPanel.AddChild(titleLabel);

        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.CustomMinimumSize = new Vector2(32, 32);
        closeBtn.Position = new Vector2(410, 12);
        closeBtn.Pressed += () => { weaponMatrixEditorPanel.Visible = false; isMenuOpen = false; };
        weaponMatrixEditorPanel.AddChild(closeBtn);

        var scroll = new ScrollContainer();
        scroll.Position = new Vector2(20, 60);
        scroll.Size = new Vector2(410, 320);
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;

        var container = new VBoxContainer();
        container.Name = "MatrixContainer";
        container.AddThemeConstantOverride("separation", 6);
        container.Size = new Vector2(410, 0);

        scroll.AddChild(container);
        weaponMatrixEditorPanel.AddChild(scroll);

        AddChild(weaponMatrixEditorPanel);
        MakeMenuDraggable(weaponMatrixEditorPanel);
    }
}
