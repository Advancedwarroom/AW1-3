// TranslateGroup.cs - 翻译组 (Translation Group) v2.0
// 支持自动检测系统语言 + 左下角语言切换按钮UI
// 支持7种语言: 英语/日语/韩语/德语/俄语/简体中文/繁体中文
using Godot;
using System;
using System.Collections.Generic;

public partial class TranslateGroup : Node
{
    public static TranslateGroup Instance { get; private set; }

    [ExportGroup("")]
    [Export] public GameLanguage CurrentLanguage = GameLanguage.SimplifiedChinese;
    [Export] public bool AutoDetectLanguage = true;

    // ===== UI 容器（AutoLoad 时动态创建 CanvasLayer）=====
    private CanvasLayer uiLayer;

    // ===== UI 元素 =====
    private Button langButton;
    private Panel langPanel;
    private VBoxContainer langList;
    private bool isLangPanelOpen = false;

    private Dictionary<string, Dictionary<GameLanguage, string>> translations;
    private Dictionary<GameLanguage, string> languageNames;
    private Dictionary<string, GameLanguage> localeToLanguage;

    public override void _Ready()
    {
        Instance = this;
        AddToGroup("translate_group");

        InitializeLanguageMaps();
        InitializeTranslations();

        if (AutoDetectLanguage)
        {
            DetectSystemLanguage();
        }

        // AutoLoad 时创建 CanvasLayer 承载 UI，确保按钮能显示在最上层
        uiLayer = new CanvasLayer();
        uiLayer.Name = "TranslateGroupUI";
        uiLayer.Layer = 100; // 最上层
        AddChild(uiLayer);

        CreateLanguageButton();
        CreateLanguagePanel();

        GD.Print($"[TranslateGroup] Language initialized: {GetLanguageDisplayName(CurrentLanguage)}");
        RefreshAllUI(); // 初始扫描翻译场景树中所有中文文本
    }

    // ==================== 系统语言检测 ====================
    public void DetectSystemLanguage()
    {
        string locale = OS.GetLocale();
        string languageCode = OS.GetLocaleLanguage();
        GD.Print($"[TranslateGroup] System locale: {locale}, language: {languageCode}");

        foreach (var kvp in localeToLanguage)
        {
            if (locale.StartsWith(kvp.Key) || languageCode == kvp.Key)
            {
                CurrentLanguage = kvp.Value;
                GD.Print($"[TranslateGroup] Auto-detected: {GetLanguageDisplayName(CurrentLanguage)}");
                return;
            }
        }
        CurrentLanguage = GameLanguage.English;
        GD.Print("[TranslateGroup] Fallback to English");
    }

    // ==================== 核心翻译方法 ====================
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (translations.TryGetValue(key, out var langDict))
        {
            if (langDict.TryGetValue(CurrentLanguage, out string translated))
                return translated;
            if (langDict.TryGetValue(GameLanguage.English, out string english))
                return english;
            if (langDict.TryGetValue(GameLanguage.SimplifiedChinese, out string chinese))
                return chinese;
        }
        return key;
    }

    public string GetF(string key, params object[] args)
    {
        string format = Get(key);
        try { return string.Format(format, args); }
        catch { return format; }
    }

public void SetLanguage(GameLanguage lang)
{
    CurrentLanguage = lang;
    UpdateLangButtonText();
    GD.Print($"[TranslateGroup] Switched to: {GetLanguageDisplayName(lang)}");
    EmitSignal(SignalName.LanguageChanged, (int)lang);
    RefreshAllRegistered(); // ← 加这行！
}

    public string GetLanguageDisplayName(GameLanguage lang)
    {
        return languageNames.GetValueOrDefault(lang, "Unknown");
    }

    public string GetCurrentLanguageDisplayName()
    {
        return GetLanguageDisplayName(CurrentLanguage);
    }

    [Signal]
    public delegate void LanguageChangedEventHandler(int languageCode);
}
