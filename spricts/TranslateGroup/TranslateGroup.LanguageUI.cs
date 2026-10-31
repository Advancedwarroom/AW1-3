// TranslateGroup.cs - 翻译组 (Translation Group) v2.0
// 支持自动检测系统语言 + 左下角语言切换按钮UI
// 支持7种语言: 英语/日语/韩语/德语/俄语/简体中文/繁体中文
using Godot;
using System;
using System.Collections.Generic;

// TranslateGroup · LanguageUI（由 TranslateGroup.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class TranslateGroup
{

    // ==================== 语言切换 UI ====================
    private void CreateLanguageButton()
    {
        langButton = new Button();
        langButton.Name = "LangButton";
        langButton.Text = GetLanguageDisplayName(CurrentLanguage);
        langButton.CustomMinimumSize = new Vector2(100, 40);
        langButton.AddThemeFontSizeOverride("font_size", 14);

        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.15f, 0.15f, 0.2f, 0.85f);
        style.SetCornerRadiusAll(8);
        style.BorderWidthBottom = 2;
        style.BorderWidthLeft = 2;
        style.BorderWidthRight = 2;
        style.BorderWidthTop = 2;
        style.BorderColor = new Color(0.4f, 0.4f, 0.5f, 0.8f);
        langButton.AddThemeStyleboxOverride("normal", style);

        var hover = new StyleBoxFlat();
        hover.BgColor = new Color(0.25f, 0.25f, 0.35f, 0.9f);
        hover.SetCornerRadiusAll(8);
        hover.BorderWidthBottom = 2;
        hover.BorderWidthLeft = 2;
        hover.BorderWidthRight = 2;
        hover.BorderWidthTop = 2;
        hover.BorderColor = new Color(0.6f, 0.6f, 0.8f, 1f);
        langButton.AddThemeStyleboxOverride("hover", hover);

        langButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        langButton.Pressed += OnLangButtonPressed;

        uiLayer.AddChild(langButton);

        // 左下角定位
        var viewportSize = GetViewport().GetVisibleRect().Size;
        langButton.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        langButton.Position = new Vector2(15, viewportSize.Y - 55);
    }

    private void CreateLanguagePanel()
    {
        langPanel = new Panel();
        langPanel.Name = "LangPanel";
        langPanel.CustomMinimumSize = new Vector2(160, 280);
        langPanel.Visible = false;

        var panelStyle = new StyleBoxFlat();
        panelStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        panelStyle.SetCornerRadiusAll(10);
        panelStyle.BorderWidthBottom = 2;
        panelStyle.BorderWidthLeft = 2;
        panelStyle.BorderWidthRight = 2;
        panelStyle.BorderWidthTop = 2;
        panelStyle.BorderColor = new Color(0.4f, 0.4f, 0.55f, 0.9f);
        langPanel.AddThemeStyleboxOverride("panel", panelStyle);

        langList = new VBoxContainer();
        langList.Name = "LangList";
        langList.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        langList.OffsetLeft = 8;
        langList.OffsetRight = -8;
        langList.OffsetTop = 8;
        langList.OffsetBottom = -8;
        langList.AddThemeConstantOverride("separation", 4);

        // 标题
        var title = new Label();
        title.Text = "Language / 语言";
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.9f));
        langList.AddChild(title);

        // 分隔线
        var sep = new HSeparator();
        sep.CustomMinimumSize = new Vector2(0, 4);
        langList.AddChild(sep);

        // 7个语言按钮
        var langs = new[]
        {
            GameLanguage.English,
            GameLanguage.Japanese,
            GameLanguage.Korean,
            GameLanguage.German,
            GameLanguage.Russian,
            GameLanguage.SimplifiedChinese,
            GameLanguage.TraditionalChinese
        };

        foreach (var lang in langs)
        {
            var btn = new Button();
            btn.Text = languageNames[lang];
            btn.CustomMinimumSize = new Vector2(0, 32);
            btn.AddThemeFontSizeOverride("font_size", 13);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.2f, 0.2f, 0.25f, 0.7f);
            btnStyle.SetCornerRadiusAll(6);
            btn.AddThemeStyleboxOverride("normal", btnStyle);

            var btnHover = new StyleBoxFlat();
            btnHover.BgColor = new Color(0.35f, 0.35f, 0.45f, 0.9f);
            btnHover.SetCornerRadiusAll(6);
            btn.AddThemeStyleboxOverride("hover", btnHover);

            var currentLang = lang;
            btn.Pressed += () => OnLanguageSelected(currentLang);
            langList.AddChild(btn);
        }

        langPanel.AddChild(langList);
        uiLayer.AddChild(langPanel);

        // 面板位置: 按钮上方
        PositionLangPanel();
    }

    private void PositionLangPanel()
    {
        if (langPanel == null || langButton == null) return;
        var btnPos = langButton.GlobalPosition;
        langPanel.Position = new Vector2(btnPos.X, btnPos.Y - langPanel.CustomMinimumSize.Y - 8);
    }

    private void OnLangButtonPressed()
    {
        isLangPanelOpen = !isLangPanelOpen;
        langPanel.Visible = isLangPanelOpen;
        if (isLangPanelOpen) PositionLangPanel();
    }

    private void OnLanguageSelected(GameLanguage lang)
    {
        SetLanguage(lang);
        isLangPanelOpen = false;
        langPanel.Visible = false;
    }

    private void UpdateLangButtonText()
    {
        if (langButton != null)
            langButton.Text = GetLanguageDisplayName(CurrentLanguage);
    }

    public override void _Input(InputEvent @event)
    {
        // 点击面板外部关闭
        if (isLangPanelOpen && @event is InputEventMouseButton mb && mb.Pressed)
        {
            var mousePos = GetViewport().GetMousePosition();
            if (!langPanel.GetGlobalRect().HasPoint(mousePos) &&
                !langButton.GetGlobalRect().HasPoint(mousePos))
            {
                isLangPanelOpen = false;
                langPanel.Visible = false;
            }
        }
    }
// 存储所有注册的控件（key + 可选格式化参数）
private static System.Collections.Generic.List<(Control ctrl, string key, object[] args)> _registered = new();

// 设置文本并自动注册，语言切换时自动刷新
// 支持动态参数：SetText(label, "回合 {0}", turn)
public static void SetText(Control control, string key, params object[] args)
{
    string text = args.Length > 0 ? TF(key, args) : T(key);
    
    if (control is Label lbl) lbl.Text = text;
    else if (control is Button btn) btn.Text = text;
    else if (control is RichTextLabel rtl) rtl.Text = text;
    
    // 去重：如果同一个控件已经注册过，更新 key 和 args
    for (int i = _registered.Count - 1; i >= 0; i--)
    {
        if (_registered[i].ctrl == control)
        {
            _registered.RemoveAt(i);
            break;
        }
    }
    _registered.Add((control, key, args));
}

// 语言切换时自动刷新所有注册的控件
private void RefreshAllRegistered()
{
    for (int i = _registered.Count - 1; i >= 0; i--)
    {
        var (ctrl, key, args) = _registered[i];
        if (!IsInstanceValid(ctrl))
        {
            _registered.RemoveAt(i);
            continue;
        }
        string text = args.Length > 0 ? TF(key, args) : T(key);
        if (ctrl is Label lbl) lbl.Text = text;
        else if (ctrl is Button btn) btn.Text = text;
        else if (ctrl is RichTextLabel rtl) rtl.Text = text;
    }
}

// 强制整棵场景树重新翻译（一般无需调用：SetLanguage 切换 locale 时引擎会自动通知）
// 旧实现会把 tr_key 元数据写回文本，覆盖代码后来动态设置的内容，已废弃。
public void RefreshAllUI()
{
    var tree = GetTree();
    if (tree == null) return;
    tree.Root.PropagateNotification((int)NotificationTranslationChanged);
}

// 静态快捷方法：带格式化参数
public static string TF(string key, params object[] args)
{
    if (Instance == null) return string.Format(key, args);
    return Instance.GetF(key, args);
}

public static string T(string key)
{
    if (Instance == null) return key; 
    return Instance.Get(key);
}
    public override void _Notification(int what)
    {
        if (what == Control.NotificationResized)
        {
            // 窗口大小变化时重新定位
            if (langButton != null)
            {
                var viewportSize = GetViewport().GetVisibleRect().Size;
                langButton.Position = new Vector2(15, viewportSize.Y - 55);
                PositionLangPanel();
            }
        }
    }
}
