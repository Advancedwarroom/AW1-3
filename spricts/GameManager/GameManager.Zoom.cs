// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// GameManager · Zoom（由 GameManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GameManager
{

    // ========== ✅ 新增：缩放滑块 ==========
    private void SetupZoomSlider()
    {
        var mainScene = GetTree().CurrentScene;
        var canvas = mainScene.GetNodeOrNull<CanvasLayer>("CanvasLayer");
        if (canvas == null) return;

        // 获取或查找Camera
        cameraController = mainScene.GetNodeOrNull<CameraTouchController>("CameraController");
        if (cameraController == null) return;

        // 创建滑块容器
        var sliderPanel = new Control();
        sliderPanel.Name = "ZoomSliderPanel";
        sliderPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        sliderPanel.OffsetLeft = -80;
        sliderPanel.OffsetTop = 200;
        sliderPanel.OffsetRight = 0;
        sliderPanel.OffsetBottom = 500;
        sliderPanel.CustomMinimumSize = new Godot.Vector2(60, 300);
        canvas.AddChild(sliderPanel);

        // 背景
        var bg = new ColorRect();
        bg.Name = "ZoomBg";
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bg.Color = new Color(0, 0, 0, 0.6f);
        sliderPanel.AddChild(bg);

        // 标题
        var title = new Label();
        title.Name = "ZoomTitle";
        title.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        TranslateGroup.SetText(title, "缩放");
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeColorOverride("font_color", Colors.White);
        title.AddThemeFontSizeOverride("font_size", 14);
        title.CustomMinimumSize = new Godot.Vector2(0, 20);
        sliderPanel.AddChild(title);

        // 大圆（可拖动区域）
        var knob = new Button();
        knob.Name = "ZoomKnob";
        knob.SetAnchorsPreset(Control.LayoutPreset.Center);
        knob.CustomMinimumSize = new Godot.Vector2(48, 48);
        knob.Size = new Godot.Vector2(48, 48);
        knob.Text = "●";
        knob.AddThemeFontSizeOverride("font_size", 28);
        knob.AddThemeColorOverride("font_color", new Color(1, 0.8f, 0.2f));
        var knobStyle = new StyleBoxFlat();
        knobStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        knobStyle.SetCornerRadiusAll(24);
        knob.AddThemeStyleboxOverride("normal", knobStyle);
        sliderPanel.AddChild(knob);

        // 百分比标签
        var percentLabel = new Label();
        percentLabel.Name = "ZoomPercent";
        percentLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        percentLabel.Text = "100%";
        percentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        percentLabel.AddThemeColorOverride("font_color", Colors.White);
        percentLabel.AddThemeFontSizeOverride("font_size", 12);
        percentLabel.CustomMinimumSize = new Godot.Vector2(0, 20);
        sliderPanel.AddChild(percentLabel);

        // 滑块逻辑：处理拖动
        bool isDraggingKnob = false;
        float knobStartY = 0;
        float zoomStartPercent = 0;

        knob.ButtonDown += () =>
        {
            isDraggingKnob = true;
            knobStartY = sliderPanel.GetGlobalMousePosition().Y;
            zoomStartPercent = cameraController.ZoomPercent;
        };

        knob.ButtonUp += () =>
        {
            isDraggingKnob = false;
        };

        // 使用 Godot 的 _Process 来更新
        var updateTimer = new Timer();
        updateTimer.WaitTime = 0.016f;
        updateTimer.OneShot = false;
        updateTimer.Timeout += () =>
        {
            if (!isDraggingKnob) return;

            float currentY = sliderPanel.GetGlobalMousePosition().Y;
            float deltaY = knobStartY - currentY; // 向上拖动 = 放大
            float deltaPercent = deltaY / 200f * 100f; // 200px 范围对应 100%
            float newPercent = Mathf.Clamp(zoomStartPercent + deltaPercent, 1f, 100f);

            cameraController.SetZoomPercent(newPercent);
            percentLabel.Text = $"{Mathf.RoundToInt(newPercent)}%";
        };
        sliderPanel.AddChild(updateTimer);
        updateTimer.Start();

        // 点击面板直接跳转
        sliderPanel.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
            {
                var localPos = sliderPanel.GetLocalMousePosition();
                float t = 1f - (localPos.Y / sliderPanel.Size.Y);
                t = Mathf.Clamp(t, 0f, 1f);
                float newPercent = t * 100f;
                cameraController.SetZoomPercent(newPercent);
                percentLabel.Text = $"{Mathf.RoundToInt(newPercent)}%";
            }
        };
    }
}
