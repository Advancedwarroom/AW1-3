// EditorPanelDragger.cs - 通用面板拖拽管理器 + 编辑器集成
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class EditorPanelDragger : Node
{
    [Export] public TerrainEditor terrainEditor;

    private Dictionary<Control, DragState> draggablePanels = new();

    private class DragState
    {
        public bool isDragging = false;
        public Vector2 dragOffset = Vector2.Zero;
        public Control titleBar = null;
    }

    public override void _Ready()
    {
        AddToGroup("editor_panel_dragger");
        if (terrainEditor == null)
            terrainEditor = GetTree().GetFirstNodeInGroup("terrain_editor") as TerrainEditor;
        CallDeferred(nameof(DeferredRegisterPanels));
    }

    private void DeferredRegisterPanels()
    {
        if (terrainEditor == null) return;
        var fields = terrainEditor.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Panel))
            {
                var panel = field.GetValue(terrainEditor) as Panel;
                if (panel != null && IsInstanceValid(panel))
                    MakePanelDraggable(panel, field.Name);
            }
        }

        // 同时注册 MapEditorHelper 和 EditorSaveSlots 的面板
        var helper = GetTree().GetFirstNodeInGroup("map_editor_helper") as MapEditorHelper;
        if (helper != null)
        {
            var helperPanel = helper.GetNodeOrNull<Panel>("HelperPanel");
            if (helperPanel != null) MakePanelDraggable(helperPanel, "HelperPanel");
        }

        var slots = GetTree().GetFirstNodeInGroup("editor_save_slots") as EditorSaveSlots;
        if (slots != null)
        {
            var slotsPanel = slots.GetNodeOrNull<Panel>("SlotsPanel");
            if (slotsPanel != null) MakePanelDraggable(slotsPanel, "SlotsPanel");
        }
    }

    public void MakePanelDraggable(Panel panel, string panelName)
    {
        if (panel == null || draggablePanels.ContainsKey(panel)) return;

        var dragBar = new ColorRect();
        dragBar.Name = $"DragBar_{panelName}";
        dragBar.Color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
        dragBar.CustomMinimumSize = new Vector2(0, 22);
        dragBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        dragBar.MouseFilter = Control.MouseFilterEnum.Stop;

        panel.AddChild(dragBar);
        if (dragBar.GetParent() == panel && panel.GetChildCount() > 1)
            panel.MoveChild(dragBar, 0);

        var titleLabel = new Label();
        titleLabel.Text = ""; // 面板自己已有标题，这里留空
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
        dragBar.AddChild(titleLabel);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        titleLabel.CustomMinimumSize = dragBar.CustomMinimumSize;

        var state = new DragState { titleBar = dragBar };
        draggablePanels[panel] = state;
        dragBar.GuiInput += (evt) => OnDragBarInput(evt, panel, state);
    }

    private void OnDragBarInput(InputEvent @event, Panel panel, DragState state)
    {
        if (@event is InputEventMouseButton btn && btn.ButtonIndex == MouseButton.Left)
        {
            if (btn.Pressed)
            {
                state.isDragging = true;
                state.dragOffset = panel.GetGlobalMousePosition() - panel.GlobalPosition;
                panel.ZIndex = 100;
            }
            else
            {
                state.isDragging = false;
            }
        }
    }

    public override void _Process(double delta)
    {
        foreach (var kvp in draggablePanels)
        {
            var panel = kvp.Key;
            var state = kvp.Value;
            if (state.isDragging && panel != null && IsInstanceValid(panel))
            {
                panel.GlobalPosition = panel.GetGlobalMousePosition() - state.dragOffset;
            }
        }
    }
}
