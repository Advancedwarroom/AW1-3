using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 势力配置面板：支持最多100个势力的增删改查
/// </summary>
public partial class FactionConfigPanel : VBoxContainer
{
    private VBoxContainer factionListContainer;
    private Button addButton;
    private Button saveButton;
    private Label countLabel;
    
    // 每行势力的控件引用（用于读取数据）
    private readonly List<FactionRow> rows = new();
    
    private const int MaxFactions = 100;
    
    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 10);
        AddThemeConstantOverride("margin_left", 10);
        AddThemeConstantOverride("margin_right", 10);
        AddThemeConstantOverride("margin_top", 10);
        AddThemeConstantOverride("margin_bottom", 10);
        
        CreateHeader();
        CreateScrollList();
        CreateFooterButtons();
        RefreshList();
    }
    
    private void CreateHeader()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        
        var title = new Label();
        title.Text = "🏴 势力配置（最多100个）";
        title.AddThemeFontSizeOverride("font_size", 18);
        title.AddThemeColorOverride("font_color", Colors.Yellow);
        header.AddChild(title);
        
        countLabel = new Label();
        countLabel.Text = "(0/100)";
        countLabel.AddThemeFontSizeOverride("font_size", 14);
        countLabel.AddThemeColorOverride("font_color", Colors.LightGray);
        header.AddChild(countLabel);
        
        AddChild(header);
        AddChild(new HSeparator());
    }
    
    private void CreateScrollList()
    {
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(0, 400);
        
        factionListContainer = new VBoxContainer();
        factionListContainer.AddThemeConstantOverride("separation", 6);
        factionListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        
        // 表头
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 5);
        
        headerRow.AddChild(CreateHeaderLabel("启用", 50));
        headerRow.AddChild(CreateHeaderLabel("势力ID", 80));
        headerRow.AddChild(CreateHeaderLabel("显示名称", 120));
        headerRow.AddChild(CreateHeaderLabel("主色", 80));
        headerRow.AddChild(CreateHeaderLabel("副色", 80));
        headerRow.AddChild(CreateHeaderLabel("操作", 60));
        
        factionListContainer.AddChild(headerRow);
        factionListContainer.AddChild(new HSeparator());
        
        scroll.AddChild(factionListContainer);
        AddChild(scroll);
    }
    
    private Label CreateHeaderLabel(string text, int width)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Colors.LightBlue);
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }
    
    private void CreateFooterButtons()
    {
        AddChild(new HSeparator());
        
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);
        btnRow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        
        addButton = new Button();
        addButton.Text = "➕ 添加新势力";
        addButton.CustomMinimumSize = new Vector2(140, 40);
        addButton.AddThemeFontSizeOverride("font_size", 14);
        var addStyle = new StyleBoxFlat();
        addStyle.BgColor = new Color(0.2f, 0.5f, 0.3f, 0.9f);
        addStyle.SetCornerRadiusAll(8);
        addButton.AddThemeStyleboxOverride("normal", addStyle);
        addButton.Pressed += OnAddFaction;
        btnRow.AddChild(addButton);
        
        saveButton = new Button();
        saveButton.Text = "💾 保存势力配置";
        saveButton.CustomMinimumSize = new Vector2(140, 40);
        saveButton.AddThemeFontSizeOverride("font_size", 14);
        var saveStyle = new StyleBoxFlat();
        saveStyle.BgColor = new Color(0.2f, 0.4f, 0.6f, 0.9f);
        saveStyle.SetCornerRadiusAll(8);
        saveButton.AddThemeStyleboxOverride("normal", saveStyle);
        saveButton.Pressed += OnSaveFactions;
        btnRow.AddChild(saveButton);
        
        var resetBtn = new Button();
        resetBtn.Text = "🔄 恢复默认";
        resetBtn.CustomMinimumSize = new Vector2(120, 40);
        resetBtn.AddThemeFontSizeOverride("font_size", 14);
        var resetStyle = new StyleBoxFlat();
        resetStyle.BgColor = new Color(0.5f, 0.3f, 0.2f, 0.9f);
        resetStyle.SetCornerRadiusAll(8);
        resetBtn.AddThemeStyleboxOverride("normal", resetStyle);
        resetBtn.Pressed += OnResetDefaults;
        btnRow.AddChild(resetBtn);
        
        AddChild(btnRow);
    }
    
    /// <summary>
    /// 刷新势力列表显示
    /// </summary>
    public void RefreshList()
    {
        // 清除旧行（保留表头部分）
        foreach (var row in rows)
            row.Dispose();
        rows.Clear();
        
        // 清除容器中的子节点（跳过表头：前3个是headerRow + separator + 可能的其他）
        // 实际上我们需要更精确地清除。让我重新设计：表头放在单独的容器中
        // 简单做法：重建整个列表内容
        
        // 先清除所有子节点（除了前3个：headerRow + separator + 表头内容）
        while (factionListContainer.GetChildCount() > 2)
        {
            var child = factionListContainer.GetChild(factionListContainer.GetChildCount() - 1);
            factionListContainer.RemoveChild(child);
            child.QueueFree();
        }
        
        var factions = TeamHelper.GetAllFactions();
        countLabel.Text = $"({factions.Count}/{MaxFactions})";
        
        foreach (var f in factions)
        {
            var row = CreateFactionRow(f);
            factionListContainer.AddChild(row.Container);
            rows.Add(row);
        }
        
        // 更新添加按钮状态
        addButton.Disabled = factions.Count >= MaxFactions;
    }
    
    private FactionRow CreateFactionRow(FactionConfig faction)
    {
        var row = new FactionRow();
        row.Config = faction;
        
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 5);
        hbox.AddThemeConstantOverride("margin_top", 4);
        hbox.AddThemeConstantOverride("margin_bottom", 4);
        
        // 是否内置势力（不可删）
        bool isBuiltin = faction.Id == TeamHelper.Player || faction.Id == TeamHelper.Player0;
        
        // 启用CheckBox
        row.ActiveCheck = new CheckBox();
        row.ActiveCheck.ButtonPressed = faction.IsActive;
        row.ActiveCheck.CustomMinimumSize = new Vector2(50, 0);
        hbox.AddChild(row.ActiveCheck);
        
        // 势力ID（只读）
        var idLabel = new Label();
        idLabel.Text = faction.Id;
        idLabel.AddThemeFontSizeOverride("font_size", 12);
        idLabel.CustomMinimumSize = new Vector2(80, 0);
        hbox.AddChild(idLabel);
        
        // 显示名称输入
        row.NameEdit = new LineEdit();
        row.NameEdit.Text = faction.DisplayName;
        row.NameEdit.CustomMinimumSize = new Vector2(120, 32);
        row.NameEdit.AddThemeFontSizeOverride("font_size", 12);
        hbox.AddChild(row.NameEdit);
        
        // 主色选择
        row.PrimaryColorBtn = new ColorPickerButton();
        row.PrimaryColorBtn.Color = faction.PrimaryColor;
        row.PrimaryColorBtn.CustomMinimumSize = new Vector2(80, 32);
        hbox.AddChild(row.PrimaryColorBtn);
        
        // 副色选择
        row.SecondaryColorBtn = new ColorPickerButton();
        row.SecondaryColorBtn.Color = faction.SecondaryColor;
        row.SecondaryColorBtn.CustomMinimumSize = new Vector2(80, 32);
        hbox.AddChild(row.SecondaryColorBtn);
        
        // 删除按钮
        var delBtn = new Button();
        delBtn.Text = "🗑️";
        delBtn.CustomMinimumSize = new Vector2(60, 32);
        delBtn.Disabled = isBuiltin;
        if (!isBuiltin)
        {
            delBtn.Pressed += () => OnDeleteFaction(row);
        }
        hbox.AddChild(delBtn);
        
        row.Container = hbox;
        return row;
    }
    
    private void OnAddFaction()
    {
        var factions = TeamHelper.GetAllFactions();
        if (factions.Count >= MaxFactions)
        {
            ShowToast("已达到最大势力数量（100个）");
            return;
        }
        
        // 生成新ID
        int newIndex = 3; // 从Player3开始
        string newId;
        do
        {
            newId = $"Player{newIndex}";
            newIndex++;
        } while (TeamHelper.GetFaction(newId) != null);
        
        // 随机颜色
        var rand = new Random();
        var primary = new Color((float)rand.NextDouble(), (float)rand.NextDouble(), (float)rand.NextDouble());
        var secondary = primary.Lightened(0.3f);
        
        var config = new FactionConfig
        {
            Id = newId,
            DisplayName = $"势力{newIndex - 1}",
            PrimaryColor = primary,
            SecondaryColor = secondary,
            IsActive = true,
            SortOrder = newIndex - 1
        };
        
        if (TeamHelper.RegisterFaction(config))
        {
            RefreshList();
        }
    }
    
    private void OnDeleteFaction(FactionRow row)
    {
        if (row.Config == null) return;
        
        TeamHelper.RemoveFaction(row.Config.Id);
        RefreshList();
    }
    
    private void OnSaveFactions()
    {
        // 从UI读取数据更新到TeamHelper
        foreach (var row in rows)
        {
            if (row.Config == null) continue;
            
            row.Config.IsActive = row.ActiveCheck.ButtonPressed;
            row.Config.DisplayName = row.NameEdit.Text;
            row.Config.PrimaryColor = row.PrimaryColorBtn.Color;
            row.Config.SecondaryColor = row.SecondaryColorBtn.Color;
        }
        
        TeamHelper.SaveFactionsToExtraSave();
        // 即时刷新场景内所有单位/设施/兵器颜色（内部会清缓存）
        FactionPaletteManager.RefreshAllVisuals(GetTree()?.Root);
        
        ShowToast("✅ 势力配置已保存，颜色已即时刷新。");
    }
    
    private void OnResetDefaults()
    {
        TeamHelper.InitializeDefaults();
        FactionPaletteManager.RefreshAllVisuals(GetTree()?.Root);
        RefreshList();
        ShowToast("已恢复默认势力配置");
    }
    
    private void ShowToast(string message)
    {
        var toast = new Label();
        toast.Text = message;
        toast.AddThemeFontSizeOverride("font_size", 14);
        toast.AddThemeColorOverride("font_color", Colors.LightGreen);
        toast.HorizontalAlignment = HorizontalAlignment.Center;
        
        AddChild(toast);
        
        var tween = CreateTween();
        tween.TweenInterval(2.0);
        tween.TweenCallback(Callable.From(() => toast.QueueFree()));
    }
    
    /// <summary>
    /// 势力行数据封装
    /// </summary>
    private class FactionRow
    {
        public FactionConfig Config;
        public HBoxContainer Container;
        public CheckBox ActiveCheck;
        public LineEdit NameEdit;
        public ColorPickerButton PrimaryColorBtn;
        public ColorPickerButton SecondaryColorBtn;
        
        public void Dispose()
        {
            if (Container != null && IsInstanceValid(Container))
            {
                Container.QueueFree();
            }
        }
    }
}
