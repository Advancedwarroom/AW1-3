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
        InitEngine(); // 词条合并 + 索引 + 注册到 Godot TranslationServer

        // 优先使用玩家上次手动选择的语言，其次自动检测
        if (!LoadSavedLanguage() && AutoDetectLanguage)
        {
            DetectSystemLanguage();
        }
        ApplyLocale();

        // AutoLoad 时创建 CanvasLayer 承载 UI，确保按钮能显示在最上层
        uiLayer = new CanvasLayer();
        uiLayer.Name = "TranslateGroupUI";
        uiLayer.Layer = 100; // 最上层
        uiLayer.AutoTranslateMode = AutoTranslateModeEnum.Disabled; // 语言名称本身不参与翻译
        AddChild(uiLayer);

        CreateLanguageButton();
        CreateLanguagePanel();

        GD.Print($"[TranslateGroup] Language initialized: {GetLanguageDisplayName(CurrentLanguage)}");
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
        return Translate(key, CurrentLanguage);
    }

    public string GetF(string key, params object[] args)
    {
        // 精确键：先译格式串再代入；否则先代入再整体翻译（走模式/分词）
        if (translations != null && translations.ContainsKey(key))
        {
            string format = Get(key);
            try { return string.Format(format, args); }
            catch { return format; }
        }
        string raw;
        try { raw = string.Format(key, args); }
        catch { raw = key; }
        return Get(raw);
    }

    public void SetLanguage(GameLanguage lang)
    {
        CurrentLanguage = lang;
        UpdateLangButtonText();
        GD.Print($"[TranslateGroup] Switched to: {GetLanguageDisplayName(lang)}");
        ApplyLocale();          // 引擎自动重译所有 auto_translate 控件（含代码后设的文本/提示/菜单项）
        SaveLanguage();         // 记住选择
        SaveMissingReport();
        RefreshAllRegistered(); // SetText 注册的控件（含格式化参数）重新生成
        EmitSignal(SignalName.LanguageChanged, (int)lang);
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
