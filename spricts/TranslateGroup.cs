// TranslateGroup.cs - 翻译组 (Translation Group) v2.0
// 支持自动检测系统语言 + 左下角语言切换按钮UI
// 支持7种语言: 英语/日语/韩语/德语/俄语/简体中文/繁体中文
using Godot;
using System;
using System.Collections.Generic;

public enum GameLanguage
{
    English,            // 英语
    Japanese,           // 日语
    Korean,             // 韩语
    German,             // 德语
    Russian,            // 俄语
    SimplifiedChinese,  // 简体中文
    TraditionalChinese  // 繁体中文
}

public partial class TranslateGroup : Node
{
    public static TranslateGroup Instance { get; private set; }

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
// 存储所有注册的控件
private static System.Collections.Generic.List<(Control ctrl, string key)> _registered = new();

// 设置文本并自动注册，语言切换时自动刷新
public static void SetText(Control control, string key)
{
    if (control is Label lbl) lbl.Text = T(key);
    else if (control is Button btn) btn.Text = T(key);
    else if (control is RichTextLabel rtl) rtl.Text = T(key);
    
    // 去重：如果同一个控件已经注册过，更新 key
    for (int i = _registered.Count - 1; i >= 0; i--)
    {
        if (_registered[i].ctrl == control)
        {
            _registered.RemoveAt(i);
            break;
        }
    }
    _registered.Add((control, key));
}

// 语言切换时自动刷新所有注册的控件
private void RefreshAllRegistered()
{
    for (int i = _registered.Count - 1; i >= 0; i--)
    {
        var (ctrl, key) = _registered[i];
        if (!IsInstanceValid(ctrl))
        {
            _registered.RemoveAt(i); // 清理已销毁的控件
            continue;
        }
        if (ctrl is Label lbl) lbl.Text = T(key);
        else if (ctrl is Button btn) btn.Text = T(key);
        else if (ctrl is RichTextLabel rtl) rtl.Text = T(key);
    }
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

    // ==================== 语言映射 ====================
    private void InitializeLanguageMaps()
    {
        languageNames = new Dictionary<GameLanguage, string>
        {
            [GameLanguage.English] = "English",
            [GameLanguage.Japanese] = "日本語",
            [GameLanguage.Korean] = "한국어",
            [GameLanguage.German] = "Deutsch",
            [GameLanguage.Russian] = "Русский",
            [GameLanguage.SimplifiedChinese] = "简体中文",
            [GameLanguage.TraditionalChinese] = "繁體中文"
        };

        localeToLanguage = new Dictionary<string, GameLanguage>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = GameLanguage.English,
            ["en_US"] = GameLanguage.English,
            ["en_GB"] = GameLanguage.English,
            ["ja"] = GameLanguage.Japanese,
            ["ja_JP"] = GameLanguage.Japanese,
            ["ko"] = GameLanguage.Korean,
            ["ko_KR"] = GameLanguage.Korean,
            ["de"] = GameLanguage.German,
            ["de_DE"] = GameLanguage.German,
            ["ru"] = GameLanguage.Russian,
            ["ru_RU"] = GameLanguage.Russian,
            ["zh"] = GameLanguage.SimplifiedChinese,
            ["zh_CN"] = GameLanguage.SimplifiedChinese,
            ["zh_SG"] = GameLanguage.SimplifiedChinese,
            ["zh_Hans"] = GameLanguage.SimplifiedChinese,
            ["zh_TW"] = GameLanguage.TraditionalChinese,
            ["zh_HK"] = GameLanguage.TraditionalChinese,
            ["zh_Hant"] = GameLanguage.TraditionalChinese
        };
    }

    // ==================== 翻译字典 ====================
    private void InitializeTranslations()
    {
        translations = new Dictionary<string, Dictionary<GameLanguage, string>>();

        void Add(string key, string en, string ja, string ko, string de, string ru, string zhCN, string zhTW)
        {
            translations[key] = new Dictionary<GameLanguage, string>
            {
                [GameLanguage.English] = en,
                [GameLanguage.Japanese] = ja,
                [GameLanguage.Korean] = ko,
                [GameLanguage.German] = de,
                [GameLanguage.Russian] = ru,
                [GameLanguage.SimplifiedChinese] = zhCN,
                [GameLanguage.TraditionalChinese] = zhTW
            };
        }

        // ===== 通用按钮 & UI =====
        Add("确认", "Confirm", "確認", "확인", "Bestätigen", "Подтвердить", "确认", "確認");
        Add("取消", "Cancel", "キャンセル", "취소", "Abbrechen", "Отмена", "取消", "取消");
        Add("是", "Yes", "はい", "예", "Ja", "Да", "是", "是");
        Add("否", "No", "いいえ", "아니오", "Nein", "Нет", "否", "否");
        Add("删除", "Delete", "削除", "삭제", "Löschen", "Удалить", "删除", "刪除");
        Add("编辑", "Edit", "編集", "편집", "Bearbeiten", "Редактировать", "编辑", "編輯");
        Add("保存", "Save", "保存", "저장", "Speichern", "Сохранить", "保存", "儲存");
        Add("加载", "Load", "読込", "불러오기", "Laden", "Загрузить", "加载", "載入");
        Add("应用", "Apply", "適用", "적용", "Anwenden", "Применить", "应用", "套用");
        Add("关闭", "Close", "閉じる", "닫기", "Schließen", "Закрыть", "关闭", "關閉");
        Add("返回", "Back", "戻る", "뒤로", "Zurück", "Назад", "返回", "返回");
        Add("设置", "Settings", "設定", "설정", "Einstellungen", "Настройки", "设置", "設定");
        Add("开始", "Start", "開始", "시작", "Start", "Старт", "开始", "開始");
        Add("退出", "Exit", "終了", "종료", "Beenden", "Выход", "退出", "退出");
        Add("完成", "Done", "完了", "완료", "Fertig", "Готово", "完成", "完成");
        Add("就绪", "Ready", "準備完了", "준비", "Bereit", "Готово", "就绪", "就緒");
        Add("待机", "Standby", "待機", "대기", "Bereitschaft", "Ожидание", "待机", "待機");
        Add("未知", "Unknown", "不明", "알 수 없음", "Unbekannt", "Неизвестно", "未知", "未知");
        Add("无", "None", "なし", "없음", "Keine", "Нет", "无", "無");
        Add("无限", "Infinite", "無限", "무한", "Unendlich", "Бесконечно", "无限", "無限");

        // ===== 游戏核心术语 =====
        Add("单位", "Unit", "ユニット", "유닛", "Einheit", "Юнит", "单位", "單位");
        Add("兵器", "Weapon", "兵器", "병기", "Waffe", "Оружие", "兵器", "兵器");
        Add("设施", "Facility", "施設", "시설", "Einrichtung", "Сооружение", "设施", "設施");
        Add("地形", "Terrain", "地形", "지형", "Gelände", "Местность", "地形", "地形");
        Add("团队", "Team", "チーム", "팀", "Team", "Команда", "团队", "團隊");
        Add("生命值", "HP", "HP", "HP", "LP", "ОЗ", "生命值", "生命值");
        Add("攻击力", "Attack", "攻撃力", "공격력", "Angriff", "Атака", "攻击力", "攻擊力");
        Add("防御力", "Defense", "防御力", "방어력", "Verteidigung", "Защита", "防御力", "防禦力");
        Add("移动力", "Movement", "移動力", "이동력", "Bewegung", "Передвижение", "移动力", "移動力");
        Add("燃料", "Fuel", "燃料", "연료", "Treibstoff", "Топливо", "燃料", "燃料");
        Add("弹药", "Ammo", "弾薬", "탄약", "Munition", "Боеприпасы", "弹药", "彈藥");
        Add("射程", "Range", "射程", "사거리", "Reichweite", "Дальность", "射程", "射程");
        Add("视野", "Vision", "視界", "시야", "Sicht", "Обзор", "视野", "視野");
        Add("回合", "Turn", "ターン", "턴", "Runde", "Ход", "回合", "回合");
        Add("资金", "Funds", "資金", "자금", "Geld", "Средства", "资金", "資金");
        Add("占领", "Capture", "占領", "점령", "Eroberung", "Захват", "占领", "佔領");
        Add("补给", "Supply", "補給", "보급", "Versorgung", "Снабжение", "补给", "補給");
        Add("治疗", "Heal", "回復", "치료", "Heilung", "Лечение", "治疗", "治療");
        Add("攻击", "Attack", "攻撃", "공격", "Angriff", "Атака", "攻击", "攻擊");
        Add("反击", "Counter", "反撃", "반격", "Gegenangriff", "Контратака", "反击", "反擊");
        Add("移动", "Move", "移動", "이동", "Bewegen", "Перемещение", "移动", "移動");
        Add("等待", "Wait", "待機", "대기", "Warten", "Ожидание", "等待", "等待");
        Add("自爆", "Explode", "自爆", "자폭", "Explosion", "Взрыв", "自爆", "自爆");
        Add("照明", "Illuminate", "照明", "조명", "Beleuchtung", "Освещение", "照明", "照明");
        Add("运输", "Transport", "輸送", "수송", "Transport", "Транспорт", "运输", "運輸");
        Add("装载", "Load", "搭載", "적재", "Beladen", "Загрузка", "装载", "裝載");
        Add("卸载", "Unload", "降下", "하차", "Entladen", "Выгрузка", "卸载", "卸載");

        // ===== 单位名称 =====
        Add("步兵", "Infantry", "歩兵", "보병", "Infanterie", "Пехота", "步兵", "步兵");
        Add("机甲", "Mech", "メック", "메크", "Mech", "Мех", "机甲", "機甲");
        Add("坦克", "Tank", "戦車", "전차", "Panzer", "Танк", "坦克", "戰車");
        Add("轻型坦克", "Light Tank", "軽戦車", "경전차", "Leichter Panzer", "Лёгкий танк", "轻型坦克", "輕型戰車");
        Add("重型坦克", "Heavy Tank", "重戦車", "중전차", "Schwerer Panzer", "Тяжёлый танк", "重型坦克", "重型戰車");
        Add("侦察车", "Recon", "偵察車", "정찰차", "Aufklärer", "Разведчик", "侦察车", "偵察車");
        Add("防空高炮", "Anti-Air", "対空砲", "대공포", "Flugabwehr", "Зенитка", "防空高炮", "防空高炮");
        Add("反坦克炮", "Anti-Tank", "対戦車砲", "대전차포", "Panzerabwehr", "ПТО", "反坦克炮", "反坦克炮");
        Add("火箭炮", "Rocket", "ロケット", "로켓", "Rakete", "Ракета", "火箭炮", "火箭炮");
        Add("自行火炮", "Artillery", "自走砲", "자주포", "Artillerie", "Артиллерия", "自行火炮", "自走炮");
        Add("装甲运兵车", "APC", "装甲車", "장갑차", "MTW", "БТР", "装甲运兵车", "裝甲運兵車");
        Add("摩托兵", "Bike", "バイク", "바이크", "Motorrad", "Мотоцикл", "摩托兵", "摩托兵");
        Add("照明车", "Flare", "照明車", "조명차", "Leuchtfahrzeug", "Осветитель", "照明车", "照明車");
        Add("飞弹", "FlyBomb", "ミサイル", "미사일", "Flugbombe", "Лет. бомба", "飞弹", "飛彈");
        Add("史莱姆", "Oozium", "ウージウム", "우지움", "Oozium", "Узиум", "史莱姆", "史萊姆");
        Add("运输车", "PipeRunner", "パイプ", "파이프", "Rohr", "Трубовоз", "运输车", "運輸車");
        Add("黑水晶", "Crystal", "黒水晶", "흑수정", "Kristall", "Кристалл", "黑水晶", "黑水晶");
        Add("黑炮", "Black Cannon", "黒砲", "흑포", "Schwarze Kanone", "Чёрная пушка", "黑炮", "黑炮");
        Add("死光炮", "Death Ray", "死光砲", "사광포", "Todesstrahl", "Луч смерти", "死光炮", "死光炮");
        Add("激光炮", "Laser", "レーザー", "레이저", "Laser", "Лазер", "激光炮", "雷射炮");
        Add("战斗机", "Fighter", "戦闘機", "전투기", "Jäger", "Истребитель", "战斗机", "戰鬥機");

        // ===== 地形名称 =====
        Add("平原", "Plains", "平原", "평원", "Ebene", "Равнина", "平原", "平原");
        Add("森林", "Forest", "森", "숲", "Wald", "Лес", "森林", "森林");
        Add("山地", "Mountain", "山", "산", "Berg", "Гора", "山地", "山地");
        Add("海洋", "Sea", "海", "바다", "Meer", "Море", "海洋", "海洋");
        Add("河流", "River", "河", "강", "Fluss", "Река", "河流", "河流");
        Add("公路", "Road", "道路", "도로", "Straße", "Дорога", "公路", "公路");
        Add("沙滩", "Beach", "砂浜", "해변", "Strand", "Пляж", "沙滩", "沙灘");
        Add("废墟", "Ruins", "廃墟", "폐허", "Ruinen", "Руины", "废墟", "廢墟");
        Add("桥梁", "Bridge", "橋", "다리", "Brücke", "Мост", "桥梁", "橋樑");
        Add("铁轨", "Rail", "鉄道", "철도", "Schiene", "Ж/д", "铁轨", "鐵軌");
        Add("悬崖", "Cliff", "崖", "절벽", "Klippe", "Утёс", "悬崖", "懸崖");
        Add("岩浆", "Magma", "マグマ", "마그마", "Magma", "Магма", "岩浆", "岩漿");
        Add("漩涡", "Whirlpool", "渦", "소용돌이", "Strudel", "Водоворот", "漩涡", "漩渦");
        Add("管道", "Pipe", "パイプ", "파이프", "Rohr", "Труба", "管道", "管道");
        Add("站台", "Station", "駅", "역", "Station", "Станция", "站台", "站台");

        // ===== 设施名称 =====
        Add("城市", "City", "都市", "도시", "Stadt", "Город", "城市", "城市");
        Add("基地", "Base", "基地", "기지", "Basis", "База", "基地", "基地");
        Add("机场", "Airport", "飛行場", "공항", "Flugplatz", "Аэродром", "机场", "機場");
        Add("港口", "Port", "港", "항구", "Hafen", "Порт", "港口", "港口");
        Add("工厂", "Factory", "工場", "공장", "Fabrik", "Завод", "工厂", "工廠");

        // ===== 移动方式 =====
        Add("步兵移动", "Foot", "歩行", "보행", "Fuß", "Пешком", "步兵移动", "步兵移動");
        Add("轮胎移动", "Tires", "タイヤ", "타이어", "Reifen", "Колёса", "轮胎移动", "輪胎移動");
        Add("履带移动", "Treads", "キャタピラ", "궤도", "Kette", "Гусеницы", "履带移动", "履帶移動");
        Add("机甲移动", "Mech Move", "メック移動", "메크 이동", "Mech-Bewegung", "Мех. ход", "机甲移动", "機甲移動");
        Add("海军移动", "Naval", "航海", "해군", "Marine", "Морской", "海军移动", "海軍移動");
        Add("飞机移动", "Air", "航空", "공군", "Luft", "Воздушный", "飞机移动", "飛機移動");
        Add("直升机移动", "Heli", "ヘリ", "헬리", "Hubschrauber", "Вертолёт", "直升机移动", "直升機移動");
        Add("管道行者移动", "Pipe", "パイプ歩行", "파이프 이동", "Rohr", "Трубопровод", "管道行者移动", "管道行者移動");
        Add("蒸汽机移动", "Steam", "蒸気", "증기", "Dampf", "Пар", "蒸汽机移动", "蒸汽機移動");
        Add("空天移动", "Space", "宇宙", "우주", "Weltraum", "Космос", "空天移动", "空天移動");
        Add("高铁移动", "High-Speed", "高速鉄道", "고속철도", "Hochgeschw.", "Выс. скорость", "高铁移动", "高鐵移動");
        Add("无人机移动", "Drone", "ドローン", "드론", "Drohne", "Дрон", "无人机移动", "無人機移動");
        Add("飞艇移动", "Airship", "飛行船", "비행선", "Luftschiff", "Дирижабль", "飞艇移动", "飛艇移動");
        Add("星舰移动", "Starship", "星艦", "성함", "Sternschiff", "Звездолёт", "星舰移动", "星艦移動");
        Add("岩浆登陆者移动", "Magma Land", "マグマ上陸", "마그마 상륙", "Magma-Landung", "Магма-высадка", "岩浆登陆者移动", "岩漿登陸者移動");
        Add("岩浆行者移动", "Magma Walk", "マグマ歩行", "마그마 보행", "Magma-Lauf", "Магма-ход", "岩浆行者移动", "岩漿行者移動");
        Add("普通火车移动", "Train", "列車", "열차", "Zug", "Поезд", "普通火车移动", "普通火車移動");
        Add("登陆艇移动", "Lander", "揚陸艇", "상륙정", "Landungsboot", "Десант", "登陆艇移动", "登陸艇移動");
        Add("船闸", "Lock", "閘門", "수문", "Schleuse", "Шлюз", "船闸", "船閘");

        // ===== 游戏状态 & 提示 =====
        Add("胜利", "Victory", "勝利", "승리", "Sieg", "Победа", "胜利", "勝利");
        Add("失败", "Defeat", "敗北", "패배", "Niederlage", "Поражение", "失败", "失敗");
        Add("平局", "Draw", "引き分け", "무승부", "Unentschieden", "Ничья", "平局", "平局");
        Add("获胜", "Win", "勝利", "승리", "Gewinn", "Победа", "获胜", "獲勝");
        Add("战败", "Lost", "敗北", "패배", "Verlust", "Проигрыш", "战败", "戰敗");
        Add("P1 获得胜利！", "P1 Wins!", "P1の勝利！", "P1 승리!", "P1 gewinnt!", "P1 победил!", "P1 获得胜利！", "P1 獲得勝利！");
        Add("P2 获得胜利！", "P2 Wins!", "P2の勝利！", "P2 승리!", "P2 gewinnt!", "P2 победил!", "P2 获得胜利！", "P2 獲得勝利！");
        Add("P1 战败", "P1 Defeated", "P1 敗北", "P1 패배", "P1 besiegt", "P1 проиграл", "P1 战败", "P1 戰敗");
        Add("P2 战败", "P2 Defeated", "P2 敗北", "P2 패배", "P2 besiegt", "P2 проиграл", "P2 战败", "P2 戰敗");
        Add("双方均未能取胜", "No Winner", "勝者なし", "승자 없음", "Kein Sieger", "Нет победителя", "双方均未能取胜", "雙方均未能取勝");
        Add("占领完成!", "Captured!", "占領完了!", "점령 완료!", "Erobert!", "Захвачено!", "占领完成!", "佔領完成!");
        Add("占领中断!", "Capture Interrupted!", "占領中断!", "점령 중단!", "Eroberung unterbrochen!", "Захват прерван!", "占领中断!", "佔領中斷!");
        Add("争夺中!", "Contesting!", "争奪中!", "쟁탈 중!", "Im Streit!", "Оспаривается!", "争夺中!", "爭奪中!");
        Add("占领中...", "Capturing...", "占領中...", "점령 중...", "Eroberung...", "Захват...", "占领中...", "佔領中...");
        Add("资金不足", "Insufficient Funds", "資金不足", "자금 부족", "Zu wenig Geld", "Недостаточно средств", "资金不足", "資金不足");
        Add("已达本回合生产上限", "Production Limit Reached", "生産上限", "생산 한도", "Produktionslimit", "Лимит производства", "已达本回合生产上限", "已達本回合生產上限");
        Add("格子已被占据", "Tile Occupied", "マス占有済", "칸 점유됨", "Feld belegt", "Клетка занята", "格子已被占据", "格子已被佔據");
        Add("未选择单位", "No Unit Selected", "未選択", "유닛 미선택", "Keine Einheit", "Юнит не выбран", "未选择单位", "未選擇單位");

        // ===== 按钮动作 =====
        Add("移动", "Move", "移動", "이동", "Bewegen", "Движение", "移动", "移動");
        Add("攻击", "Attack", "攻撃", "공격", "Angriff", "Атака", "攻击", "攻擊");
        Add("等待", "Wait", "待機", "대기", "Warten", "Ожидание", "等待", "等待");
        Add("占领", "Capture", "占領", "점령", "Erobern", "Захват", "占领", "佔領");
        Add("装载", "Load", "搭載", "적재", "Beladen", "Загрузка", "装载", "裝載");
        Add("卸载", "Unload", "降下", "하차", "Entladen", "Выгрузка", "卸载", "卸載");
        Add("补给", "Supply", "補給", "보급", "Versorgen", "Снабжение", "补给", "補給");
        Add("治疗", "Heal", "回復", "치료", "Heilen", "Лечение", "治疗", "治療");
        Add("信息", "Info", "情報", "정보", "Info", "Инфо", "信息", "資訊");
        Add("爆炸", "Explode", "爆発", "폭발", "Explodieren", "Взорвать", "爆炸", "爆炸");
        Add("确认爆炸", "Confirm Explode", "爆発確認", "폭발 확인", "Explosion best.", "Подтвердить взрыв", "确认爆炸", "確認爆炸");
        Add("照明", "Illuminate", "照明", "조명", "Beleuchten", "Осветить", "照明", "照明");
        Add("确认照明", "Confirm Light", "照明確認", "조명 확인", "Beleuchtung best.", "Подтвердить свет", "确认照明", "確認照明");
        Add("取消照明", "Cancel Light", "照明取消", "조명 취소", "Beleuchtung abb.", "Отменить свет", "取消照明", "取消照明");
        Add("旋转", "Rotate", "回転", "회전", "Drehen", "Повернуть", "旋转", "旋轉");

        // ===== 配置 & 编辑器 =====
        Add("编辑模式", "Edit Mode", "編集モード", "편집 모드", "Bearbeitungsmodus", "Режим редактора", "编辑模式", "編輯模式");
        Add("游玩模式", "Play Mode", "プレイモード", "플레이 모드", "Spielmodus", "Игровой режим", "游玩模式", "遊玩模式");
        Add("地图编辑器", "Map Editor", "マップエディタ", "맵 에디터", "Karteneditor", "Редактор карт", "地图编辑器", "地圖編輯器");
        Add("生成地图", "Generate Map", "マップ生成", "맵 생성", "Karte erstellen", "Сгенерировать карту", "生成地图", "生成地圖");
        Add("导入地图", "Import Map", "マップ読込", "맵 불러오기", "Karte importieren", "Импорт карты", "导入地图", "匯入地圖");
        Add("导出地图", "Export Map", "マップ書出", "맵 내보내기", "Karte exportieren", "Экспорт карты", "导出地图", "匯出地圖");
        Add("批量地形", "Batch Terrain", "地形一括", "지형 일괄", "Gelände-Stapel", "Массовое местность", "批量地形", "批量地形");
        Add("贴图上传", "Upload Texture", "テクスチャ", "텍스처 업로드", "Textur hochladen", "Загрузить текстуру", "贴图上传", "貼圖上傳");
        Add("贴图替换", "Replace Texture", "テクスチャ差替", "텍스처 교체", "Textur ersetzen", "Заменить текстуру", "贴图替换", "貼圖替換");
        Add("填充工具", "Fill Tool", "塗りつぶし", "채우기 도구", "Füllwerkzeug", "Заливка", "填充工具", "填充工具");
        Add("矩形选区", "Rect Select", "矩形選択", "사각 선택", "Rechteckauswahl", "Прямоуг. выбор", "矩形选区", "矩形選區");
        Add("不规则选区", "Irregular Select", "不規則選択", "불규칙 선택", "Freie Auswahl", "Произв. выбор", "不规则选区", "不規則選區");
        Add("撤销", "Undo", "元に戻す", "실행 취소", "Rückgängig", "Отменить", "撤销", "復原");
        Add("前进", "Redo", "やり直し", "다시 실행", "Wiederholen", "Повторить", "前进", "重做");
        Add("缩放", "Zoom", "ズーム", "확대/축소", "Zoom", "Масштаб", "缩放", "縮放");
        Add("方向", "Direction", "方向", "방향", "Richtung", "Направление", "方向", "方向");
        Add("宽度", "Width", "幅", "너비", "Breite", "Ширина", "宽度", "寬度");
        Add("高度", "Height", "高さ", "높이", "Höhe", "Высота", "高度", "高度");

        // ===== 生产菜单 =====
        Add("生产菜单", "Production", "生産", "생산", "Produktion", "Производство", "生产菜单", "生產選單");
        Add("本回合剩余生产", "Remaining", "残り生産", "남은 생산", "Verbleibend", "Осталось", "本回合剩余生产", "本回合剩餘生產");
        Add("生产后可立即移动", "Can Move After", "生産後移動可", "생산 후 이동", "Nach Prod. beweg.", "Движение после", "生产后可立即移动", "生產後可立即移動");
        Add("不可生产", "Cannot Produce", "生産不可", "생산 불가", "Nicht produzierbar", "Нельзя произвести", "不可生产", "不可生產");

        // ===== 战争迷雾 =====
        Add("战争迷雾", "Fog of War", "戦争霧", "전쟁의 안개", "Nebel des Krieges", "Туман войны", "战争迷雾", "戰爭迷霧");
        Add("启用战争迷雾", "Enable Fog", "霧を有効化", "안개 활성화", "Nebel aktivieren", "Включить туман", "启用战争迷雾", "啟用戰爭迷霧");
        Add("无迷雾", "No Fog", "霧なし", "안개 없음", "Kein Nebel", "Без тумана", "无迷雾", "無迷霧");
        Add("视野内", "In Vision", "視界内", "시야 내", "Im Sichtfeld", "В зоне видимости", "视野内", "視野內");
        Add("不紧邻不显示", "Adjacent Only", "隣接時表示", "인접 시 표시", "Nur angrenzend", "Только рядом", "不紧邻不显示", "不緊鄰不顯示");

        // ===== 胜利判定 =====
        Add("胜利判定", "Victory Cond.", "勝利条件", "승리 조건", "Siegbedingung", "Условие победы", "胜利判定", "勝利判定");
        Add("全灭胜利判定", "Annihilation", "全滅勝利", "전멸 승리", "Vernichtung", "Уничтожение", "全灭胜利判定", "全滅勝利判定");
        Add("兵器摧毁胜利判定", "Weapon Destr.", "兵器破壊勝利", "병기 파괴 승리", "Waffenzerstörung", "Уничт. оружия", "兵器摧毁胜利判定", "兵器摧毀勝利判定");
        Add("介入胜利判定", "Intervention", "介入勝利", "개입 승리", "Intervention", "Вмешательство", "介入胜利判定", "介入勝利判定");
        Add("其他势力战败", "Others Defeated", "他勢力敗北", "다른 세력 패배", "Andere besiegt", "Другие проиграли", "其他势力战败", "其他勢力戰敗");
        Add("有目标且全部摧毁", "All Targets Destroyed", "目標全破壊", "모든 목표 파괴", "Alle Ziele zerst.", "Все цели уничтожены", "有目标且全部摧毁", "有目標且全部摧毀");

        // ===== AI =====
        Add("AI控制", "AI Control", "AI制御", "AI 제어", "KI-Steuerung", "ИИ-управление", "AI控制", "AI控制");
        Add("AI自动操作", "AI Auto", "AI自動", "AI 자동", "KI-Auto", "ИИ-авто", "AI自动操作", "AI自動操作");
        Add("P1 AI 操控", "P1 AI", "P1 AI", "P1 AI", "P1 KI", "P1 ИИ", "P1 AI 操控", "P1 AI 操控");
        Add("P2 AI 操控", "P2 AI", "P2 AI", "P2 AI", "P2 KI", "P2 ИИ", "P2 AI 操控", "P2 AI 操控");

        // ===== 属性标签 =====
        Add("造价", "Cost", "コスト", "비용", "Kosten", "Стоимость", "造价", "造價");
        Add("主武器", "Primary", "主武器", "주무기", "Hauptwaffe", "Основное оружие", "主武器", "主武器");
        Add("副武器", "Secondary", "副武器", "부무기", "Nebenwaffe", "Вторичное оружие", "副武器", "副武器");
        Add("防御类型", "Defense Type", "防御タイプ", "방어 타입", "Verteidigungstyp", "Тип защиты", "防御类型", "防禦類型");
        Add("受地形加成", "Terrain Bonus", "地形ボーナス", "지형 보너스", "Gelände-Bonus", "Бонус местности", "受地形加成", "受地形加成");
        Add("不受地形加成", "No Bonus", "ボーナスなし", "보너스 없음", "Kein Bonus", "Без бонуса", "不受地形加成", "不受地形加成");
        Add("占领能力", "Capture", "占領能力", "점령 능력", "Eroberung", "Захват", "占领能力", "佔領能力");
        Add("可以占领", "Can Capture", "占領可", "점령 가능", "Eroberbar", "Можно захватить", "可以占领", "可以佔領");
        Add("不能占领", "Cannot Capture", "占領不可", "점령 불가", "Nicht erob.", "Нельзя захватить", "不能占领", "不能佔領");
        Add("收入", "Income", "収入", "수입", "Einkommen", "Доход", "收入", "收入");
        Add("无收入", "No Income", "収入なし", "수입 없음", "Kein Einkommen", "Нет дохода", "无收入", "無收入");
        Add("现有", "Current", "現在", "현재", "Aktuell", "Текущий", "现有", "現有");
        Add("上限", "Limit", "上限", "한도", "Limit", "Лимит", "上限", "上限");

        // ===== 冷却 & 弹药系统 =====
        Add("冷却", "Cooldown", "クールダウン", "쿨다운", "Abklingzeit", "Перезарядка", "冷却", "冷卻");
        Add("冷却系统", "Cooldown Sys", "クールダウン系", "쿨다운 시스템", "Abkling-System", "Система перезарядки", "冷却系统", "冷卻系統");
        Add("周期剩余次数", "Cycles Left", "残り回数", "남은 횟수", "Verbleibende", "Осталось циклов", "周期剩余次数", "週期剩餘次數");
        Add("当前周期进度", "Cycle Progress", "周期進行", "주기 진행", "Zyklus-Fortschritt", "Прогресс цикла", "当前周期进度", "當前週期進度");
        Add("还需等待", "Wait More", "あと待機", "더 기다리기", "Warte mehr", "Ждать ещё", "还需等待", "還需等待");
        Add("弹药耗尽", "Out of Ammo", "弾薬耗尽", "탄약 소진", "Munition leer", "Боеприпасы кончились", "弹药耗尽", "彈藥耗盡");
        Add("原因", "Reason", "理由", "이유", "Grund", "Причина", "原因", "原因");

        // ===== 格子属性 =====
        Add("防御加成", "Defense", "防御", "방어", "Verteidigung", "Защита", "防御加成", "防禦加成");
        Add("固定伤害", "Fixed Dmg", "固定ダメ", "고정 데미지", "Fester Schaden", "Фикс. урон", "固定伤害", "固定傷害");
        Add("固定攻击", "Fixed Atk", "固定攻撃", "고정 공격", "Fester Angriff", "Фикс. атака", "固定攻击", "固定攻擊");
        Add("血量变化", "HP Change", "HP変化", "HP 변화", "LP-Änderung", "Изм. ОЗ", "血量变化", "血量變化");
        Add("燃料变化", "Fuel Change", "燃料変化", "연료 변화", "Treibstoff-Änd.", "Изм. топлива", "燃料变化", "燃料變化");
        Add("弹药变化", "Ammo Change", "弾薬変化", "탄약 변화", "Munitions-Änd.", "Изм. боеприпасов", "弹药变化", "彈藥變化");
        Add("可归零", "Can Reach Zero", "ゼロ可", "제로 가능", "Kann Null", "Может быть 0", "可归零", "可歸零");
        Add("可超上限", "Can Exceed", "上限超過可", "상한 초과 가능", "Kann überschr.", "Может превысить", "可超上限", "可超上限");
        Add("可摧毁单位", "Can Destroy", "破壊可", "파괴 가능", "Kann zerstören", "Может уничтожить", "可摧毁单位", "可摧毀單位");

        // ===== 特殊能力 =====
        Add("自爆系统", "Self-Destruct", "自爆システム", "자폭 시스템", "Selbstzerstörung", "Самоуничтожение", "自爆系统", "自爆系統");
        Add("照明模块", "Illumination", "照明モジュール", "조명 모듈", "Beleuchtungsmodul", "Модуль освещения", "照明模块", "照明模組");
        Add("穿透系统", "Penetration", "貫通システム", "관통 시스템", "Durchdringung", "Проникновение", "穿透系统", "穿透系統");
        Add("搭载系统", "Transport", "輸送システム", "수송 시스템", "Transportsystem", "Трансп. система", "搭载系统", "搭載系統");
        Add("远程搭载", "Remote Load", "遠隔搭載", "원격 적재", "Fernbeladung", "Дистанц. загрузка", "远程搭载", "遠程搭載");
        Add("多格占据", "Multi-Tile", "複数マス", "다중 칸", "Mehrfeld", "Многоклеточный", "多格占据", "多格佔據");
        Add("反击系数", "Counter Mult", "反撃倍率", "반격 배율", "Gegenangr.-Faktor", "Множ. контратаки", "反击系数", "反擊系數");

        // ===== 统计面板 =====
        Add("单位统计", "Unit Stats", "ユニット統計", "유닛 통계", "Einheitenstat.", "Статистика юнитов", "单位统计", "單位統計");
        Add("兵器统计", "Weapon Stats", "兵器統計", "병기 통계", "Waffenstat.", "Статистика оружия", "兵器统计", "兵器統計");
        Add("设施统计", "Facility Stats", "施設統計", "시설 통계", "Einrichtungsstat.", "Статистика сооружений", "设施统计", "設施統計");
        Add("资金统计", "Fund Stats", "資金統計", "자금 통계", "Geldstat.", "Статистика средств", "资金统计", "資金統計");
        Add("价值统计", "Value Stats", "価値統計", "가치 통계", "Wertstat.", "Статистика ценности", "价值统计", "價值統計");
        Add("需摧毁数量", "To Destroy", "破壊対象", "파괴 대상", "Zu zerstören", "Уничтожить", "需摧毁数量", "需摧毀數量");

        // ===== 保存/加载槽位 =====
        Add("保存槽", "Save Slot", "セーブ枠", "저장 슬롯", "Speicherplatz", "Слот сохранения", "保存槽", "儲存槽");
        Add("单位保存槽", "Unit Slot", "ユニット枠", "유닛 슬롯", "Einheitenslot", "Слот юнитов", "单位保存槽", "單位儲存槽");
        Add("兵器保存槽", "Weapon Slot", "兵器枠", "병기 슬롯", "Waffenslot", "Слот оружия", "兵器保存槽", "兵器儲存槽");
        Add("地图保存槽", "Map Slot", "マップ枠", "맵 슬롯", "Kartenslot", "Слот карты", "地图保存槽", "地圖儲存槽");
        Add("地形保存槽", "Terrain Slot", "地形枠", "지형 슬롯", "Geländeslot", "Слот местности", "地形保存槽", "地形儲存槽");
        Add("设施保存槽", "Facility Slot", "施設枠", "시설 슬롯", "Einrichtungsslot", "Слот сооружений", "设施保存槽", "設施儲存槽");
        Add("槽位已有数据", "Slot Has Data", "枠にデータあり", "슬롯에 데이터 있음", "Slot hat Daten", "Слот занят", "槽位已有数据", "槽位已有數據");
        Add("收藏", "Favorite", "お気に入り", "즐겨찾기", "Favorit", "Избранное", "收藏", "收藏");
        Add("取消收藏", "Unfavorite", "お気に入解除", "즐겨찾기 해제", "Favorit entf.", "Убрать из избр.", "取消收藏", "取消收藏");
        Add("收藏已达上限", "Fav Limit", "お気に入上限", "즐겨찾기 한도", "Favoritenlimit", "Лимит избранного", "收藏已达上限", "收藏已達上限");

        // ===== 提示信息 =====
        Add("点击地图格子应用地形", "Click to Apply", "クリックで適用", "클릭으로 적용", "Klicken zum Anwenden", "Нажмите для применения", "点击地图格子应用地形", "點擊地圖格子套用地形");
        Add("点击地图格子放置", "Click to Place", "クリックで配置", "클릭으로 배치", "Klicken zum Platzieren", "Нажмите для размещения", "点击地图格子放置", "點擊地圖格子放置");
        Add("请先选择一个格子", "Select a Tile", "マスを選択", "칸을 선택하세요", "Feld auswählen", "Выберите клетку", "请先选择一个格子", "請先選擇一個格子");
        Add("请先选择一个单位", "Select Unit", "ユニット選択", "유닛을 선택하세요", "Einheit auswählen", "Выберите юнита", "请先选择一个单位", "請先選擇一個單位");
        Add("请先选择一个兵器", "Select Weapon", "兵器選択", "병기를 선택하세요", "Waffe auswählen", "Выберите оружие", "请先选择一个兵器", "請先選擇一個兵器");
        Add("请先选择一个设施", "Select Facility", "施設選択", "시설을 선택하세요", "Einrichtung wählen", "Выберите сооружение", "请先选择一个设施", "請先選擇一個設施");
        Add("请先选择贴图", "Select Texture", "テクスチャ選択", "텍스처를 선택하세요", "Textur auswählen", "Выберите текстуру", "请先选择贴图", "請先選擇貼圖");
        Add("超出地图范围", "Out of Bounds", "範囲外", "범위 초과", "Außerhalb", "Вне границ", "超出地图范围", "超出地圖範圍");
        Add("该格子已有兵器", "Has Weapon", "兵器あり", "병기 있음", "Hat Waffe", "Оружие есть", "该格子已有兵器", "該格子已有兵器");
        Add("该格子已有设施", "Has Facility", "施設あり", "시설 있음", "Hat Einrichtung", "Сооружение есть", "该格子已有设施", "該格子已有設施");
        Add("该区域已有兵器占据", "Area Occupied", "エリア占有", "구역 점유됨", "Bereich belegt", "Зона занята", "该区域已有兵器占据", "該區域已有兵器佔據");

        // ===== 文件操作 =====
        Add("选择文件", "Select File", "ファイル選択", "파일 선택", "Datei auswählen", "Выбрать файл", "选择文件", "選擇檔案");
        Add("选择地图文件", "Select Map", "マップ選択", "맵 파일 선택", "Karte auswählen", "Выбрать карту", "选择地图文件", "選擇地圖檔案");
        Add("导入成功", "Import Success", "読込成功", "불러오기 성공", "Import erfolgr.", "Импорт успешен", "导入成功", "匯入成功");
        Add("导出成功", "Export Success", "書出成功", "내보내기 성공", "Export erfolgr.", "Экспорт успешен", "导出成功", "匯出成功");
        Add("加载失败", "Load Failed", "読込失敗", "불러오기 실패", "Laden fehlgeschl.", "Загрузка не удалась", "加载失败", "載入失敗");
        Add("文件格式不支持", "Unsupported", "未対応形式", "지원하지 않음", "Nicht unterstützt", "Не поддерживается", "文件格式不支持", "檔案格式不支援");
        Add("剪贴板为空", "Clipboard Empty", "クリップボ空", "클립보드 비어있음", "Zwischenablage leer", "Буфер обмена пуст", "剪贴板为空", "剪貼簿為空");
        Add("图片加载失败", "Image Error", "画像エラー", "이미지 오류", "Bildfehler", "Ошибка изображения", "图片加载失败", "圖片載入失敗");
        Add("贴图尺寸过小", "Texture Small", "テクスチャ小", "텍스처 작음", "Textur zu klein", "Текстура мала", "贴图尺寸过小", "貼圖尺寸過小");
        Add("贴图不是正方形", "Not Square", "非正方形", "정사각형 아님", "Nicht quadrat.", "Не квадратная", "贴图不是正方形", "貼圖不是正方形");
        Add("贴图上传成功", "Upload OK", "アップロード成功", "업로드 성공", "Hochladen OK", "Загрузка ОК", "贴图上传成功", "貼圖上傳成功");
        Add("贴图已替换", "Replaced", "差替完了", "교체 완료", "Ersetzt", "Заменено", "贴图已替换", "貼圖已替換");

        // ===== 确认对话框 =====
        Add("是否继续", "Continue?", "続行?", "계속?", "Fortfahren?", "Продолжить?", "是否继续", "是否繼續");
        Add("确定要继续吗", "Are you sure?", "本当に?", "계속하시겠습니까", "Sicher?", "Вы уверены?", "确定要继续吗", "確定要繼續嗎");
        Add("将生成新地图", "New Map", "新マップ生成", "새 맵 생성", "Neue Karte", "Новая карта", "将生成新地图", "將生成新地圖");
        Add("当前所有单位将被清空", "Clear All", "全て消去", "모두 삭제", "Alles löschen", "Очистить всё", "当前所有单位将被清空", "當前所有單位將被清空");
        Add("覆盖全图并清空实体", "Overwrite All", "全上書消去", "전체 덮어쓰기", "Alles überschr.", "Перезаписать всё", "覆盖全图并清空实体", "覆蓋全圖並清空實體");

        // ===== 其他常用 =====
        Add("配置", "Config", "設定", "설정", "Konfig", "Конфиг", "配置", "配置");
        Add("全局配置", "Global Config", "全体設定", "전역 설정", "Globale Konfig", "Глоб. конфиг", "全局配置", "全域配置");
        Add("自定义配置", "Custom Config", "カスタム設定", "커스텀 설정", "Benutzerkonfig", "Польз. конфиг", "自定义配置", "自訂配置");
        Add("显示名称", "Display Name", "表示名", "표시 이름", "Anzeigename", "Отображ. имя", "显示名称", "顯示名稱");
        Add("自定义名称", "Custom Name", "カスタム名", "커스텀 이름", "Benutzername", "Польз. имя", "自定义名称", "自訂名稱");
        Add("输入新名称", "Enter Name", "新名称入力", "새 이름 입력", "Name eingeben", "Введите имя", "输入新名称", "輸入新名稱");
        Add("ID和名称不能为空", "ID/Name Required", "ID/名称必須", "ID/이름 필수", "ID/Name benötigt", "ID/Имя обязательно", "ID和名称不能为空", "ID和名稱不能為空");
        Add("重命名成功", "Renamed", "改名成功", "이름 변경 성공", "Umbenannt", "Переименовано", "重命名成功", "重新命名成功");
        Add("名称过长", "Name Too Long", "名称長過", "이름 너무 김", "Name zu lang", "Имя слишком длинное", "名称过长", "名稱過長");
        Add("历史", "History", "履歴", "기록", "Verlauf", "История", "历史", "歷史");
        Add("页", "Page", "ページ", "페이지", "Seite", "Страница", "页", "頁");
        Add("个", "", "", "", "", "", "个", "個");
        Add("格", "Tiles", "マス", "칸", "Felder", "Клеток", "格", "格");

        // ===== 开关状态 =====
        Add("启用", "Enabled", "有効", "활성", "Aktiviert", "Включено", "启用", "啟用");
        Add("禁用", "Disabled", "無効", "비활성", "Deaktiviert", "Отключено", "禁用", "禁用");
        Add("开启", "On", "ON", "켜기", "An", "Вкл", "开启", "開啟");
        Add("关闭", "Off", "OFF", "끄기", "Aus", "Выкл", "关闭", "關閉");
        Add("已启用", "Enabled", "有効", "활성화됨", "Aktiviert", "Включено", "已启用", "已啟用");
        Add("未开启", "Not On", "未開始", "켜지지 않음", "Nicht an", "Не включено", "未开启", "未開啟");

        // ===== 玩家标识 =====
        Add("P1", "P1", "P1", "P1", "P1", "P1", "P1", "P1");
        Add("P2", "P2", "P2", "P2", "P2", "P2", "P2", "P2");
        Add("Player1", "Player1", "プレイヤー1", "플레이어1", "Spieler1", "Игрок1", "Player1", "Player1");
        Add("Player2", "Player2", "プレイヤー2", "플레이어2", "Spieler2", "Игрок2", "Player2", "Player2");
        Add("中立", "Neutral", "中立", "중립", "Neutral", "Нейтрал", "中立", "中立");
        Add("双方共用", "Shared", "共有", "공유", "Geteilt", "Общий", "双方共用", "雙方共用");
        Add("仅己方", "Ally Only", "自軍のみ", "아군만", "Nur Verbündete", "Только свои", "仅己方", "僅己方");
        Add("仅敌方", "Enemy Only", "敵のみ", "적만", "Nur Feinde", "Только враги", "仅敌方", "僅敵方");
        Add("所有单位", "All Units", "全ユニット", "모든 유닛", "Alle Einheiten", "Все юниты", "所有单位", "所有單位");

        // ===== 编辑器助手 =====
        Add("编辑器助手", "Editor Helper", "エディタ助手", "에디터 도우미", "Editor-Helfer", "Помощник редактора", "编辑器助手", "編輯器助手");
        Add("生成单位", "Spawn Unit", "ユニット生成", "유닛 생성", "Einheit erzeugen", "Создать юнита", "生成单位", "生成單位");
        Add("生成兵器", "Spawn Weapon", "兵器生成", "병기 생성", "Waffe erzeugen", "Создать оружие", "生成兵器", "生成兵器");
        Add("生成设施", "Spawn Facility", "施設生成", "시설 생성", "Einrichtung erzeug.", "Создать сооружение", "生成设施", "生成設施");
        Add("生成城市", "Spawn City", "都市生成", "도시 생성", "Stadt erzeugen", "Создать город", "生成城市", "生成城市");
        Add("销毁模式", "Destroy Mode", "破壊モード", "파괴 모드", "Zerstörungsmodus", "Режим уничтожения", "销毁模式", "銷毀模式");
        Add("确认销毁", "Confirm Destroy", "破壊確認", "파괴 확인", "Zerstörung best.", "Подтвердить уничтожение", "确认销毁", "確認銷毀");

        // ===== 移动方式标签 =====
        Add("快速添加", "Quick Add", "クイック追加", "빠른 추가", "Schnell hinzuf.", "Быстрое добавл.", "快速添加", "快速新增");
        Add("暂无自定义地形", "No Custom Terr.", "カスタム地形なし", "커스텀 지형 없음", "Kein Gelände", "Нет местности", "暂无自定义地形", "暫無自訂地形");
        Add("暂无自定义移动方式", "No Custom Move", "カスタム移動なし", "커스텀 이동 없음", "Keine Bewegung", "Нет передвижения", "暂无自定义移动方式", "暫無自訂移動方式");
        Add("暂无上传的贴图", "No Textures", "テクスチャなし", "텍스처 없음", "Keine Texturen", "Нет текстур", "暂无上传的贴图", "暫無上傳的貼圖");
        Add("新建自定义地形", "New Terrain", "新地形", "새 지형", "Neues Gelände", "Новая местность", "新建自定义地形", "新增自訂地形");
        Add("新建自定义移动方式", "New Move Type", "新移動方式", "새 이동 방식", "Neue Bewegung", "Новое передвижение", "新建自定义移动方式", "新增自訂移動方式");

        // ===== 贴图相关 =====
        Add("贴图文件", "Texture", "テクスチャ", "텍스처", "Textur", "Текстура", "贴图", "貼圖");
        Add("选择贴图", "Select Tex", "テクスチャ選択", "텍스처 선택", "Textur wählen", "Выбрать текстуру", "选择贴图", "選擇貼圖");
        Add("选择贴图文件", "Pick Texture", "テクスチャ指定", "텍스처 파일", "Textur wählen", "Выбрать файл текст.", "选择贴图文件", "選擇貼圖檔案");
        Add("上传新贴图", "Upload New", "新規アップロード", "새 업로드", "Neu hochladen", "Загрузить новую", "上传新贴图", "上傳新貼圖");
        Add("已上传贴图", "Uploaded", "アップロード済", "업로드됨", "Hochgeladen", "Загружено", "已上传贴图", "已上傳貼圖");
        Add("应用到现有对象", "Apply Existing", "既存に適用", "기존에 적용", "Auf Bestehende", "Применить к сущ.", "应用到现有对象", "套用到現有物件");

        // ===== 批量操作 =====
        Add("区域填充", "Area Fill", "エリア塗り", "구역 채우기", "Bereich füllen", "Заливка области", "区域填充", "區域填充");
        Add("地形填充", "Terrain Fill", "地形塗り", "지형 채우기", "Gelände füllen", "Заливка местности", "地形填充", "地形填充");
        Add("全图替换", "Replace All", "全置換", "전체 교체", "Alles ersetzen", "Заменить всё", "全图替换", "全圖替換");
        Add("当前", "Current", "現在", "현재", "Aktuell", "Текущий", "当前", "當前");
        Add("选择地形", "Select Terrain", "地形選択", "지형 선택", "Gelände wählen", "Выбрать местность", "选择地形", "選擇地形");
        Add("选择目标新地形", "Target Terrain", "対象地形", "대상 지형", "Zielgelände", "Целевая местность", "选择目标新地形", "選擇目標新地形");
        Add("选择覆盖地形", "Overwrite Terr.", "上書地形", "덮어쓸 지형", "Überschreiben", "Перезаписать", "选择覆盖地形", "選擇覆蓋地形");
        Add("选择填充地形", "Fill Terrain", "塗り地形", "채울 지형", "Füllgelände", "Заливка местности", "选择填充地形", "選擇填充地形");

        // ===== 对称 & 选区 =====
        Add("对称", "Symmetry", "対称", "대칭", "Symmetrie", "Симметрия", "对称", "對稱");
        Add("对称放置已关闭", "Symmetry Off", "対称OFF", "대칭 꺼짐", "Symmetrie aus", "Симметрия выкл", "对称放置已关闭", "對稱放置已關閉");
        Add("横轴对称", "H-Symmetry", "横対称", "가로 대칭", "Horiz. Symmetrie", "Гориз. симметрия", "横轴对称", "橫軸對稱");
        Add("竖轴对称", "V-Symmetry", "縦対称", "세로 대칭", "Vert. Symmetrie", "Верт. симметрия", "竖轴对称", "豎軸對稱");
        Add("粘贴选区", "Paste Select", "選択貼付", "선택 붙여넣기", "Auswahl einfügen", "Вставить выбор", "粘贴选区", "貼上選區");
        Add("没有可粘贴的选区", "Nothing to Paste", "貼付物なし", "붙여넣을 것 없음", "Nichts einfügbar", "Нечего вставлять", "没有可粘贴的选区", "沒有可貼上的選區");
        Add("已粘贴", "Pasted", "貼付済", "붙여넣기 완료", "Eingefügt", "Вставлено", "已粘贴", "已貼上");
        Add("不规则选区", "Irregular", "不規則", "불규칙", "Unregelmäßig", "Произвольный", "不规则选区", "不規則選區");
        Add("矩形选区", "Rectangle", "矩形", "사각형", "Rechteck", "Прямоугольник", "矩形选区", "矩形選區");

        // ===== 属性编辑器标签 =====
        Add("基础属性", "Base Stats", "基本性能", "기본 속성", "Basiswerte", "Базовые хар-ки", "基础属性", "基礎屬性");
        Add("基础伤害配置", "Dmg Config", "ダメージ設定", "데미지 설정", "Schadenskonfig", "Конфиг урона", "基础伤害配置", "基礎傷害配置");
        Add("格子自定义伤害", "Tile Dmg", "マスダメージ", "칸 커스텀 데미지", "Feldschaden", "Урон клетки", "格子自定义伤害", "格子自訂傷害");
        Add("格子自定义效果", "Tile Effect", "マス効果", "칸 효과", "Feldeffekt", "Эффект клетки", "格子自定义效果", "格子自訂效果");
        Add("格子视野系统", "Tile Vision", "マス視界", "칸 시야", "Feldsicht", "Обзор клетки", "格子视野系统", "格子視野系統");
        Add("格子弹药燃料系统", "Tile Ammo/Fuel", "マス弾薬燃料", "칸 탄약/연료", "Feld-Munition", "Боепр./топл. клетки", "格子弹药燃料系统", "格子彈藥燃料系統");
        Add("战争迷雾视野", "Fog Vision", "霧視界", "안개 시야", "Nebelsicht", "Обзор тумана", "战争迷雾视野", "戰爭迷霧視野");
        Add("建筑视野", "Building Vision", "建物視界", "건물 시야", "Gebäudesicht", "Обзор зданий", "建筑视野", "建築視野");
        Add("视野配置表", "Vision Config", "視界設定", "시야 설정", "Sichtkonfig", "Конфиг обзора", "视野配置表", "視野配置表");
        Add("单位基础视野配置", "Unit Vision", "ユニット視界", "유닛 시야", "Einheitensicht", "Обзор юнита", "单位基础视野配置", "單位基礎視野配置");
        Add("兵器基础视野配置", "Wpn Vision", "兵器視界", "병기 시야", "Waffensicht", "Обзор оружия", "兵器基础视野配置", "兵器基礎視野配置");
        Add("全局默认地形视野加成", "Global Vision", "全体地形視界", "전역 지형 시야", "Globale Sicht", "Глоб. обзор", "全局默认地形视野加成", "全域預設地形視野加成");
        Add("单位类型地形加成矩阵", "Unit Matrix", "ユニット行列", "유닛 행렬", "Einheitenmatrix", "Матрица юнитов", "单位类型地形加成矩阵", "單位類型地形加成矩陣");
        Add("兵器类型地形加成矩阵", "Wpn Matrix", "兵器行列", "병기 행렬", "Waffenmatrix", "Матрица оружия", "兵器类型地形加成矩阵", "兵器類型地形加成矩陣");

        // ===== 武器效果 =====
        Add("武器效果", "Weapon Effect", "武器効果", "무기 효과", "Waffeneffekt", "Эффект оружия", "武器效果", "武器效果");
        Add("反步兵", "Anti-Inf", "対歩兵", "대보병", "Anti-Inf.", "Против пехоты", "反步兵", "反步兵");
        Add("反装甲", "Anti-Armor", "対装甲", "대장갑", "Anti-Panzer", "Против брони", "反装甲", "反裝甲");
        Add("无法攻击", "Cannot Attack", "攻撃不可", "공격 불가", "Kann nicht angreifen", "Нельзя атаковать", "无法攻击", "無法攻擊");
        Add("无法反击", "Cannot Counter", "反撃不可", "반격 불가", "Kann nicht kontern", "Нельзя контратаковать", "无法反击", "無法反擊");

        // ===== 占领 & 生产 =====
        Add("占领目标", "Capture Target", "占領目標", "점령 목표", "Eroberungsziel", "Цель захвата", "占领目标", "佔領目標");
        Add("占领消耗", "Capture Cost", "占領コスト", "점령 비용", "Eroberungskosten", "Стоимость захвата", "占领消耗", "佔領消耗");
        Add("占领所需", "Capture Need", "占領必要", "점령 필요", "Eroberung nötig", "Нужно для захвата", "占领所需", "佔領所需");
        Add("当前占领进度", "Capture Progr.", "占領進行度", "점령 진행", "Eroberungsforts.", "Прогресс захвата", "当前占领进度", "當前佔領進度");
        Add("当前占领中单位数", "Capturing Units", "占領中単位数", "점령 중 유닛", "Erobernde Einheiten", "Захватывающие юниты", "当前占领中单位数", "當前佔領中單位數");
        Add("归属", "Owner", "所属", "소속", "Besitzer", "Владелец", "归属", "歸屬");
        Add("无归属", "No Owner", "無所属", "소속 없음", "Kein Besitzer", "Нет владельца", "无归属", "無歸屬");

        // ===== 激光/死光炮配置 =====
        Add("激光射程配置", "Laser Range", "レーザー射程", "레이저 사거리", "Laser-Reichweite", "Дальность лазера", "激光射程配置", "雷射程配置");
        Add("激光角度系统", "Laser Angle", "レーザー角度", "레이저 각도", "Laser-Winkel", "Угол лазера", "激光角度系统", "雷射角度系統");
        Add("死光炮配置", "DeathRay Config", "死光砲設定", "사광포 설정", "Todesstrahl-Konfig", "Конфиг луча смерти", "死光炮配置", "死光炮配置");
        Add("黑炮专用配置", "Cannon Config", "黒砲設定", "흑포 설정", "Kanone-Konfig", "Конфиг пушки", "黑炮专用配置", "黑炮專用配置");
        Add("大型黑炮专属", "Large Cannon", "大型黒砲", "대형 흑포", "Große Kanone", "Большая пушка", "大型黑炮专属", "大型黑炮專屬");
        Add("角度配置", "Angle Config", "角度設定", "각도 설정", "Winkelkonfig", "Конфиг угла", "角度配置", "角度配置");
        Add("添加新角度", "Add Angle", "角度追加", "각도 추가", "Winkel hinzuf.", "Добавить угол", "添加新角度", "新增角度");
        Add("勾选启用该角度发射", "Check to Enable", "チェックで有効", "체크로 활성", "Anhaken zum Aktiv.", "Отметить для вкл.", "勾选启用该角度发射", "勾選啟用該角度發射");
        Add("应用角度配置到激光炮", "Apply to Laser", "レーザーに適用", "레이저에 적용", "Auf Laser anwenden", "Применить к лазеру", "应用角度配置到激光炮", "套用角度配置到雷射炮");
        Add("炮口", "Muzzle", "砲口", "포구", "Mündung", "Дуло", "炮口", "砲口");
        Add("弱点", "Weak Point", "弱点", "약점", "Schwachpunkt", "Слабое место", "弱点", "弱點");
        Add("炮口位置", "Muzzle Pos", "砲口位置", "포구 위치", "Mündungspos.", "Поз. дула", "炮口位置", "砲口位置");
        Add("弱点位置", "Weak Pos", "弱点位置", "약점 위치", "Schwachstelle", "Поз. слаб. места", "弱点位置", "弱點位置");
        Add("可旋转", "Can Rotate", "回転可", "회전 가능", "Drehbar", "Может вращаться", "可旋转", "可旋轉");
        Add("允许旋转", "Allow Rotate", "回転許可", "회전 허용", "Drehen erlaubt", "Разрешить вращение", "允许旋转", "允許旋轉");
        Add("旋转与交互", "Rotate & Interact", "回転と操作", "회전 및 상호작용", "Drehen & Interakt.", "Вращение и взаимод.", "旋转与交互", "旋轉與互動");

        // ===== 伤害 & 攻击 =====
        Add("伤害", "Damage", "ダメージ", "데미지", "Schaden", "Урон", "伤害", "傷害");
        Add("固定伤害回合", "Fixed Dmg/Turn", "固定ダメ/ターン", "고정 데미지/턴", "Fester Schaden/Runde", "Фикс. урон/ход", "固定伤害回合", "固定傷害回合");
        Add("固定攻击回合", "Fixed Atk/Turn", "固定攻撃/ターン", "고정 공격/턴", "Fester Angriff/Runde", "Фикс. атака/ход", "固定攻击回合", "固定攻擊回合");
        Add("接触面积衰减系数", "Contact Decay", "接触減衰", "접촉 감쇠", "Kontakt-Abfall", "Коэф. затухания", "接触面积衰减系数", "接觸面積衰減係數");
        Add("已叠加接触面积衰减", "Contact Applied", "接触減衰適用", "접촉 감쇠 적용", "Kontakt angew.", "Контакт применён", "已叠加接触面积衰减", "已疊加接觸面積衰減");
        Add("穿过中心", "Center Hit", "中心貫通", "중심 관통", "Zentrum-Treffer", "Прохождение центра", "穿过中心", "穿過中心");
        Add("擦边角", "Edge Hit", "端擦れ", "모서리 스치기", "Kanten-Treffer", "Задевание края", "擦边角", "擦邊角");
        Add("摧毁设施", "Destroy Facil.", "施設破壊", "시설 파괴", "Einrichtung zerst.", "Уничтожить сооруж.", "摧毁设施", "摧毀設施");
        Add("摧毁自身", "Self Destroy", "自壊", "자기 파괴", "Selbstzerstörung", "Самоуничтожение", "摧毁自身", "摧毀自身");
        Add("影响兵器", "Affect Weapons", "兵器影響", "병기 영향", "Waffen beeinfl.", "Влиять на оружие", "影响兵器", "影響兵器");

        // ===== 照明弹 =====
        Add("照明弹", "Flare", "照明弾", "조명탄", "Leuchtrakete", "Сигнальная ракета", "照明弹", "照明彈");
        Add("照明覆盖", "Illum. Range", "照明範囲", "조명 범위", "Beleuchtungsreichw.", "Дальность освещ.", "照明覆盖", "照明覆蓋");
        Add("持续回合", "Duration", "持続ターン", "지속 턴", "Dauer", "Длительность", "持续回合", "持續回合");
        Add("移动后可照明", "Move+Illum.", "移動後照明可", "이동 후 조명", "Bewegen+Beleuchten", "Движение+свет", "移动后可照明", "移動後可照明");

        // ===== 爆炸/自爆 =====
        Add("爆炸范围", "Explode Range", "爆発範囲", "폭발 범위", "Explosionsreichw.", "Радиус взрыва", "爆炸范围", "爆炸範圍");
        Add("自爆弹模块", "Bomb Module", "爆弾モジュール", "폭탄 모듈", "Bombenmodul", "Модуль бомбы", "自爆弹模块", "自爆彈模組");
        Add("回血可超上限", "Heal Can Exceed", "回復上限超過可", "치료 상한 초과", "Heilung überschr.", "Лечение превысить", "回血可超上限", "回血可超上限");

        // ===== 远程搭载 =====
        Add("远程搭载距离", "Remote Range", "遠隔搭載距離", "원격 적재 거리", "Fernbelad.-Reichw.", "Дист. загрузки", "远程搭载距离", "遠程搭載距離");
        Add("选择要搭载的运输单位", "Select Transport", "輸送単位選択", "수송 유닛 선택", "Transport wählen", "Выбрать транспорт", "选择要搭载的运输单位", "選擇要搭載的運輸單位");
        Add("已搭载单位", "Loaded Units", "搭載済単位", "적재된 유닛", "Geladene Einheiten", "Загруженные юниты", "已搭载单位", "已搭載單位");
        Add("当前装载", "Current Load", "現在搭載", "현재 적재", "Aktuelle Ladung", "Текущая загрузка", "当前装载", "當前裝載");
        Add("装载容量", "Capacity", "搭載容量", "적재 용량", "Kapazität", "Вместимость", "装载容量", "裝載容量");

        // ===== 弹药燃料详情 =====
        Add("有弹药", "Has Ammo", "弾薬あり", "탄약 있음", "Hat Munition", "Есть боеприпасы", "有弹药", "有彈藥");
        Add("无限弹药", "Infinite Ammo", "弾薬無限", "탄약 무한", "Unend. Munition", "Беск. боеприпасы", "无限弹药", "無限彈藥");
        Add("弹药无限", "Ammo ∞", "弾薬∞", "탄약 ∞", "Munition ∞", "Боепр. ∞", "弹药无限", "彈藥無限");
        Add("补充", "Resupply", "補充", "보충", "Auffüllen", "Пополнение", "补充", "補充");
        Add("消耗", "Consume", "消費", "소모", "Verbrauchen", "Потребление", "消耗", "消耗");
        Add("日耗", "Daily Use", "日次消費", "일일 소모", "Tägl. Verbrauch", "Ежедн. расход", "日耗", "日耗");
        Add("阈值", "Threshold", "閾値", "임계값", "Schwelle", "Порог", "阈值", "閾值");

        // ===== 生产菜单标题 =====
        Add("生产", "Produce", "生産", "생산", "Produzieren", "Производство", "生产", "生產");

        // ===== 信息面板 =====
        Add("单位名称", "Unit Name", "ユニット名", "유닛 이름", "Einheitsname", "Имя юнита", "单位名称", "單位名稱");
        Add("最大生命值", "Max HP", "最大HP", "최대 HP", "Max LP", "Макс. ОЗ", "最大生命值", "最大生命值");
        Add("实时主武器攻击力", "Real Atk", "実時攻撃力", "실시 공격력", "Echt-Angriff", "Реал. атака", "实时主武器攻击力", "即時主武器攻擊力");
        Add("实时副武器攻击力", "Real Sec.Atk", "実時副攻撃", "실시 부공격", "Echt-Nebenangr.", "Реал. вт. атака", "实时副武器攻击力", "即時副武器攻擊力");
        Add("坐标", "Coords", "座標", "좌표", "Koordinaten", "Координаты", "坐标", "座標");
        Add("地形", "Terrain", "地形", "지형", "Gelände", "Местность", "地形", "地形");
        Add("当前地形", "Current Terr.", "現在地形", "현재 지형", "Akt. Gelände", "Тек. местность", "当前地形", "當前地形");

        // ===== 回合相关 =====
        Add("切换回合", "End Turn", "ターン終了", "턴 종료", "Zug beenden", "Закончить ход", "切换回合", "切換回合");
        Add("自动切换回合", "Auto End Turn", "自動ターン終了", "자동 턴 종료", "Auto-Zugende", "Автоокончание хода", "自动切换回合", "自動切換回合");
        Add("再来一局", "Rematch", "再戦", "재대결", "Revanche", "Реванш", "再来一局", "再來一局");

        // ===== 特殊状态 =====
        Add("已行动", "Acted", "行動済", "행동 완료", "Gehandelt", "Действовал", "已行动", "已行動");
        Add("未行动", "Not Acted", "未行動", "미행동", "Nicht gehandelt", "Не действовал", "未行动", "未行動");
        Add("可反击", "Can Counter", "反撃可", "반격 가능", "Kann kontern", "Может контратаковать", "可反击", "可反擊");
        Add("已装载", "Loaded", "搭載済", "적재됨", "Geladen", "Загружен", "已装载", "已裝載");
        Add("空载", "Empty", "空", "비어있음", "Leer", "Пустой", "空载", "空載");

        // ===== 错误/警告 =====
        Add("错误", "Error", "エラー", "오류", "Fehler", "Ошибка", "错误", "錯誤");
        Add("警告", "Warning", "警告", "경고", "Warnung", "Предупреждение", "警告", "警告");
        Add("注意", "Notice", "注意", "주의", "Hinweis", "Уведомление", "注意", "注意");
        Add("成功", "Success", "成功", "성공", "Erfolg", "Успех", "成功", "成功");
        Add("失败", "Failed", "失敗", "실패", "Fehlgeschl.", "Неудача", "失败", "失敗");

        // ===== 预览 & 确认 =====
        Add("预览", "Preview", "プレビュー", "미리보기", "Vorschau", "Предпросмотр", "预览", "預覽");
        Add("确认覆盖", "Confirm Overwrite", "上書確認", "덮어쓰기 확인", "Überschreiben best.", "Подтвердить перезапись", "确认覆盖", "確認覆蓋");
        Add("确认移动", "Confirm Move", "移動確認", "이동 확인", "Bewegung best.", "Подтвердить движение", "确认移动", "確認移動");
        Add("确认创建", "Confirm Create", "作成確認", "생성 확인", "Erstellen best.", "Подтвердить создание", "确认创建", "確認建立");
        Add("确认保存", "Confirm Save", "保存確認", "저장 확인", "Speichern best.", "Подтвердить сохранение", "确认保存", "確認儲存");

        // ===== 统计单位标签 =====
        Add("个单位", "Units", "ユニット", "유닛", "Einheiten", "Юниты", "个单位", "個單位");
        Add("个兵器", "Weapons", "兵器", "병기", "Waffen", "Оружие", "个兵器", "個兵器");
        Add("个设施", "Facilities", "施設", "시설", "Einrichtungen", "Сооружения", "个设施", "個設施");
        Add("座城市", "Cities", "都市", "도시", "Städte", "Города", "座城市", "座城市");

        // ===== 模式选择 =====
        Add("选择模式开始", "Select Mode", "モード選択", "모드 선택", "Modus wählen", "Выбрать режим", "选择模式开始", "選擇模式開始");
        Add("选择生成模式", "Spawn Mode", "生成モード", "생성 모드", "Erzeugungsmodus", "Режим создания", "选择生成模式", "選擇生成模式");
        Add("选择编辑目标", "Edit Target", "編集対象", "편집 대상", "Ziel bearb.", "Цель редактирования", "选择编辑目标", "選擇編輯目標");
        Add("目标选择模式", "Target Mode", "対象選択", "대상 선택", "Zielmodus", "Режим цели", "目标选择模式", "目標選擇模式");

        // ===== 额外保存系统 =====
        Add("保存修改", "Save Changes", "変更保存", "변경 저장", "Änderungen speich.", "Сохранить изменения", "保存修改", "儲存修改");
        Add("覆盖保存", "Overwrite", "上書保存", "덮어쓰기 저장", "Überschreiben", "Перезаписать", "覆盖保存", "覆蓋儲存");
        Add("保存加载", "Save/Load", "保存読込", "저장/불러오기", "Speichern/Laden", "Сохр./Загрузка", "保存加载", "儲存載入");
        Add("从剪贴板导入", "From Clipboard", "クリップボから", "클립보드에서", "Aus Zwischenablage", "Из буфера обмена", "从剪贴板导入", "從剪貼簿匯入");
        Add("导出配置", "Export Config", "設定書出", "설정 내보내기", "Konfig exportieren", "Экспорт конфига", "导出配置", "匯出配置");
        Add("导入配置", "Import Config", "設定読込", "설정 불러오기", "Konfig importieren", "Импорт конфига", "导入配置", "匯入配置");
        Add("极简导出", "Simple Export", "簡易書出", "간단 내보내기", "Einfacher Export", "Простой экспорт", "极简导出", "極簡匯出");
        Add("极简导入", "Simple Import", "簡易読込", "간단 불러오기", "Einfacher Import", "Простой импорт", "极简导入", "極簡匯入");
        Add("请求相册访问权限", "Request Gallery", "ギャラリー許可", "갤러리 권한", "Galerie-Zugriff", "Доступ к галерее", "请求相册访问权限", "請求相簿存取權限");
        Add("当前权限状态", "Permission", "権限状態", "권한 상태", "Berechtigung", "Разрешение", "当前权限状态", "當前權限狀態");
        Add("权限请求已发送", "Perm. Sent", "権限要求送信", "권한 요청 전송", "Berecht. gesendet", "Запрос разрешения", "权限请求已发送", "權限請求已發送");
        Add("未授权", "Unauthorized", "未認可", "미인증", "Nicht autorisiert", "Не авторизовано", "未授权", "未授權");

        // ===== 复杂提示信息（长文本） =====
        Add("将生成新地图当前所有单位兵器设施将被清空",
            "This will generate a new map and clear all units, weapons, and facilities.",
            "新マップを生成し、全ユニット・兵器・施設を消去します。",
            "새 맵을 생성하고 모든 유닛, 병기, 시설을 삭제합니다.",
            "Dies erstellt eine neue Karte und löscht alle Einheiten, Waffen und Einrichtungen.",
            "Будет создана новая карта, все юниты, оружие и сооружения будут удалены.",
            "将生成新地图，当前所有单位、兵器、设施将被清空！",
            "將生成新地圖，當前所有單位、兵器、設施將被清空！");

        Add("导入将清空当前所有单位兵器和设施并用文件内容重建地图",
            "Import will clear all units/weapons/facilities and rebuild the map from file.",
            "読込により全ユニット・兵器・施設を消去し、ファイルからマップを再構築します。",
            "불러오기 시 모든 유닛, 병기, 시설을 삭제하고 파일로부터 맵을 재구성합니다.",
            "Import löscht alle Einheiten/Waffen/Einrichtungen und baut Karte aus Datei auf.",
            "Импорт очистит всех юнитов/оружие/сооружения и перестроит карту из файла.",
            "导入将清空当前所有单位、兵器和设施，并用文件内容重建地图。",
            "匯入將清空當前所有單位、兵器和設施，並用檔案內容重建地圖。");

        Add("覆盖全图并清空实体确定要继续吗",
            "Overwrite entire map and clear all entities. Are you sure?",
            "全マップを上書きし、全エンティティを消去します。本当に続行しますか？",
            "전체 맵을 덮어쓰고 모든 엔티티를 삭제합니다. 계속하시겠습니까?",
            "Gesamte Karte überschreiben und alle Entitäten löschen. Sicher?",
            "Перезаписать всю карту и очистить все объекты. Вы уверены?",
            "覆盖全图并清空实体，确定要继续吗？",
            "覆蓋全圖並清空實體，確定要繼續嗎？");

        Add("支持awsaw2awmawd格式未知地形将保留为GROUND不支持的单位设施兵器将自动跳过",
            "Supports .aws/.aw2/.awm/.awd. Unknown terrain -> GROUND. Unsupported units/facilities/weapons skipped.",
            ".aws/.aw2/.awm/.awd対応。未知地形はGROUND。非対応のユニット/施設/兵器はスキップ。",
            ".aws/.aw2/.awm/.awd 지원. 알 수 없는 지형은 GROUND. 지원하지 않는 유닛/시설/병기는 자동 스킵.",
            "Unterstützt .aws/.aw2/.awm/.awd. Unbek. Gelände -> GROUND. Nicht unterstützte Einheiten/Einr./Waffen übersprungen.",
            "Поддерживает .aws/.aw2/.awm/.awd. Неизв. местность -> GROUND. Неподдерж. юниты/сооруж./оружие пропущены.",
            "支持 .aws/.aw2/.awm/.awd 格式，未知地形将保留为 GROUND，不支持的单位/设施/兵器将自动跳过。",
            "支援 .aws/.aw2/.awm/.awd 格式，未知地形將保留為 GROUND，不支援的單位/設施/兵器將自動跳過。");

        Add("AWBW格式每行逗号分隔的地形ID不支持的地形将智能平替为最接近类型完全不支持的将保留为GROUND",
            "AWBW format: comma-separated terrain IDs per line. Unsupported terrain replaced with closest type, or GROUND.",
            "AWBW形式: 各行カンマ区切りの地形ID。非対応地形は最も近い種類に置換、完全非対応はGROUND。",
            "AWBW 형식: 각 줄 쉼표로 구분된 지형 ID. 지원하지 않는 지형은 가장 가까운 유형으로, 완전 미지원은 GROUND.",
            "AWBW-Format: Komma-getrennte Gelände-IDs pro Zeile. Nicht unterstütztes Gelände -> nächstbeste Art oder GROUND.",
            "Формат AWBW: ID местности через запятую. Неподдерж. местность заменяется ближайшим типом или GROUND.",
            "AWBW 格式：每行逗号分隔的地形ID。不支持的地形将智能平替为最接近类型，完全不支持的将保留为 GROUND。",
            "AWBW 格式：每行逗號分隔的地形ID。不支援的地形將智慧平替為最接近類型，完全不支援的將保留為 GROUND。");

        Add("极简格式每行一个对象无复杂Test块更可靠更紧凑",
            "Simple format: one object per line, no complex Test blocks. More reliable and compact.",
            "簡易形式: 1行1オブジェクト、複雑なTestブロックなし。より信頼性高くコンパクト。",
            "간단 형식: 한 줄에 하나의 객체, 복잡한 Test 블록 없음. 더 신뢰할 수 있고 컴팩트함.",
            "Einfaches Format: Ein Objekt pro Zeile, keine komplexen Test-Blöcke. Zuverlässiger und kompakter.",
            "Простой формат: один объект на строку, без сложных Test-блоков. Более надёжный и компактный.",
            "极简格式：每行一个对象，无复杂 Test 块，更可靠、更紧凑。",
            "極簡格式：每行一個物件，無複雜 Test 區塊，更可靠、更緊湊。");

        Add("上传要求尺寸32x32像素格式PNGJPGGIF非标准尺寸将被拒绝",
            "Upload requirements: 32×32 pixels, PNG/JPG/GIF format. Non-standard sizes will be rejected.",
            "アップロード要件: 32×32ピクセル、PNG/JPG/GIF形式。非標準サイズは拒否。",
            "업로드 요구사항: 32×32 픽셀, PNG/JPG/GIF 형식. 비표준 크기는 거부됨.",
            "Upload-Anforderungen: 32×32 Pixel, PNG/JPG/GIF-Format. Nicht standardmäßige Größen werden abgelehnt.",
            "Требования: 32×32 пикселя, формат PNG/JPG/GIF. Нестандартные размеры будут отклонены.",
            "上传要求：尺寸 32×32 像素，格式 PNG/JPG/GIF，非标准尺寸将被拒绝。",
            "上傳要求：尺寸 32×32 像素，格式 PNG/JPG/GIF，非標準尺寸將被拒絕。");

        Add("移动鼠标调整大小按确认填充或取消",
            "Move mouse to adjust size, press confirm to fill or cancel.",
            "マウス移動でサイズ調整、確認で塗りつぶし、キャンセルで中止。",
            "마우스 이동으로 크기 조정, 확인으로 채우기, 취소로 중단.",
            "Maus bewegen zur Größenanpassung, Bestätigen zum Füllen oder Abbrechen.",
            "Двигайте мышь для изменения размера, подтвердите для заливки или отмените.",
            "移动鼠标调整大小，按确认填充或取消。",
            "移動滑鼠調整大小，按確認填充或取消。");

        Add("已保存到应用目录内容已复制到剪贴板手机可在剪贴板粘贴分享",
            "Saved to app directory. Content copied to clipboard. Mobile: paste from clipboard to share.",
            "アプリディレクトリに保存。クリップボードにコピー済。モバイルはクリップボードから貼付けて共有。",
            "앱 디렉토리에 저장됨. 클립보드에 복사됨. 모바일은 클립보드에서 붙여넣기로 공유.",
            "Im App-Verzeichnis gespeichert. In Zwischenablage kopiert. Mobil: Aus Zwischenablage einfügen zum Teilen.",
            "Сохранено в каталог приложения. Содержимое скопировано в буфер обмена. Моб.: вставьте из буфера для отправки.",
            "已保存到应用目录，内容已复制到剪贴板，手机可在剪贴板粘贴分享。",
            "已儲存到應用目錄，內容已複製到剪貼簿，手機可在剪貼簿貼上分享。");

        Add("点击地图上的单位兵器设施来替换其贴图",
            "Click on units/weapons/facilities on the map to replace their textures.",
            "マップ上のユニット/兵器/施設をクリックしてテクスチャを差し替えます。",
            "맵上的 유닛/병기/시설을 클릭하여 텍스처를 교체합니다.",
            "Klicken Sie auf Einheiten/Waffen/Einrichtungen auf der Karte, um deren Texturen zu ersetzen.",
            "Нажмите на юниты/оружие/сооружения на карте, чтобы заменить их текстуры.",
            "点击地图上的单位/兵器/设施来替换其贴图。",
            "點擊地圖上的單位/兵器/設施來替換其貼圖。");

        Add("点击地形选择在地图滑动铺设ESC结束批量放置",
            "Click to select terrain, drag on map to place. ESC to end batch placement.",
            "地形をクリックして選択、マップ上でドラッグして配置。ESCで一括配置終了。",
            "지형을 클릭하여 선택, 맵에서 드래그로 배치. ESC로 일괄 배치 종료.",
            "Klicken zur Geländeauswahl, auf Karte ziehen zum Platzieren. ESC zum Beenden.",
            "Нажмите для выбора местности, перетаскивайте по карте для размещения. ESC для завершения.",
            "点击地形选择，在地图滑动铺设，ESC 结束批量放置。",
            "點擊地形選擇，在地圖滑動鋪設，ESC 結束批量放置。");

        Add("选择已上传贴图然后点击地图上的对象进行替换",
            "Select uploaded texture, then click objects on the map to replace.",
            "アップロード済テクスチャを選択し、マップ上のオブジェクトをクリックして差し替え。",
            "업로드된 텍스처를 선택한 후 맵上的 객체를 클릭하여 교체합니다.",
            "Hochgeladene Textur wählen, dann Objekte auf Karte anklicken zum Ersetzen.",
            "Выберите загруженную текстуру, затем нажмите на объекты на карте для замены.",
            "选择已上传贴图，然后点击地图上的对象进行替换。",
            "選擇已上傳貼圖，然後點擊地圖上的物件進行替換。");

        Add("不规则选区点击格子选中取消再次点击按钮完成选区",
            "Irregular: Click tiles to select/deselect. Click button again to finish.",
            "不規則: マスをクリックで選択/解除。ボタン再クリックで選区完了。",
            "불규칙: 칸 클릭으로 선택/해제. 버튼 다시 클릭으로 선택 완료.",
            "Unregelmäßig: Felder anklicken zum Aus-/Abwählen. Button erneut für Fertig.",
            "Произвольный: нажимайте клетки для выбора/отмены. Нажмите кнопку снова для завершения.",
            "不规则选区：点击格子选中/取消，再次点击按钮完成选区。",
            "不規則選區：點擊格子選中/取消，再次點擊按鈕完成選區。");

        Add("矩形选区点击确定起点再次点击确定终点",
            "Rectangle: Click to set start, click again to set end.",
            "矩形: 起点をクリック、終点を再クリックで確定。",
            "사각형: 시작점 클릭, 다시 클릭으로 종점 확정.",
            "Rechteck: Klicken für Start, erneut klicken für Ende.",
            "Прямоугольник: нажмите для начала, нажмите снова для конца.",
            "矩形选区：点击确定起点，再次点击确定终点。",
            "矩形選區：點擊確定起點，再次點擊確定終點。");

        Add("已确定起点点击终点完成矩形选区",
            "Start point set. Click end point to complete rectangle selection.",
            "起点確定済。終点をクリックして矩形選区を完了。",
            "시작점 확정됨. 종점을 클릭하여 사각 선택 완료.",
            "Startpunkt gesetzt. Endpunkt klicken zum Abschluss der Rechteckauswahl.",
            "Начальная точка установлена. Нажмите конечную точку для завершения прямоуг. выбора.",
            "已确定起点，点击终点完成矩形选区。",
            "已確定起點，點擊終點完成矩形選區。");

        Add("粘贴模式点击目标位置粘贴选区ESC取消",
            "Paste mode: Click target position to paste selection. ESC to cancel.",
            "貼付モード: 対象位置をクリックで選区貼付。ESCで取消。",
            "붙여넣기 모드: 대상 위치 클릭으로 선택 붙여넣기. ESC로 취소.",
            "Einfügemodus: Zielposition klicken zum Einfügen. ESC zum Abbrechen.",
            "Режим вставки: нажмите целевую позицию для вставки выбора. ESC для отмены.",
            "粘贴模式：点击目标位置粘贴选区，ESC取消。",
            "貼上模式：點擊目標位置貼上選區，ESC取消。");

        Add("该格子没有可销毁的目标",
            "No destroyable target on this tile.",
            "このマスに破壊可能な対象がありません。",
            "이 칸에 파괴 가능한 대상이 없습니다.",
            "Kein zerstörbares Ziel auf diesem Feld.",
            "На этой клетке нет целей для уничтожения.",
            "该格子没有可销毁的目标。",
            "該格子沒有可銷毀的目標。");

        Add("原因冷却中",
            "Reason: Cooling down",
            "理由: 冷却中",
            "이유: 쿨다운 중",
            "Grund: Abklingzeit",
            "Причина: перезарядка",
            "原因：冷却中",
            "原因：冷卻中");

        Add("需要APC在范围内补给弹药",
            "Requires APC in range to resupply ammo.",
            "範囲内にAPCが必要で弾薬を補給します。",
            "범위 내 APC가 필요하여 탄약을 보급합니다.",
            "Erfordert APC in Reichweite zur Munitionsauffüllung.",
            "Требуется БТР в радиусе для пополнения боеприпасов.",
            "需要 APC 在范围内补给弹药。",
            "需要 APC 在範圍內補給彈藥。");

        Add("3x3多格占据兵器仅弱点可攻击摧毁后仍不可通行",
            "3×3 multi-tile weapon. Only weak points are vulnerable. Still impassable when destroyed.",
            "3×3複数マス兵器。弱点のみ攻撃可。破壊後も通行不可。",
            "3×3 다중 칸 병기. 약점만 공격 가능. 파괴 후에도 통행 불가.",
            "3×3-Mehrfeld-Waffe. Nur Schwachpunkte verwundbar. Nach Zerstörung weiterhin unpassierbar.",
            "3×3 многоклеточное оружие. Уязвимы только слабые места. После уничтожения всё ещё непроходимо.",
            "3×3 多格占据兵器，仅弱点可攻击，摧毁后仍不可通行。",
            "3×3 多格佔據兵器，僅弱點可攻擊，摧毀後仍不可通行。");

        Add("3x3多格死光炮四方向3格宽激光摧毁后仍不可通行",
            "3×3 Death Ray. 4-direction 3-tile wide laser. Still impassable when destroyed.",
            "3×3死光砲。4方向3マス幅レーザー。破壊後も通行不可。",
            "3×3 사광포. 4방향 3칸 폭 레이저. 파괴 후에도 통행 불가.",
            "3×3 Todesstrahl. 4-Richtungen 3-Felder breiter Laser. Nach Zerstörung weiterhin unpassierbar.",
            "3×3 луч смерти. 4-направления, 3 клетки шириной. После уничтожения всё ещё непроходимо.",
            "3×3 多格死光炮，四方向 3 格宽激光，摧毁后仍不可通行。",
            "3×3 多格死光炮，四方向 3 格寬雷射，摧毀後仍不可通行。");

        Add("三角形范围兵器固定百分比伤害",
            "Triangular area weapon. Fixed percentage damage.",
            "三角形範囲兵器。固定割合ダメージ。",
            "삼각형 범위 병기. 고정 퍼센트 데미지.",
            "Dreiecksbereich-Waffe. Fester Prozentsatz Schaden.",
            "Оружие треугольной области. Фиксированный процентный урон.",
            "三角形范围兵器，固定百分比伤害。",
            "三角形範圍兵器，固定百分比傷害。");

        Add("中程火力支援射程2-3格",
            "Mid-range fire support. Range 2-3 tiles.",
            "中距離火力支援。射程2-3マス。",
            "중거리 화력 지원. 사거리 2-3칸.",
            "Mittelstrecken-Feuerunterstützung. Reichweite 2-3 Felder.",
            "Огневая поддержка средней дальности. Дальность 2-3 клетки.",
            "中程火力支援，射程 2-3 格。",
            "中程火力支援，射程 2-3 格。");

        Add("仅限管道移动远程间接火力射程2-5",
            "Pipe movement only. Long-range indirect fire. Range 2-5.",
            "パイプ移動のみ。遠距離間接火力。射程2-5。",
            "파이프 이동만. 원거리 간접 화력. 사거리 2-5.",
            "Nur Rohr-Bewegung. Fernindirektfeuer. Reichweite 2-5.",
            "Только движение по трубе. Дальний непрямой огонь. Дальность 2-5.",
            "仅限管道移动，远程间接火力，射程 2-5。",
            "僅限管道移動，遠程間接火力，射程 2-5。");

        Add("对步兵机甲毁灭性移动力6弹药9发无副武器",
            "Devastating to infantry/mechs. Move 6. 9 ammo. No secondary weapon.",
            "歩兵/メックに致命的。移動力6。弾薬9。副武器なし。",
            "보병/메크에 치명적. 이동력 6. 탄약 9. 부무기 없음.",
            "Vernichtend für Infanterie/Mechs. Bewegung 6. 9 Munition. Keine Nebenwaffe.",
            "Разрушительно для пехоты/мехов. Передвижение 6. 9 боеприпасов. Нет втор. оружия.",
            "对步兵/机甲毁灭性，移动力 6，弹药 9 发，无副武器。",
            "對步兵/機甲毀滅性，移動力 6，彈藥 9 發，無副武器。");

        Add("射程1-3反装甲单位可近战反击对地高伤害弹药6发",
            "Range 1-3 anti-armor. Can counter in melee. High ground damage. 6 ammo.",
            "射程1-3対装甲。近接反撃可。対地高ダメージ。弾薬6。",
            "사거리 1-3 대장갑. 근접 반격 가능. 대지 고데미지. 탄약 6.",
            "Reichweite 1-3 Anti-Panzer. Kann Nahkampf kontern. Hoher Bodenschaden. 6 Munition.",
            "Дальность 1-3 против брони. Может контратаковать в ближнем бою. Высокий урон по земле. 6 боеприпасов.",
            "射程 1-3 反装甲单位，可近战反击，对地高伤害，弹药 6 发。",
            "射程 1-3 反裝甲單位，可近戰反擊，對地高傷害，彈藥 6 發。");

        Add("快速装甲单位攻防均衡",
            "Fast armored unit. Balanced offense and defense.",
            "高速装甲単位。攻撃と防御のバランス型。",
            "고속 장갑 유닛. 공방 균형.",
            "Schnelle gepanzerte Einheit. Ausgewogener Angriff und Verteidigung.",
            "Быстрый бронированный юнит. Сбалансированная атака и защита.",
            "快速装甲单位，攻防均衡。",
            "快速裝甲單位，攻防均衡。");

        Add("高视野侦察单位移动力8仅副武器视野5",
            "High-vision recon. Move 8. Secondary weapon only. Vision 5.",
            "高視界偵察。移動力8。副武器のみ。視界5。",
            "고시야 정찰. 이동력 8. 부무기만. 시야 5.",
            "Hochsicht-Aufklärer. Bewegung 8. Nur Nebenwaffe. Sicht 5.",
            "Разведчик с высоким обзором. Передвижение 8. Только втор. оружие. Обзор 5.",
            "高视野侦察单位，移动力 8，仅副武器，视野 5。",
            "高視野偵察單位，移動力 8，僅副武器，視野 5。");

        Add("反装甲步兵移动力较低",
            "Anti-armor infantry. Lower movement.",
            "対装甲歩兵。移動力やや低。",
            "대장갑 보병. 이동력 다소 낮음.",
            "Anti-Panzer-Infanterie. Geringere Bewegung.",
            "Противотанковая пехота. Меньшее передвижение.",
            "反装甲步兵，移动力较低。",
            "反裝甲步兵，移動力較低。");

        Add("可发射照明弹驱散战争迷雾移动力5履带移动仅副武器",
            "Can launch flares to dispel fog. Move 5. Treads. Secondary weapon only.",
            "照明弾で霧を驱散。移動力5。キャタピラ。副武器のみ。",
            "조명탄으로 안개를驱散. 이동력 5. 궤도. 부무기만.",
            "Kann Leuchtraketen zum Vertreiben des Nebels abfeuern. Bewegung 5. Ketten. Nur Nebenwaffe.",
            "Может запускать сигнальные ракеты для рассеивания тумана. Передвижение 5. Гусеницы. Только втор. оружие.",
            "可发射照明弹驱散战争迷雾，移动力 5，履带移动，仅副武器。",
            "可發射照明彈驅散戰爭迷霧，移動力 5，履帶移動，僅副武器。");

        Add("吞噬型单位可消灭任何敌人",
            "Devourer type. Can eliminate any enemy.",
            "吞噬型。あらゆる敵を消滅可能。",
            "吞噬형. 모든 적을 소멸 가능.",
            "Verschlinger-Typ. Kann jeden Feind eliminieren.",
            "Тип пожирателя. Может уничтожить любого врага.",
            "吞噬型单位，可消灭任何敌人。",
            "吞噬型單位，可消滅任何敵人。");

        Add("自爆单位3格范围50HP伤害空军移动力9",
            "Self-destruct unit. 3-tile range, 50% HP damage. Air unit, move 9.",
            "自爆ユニット。3マス範囲、50%HPダメージ。空軍、移動力9。",
            "자폭 유닛. 3칸 범위, 50% HP 데미지. 공군, 이동력 9.",
            "Selbstzerstörungseinheit. 3-Felder-Reichw., 50% LP-Schaden. Lufteinheit, Bewegung 9.",
            "Юнит самоуничтожения. Радиус 3 клетки, 50% урона ОЗ. Воздушный юнит, передвижение 9.",
            "自爆单位，3 格范围 50% HP 伤害，空军，移动力 9。",
            "自爆單位，3 格範圍 50% HP 傷害，空軍，移動力 9。");

        Add("环形范围治疗补给兵器还原原版AW黑水晶机制",
            "Ring-area heal + resupply weapon. Restores original AW Black Crystal mechanic.",
            "環状範囲回復+補給兵器。原作AW黒水晶メカニック再現。",
            "환형 범위 치료+보급 병기. 원작 AW 흑수정 메커니즘 재현.",
            "Ringbereich-Heilung + Nachschubwaffe. Stellt originalen AW-Schwarzkristall-Mechanismus wieder her.",
            "Кольцевая область лечения + снабжение оружием. Восстанавливает оригинальную механику чёрного кристалла AW.",
            "环形范围治疗+补给兵器，还原原版 AW 黑水晶机制。",
            "環形範圍治療+補給兵器，還原原版 AW 黑水晶機制。");

        Add("运输单位可搭载步兵",
            "Transport unit. Can carry infantry.",
            "輸送単位。歩兵を搭載可能。",
            "수송 유닛. 보병을 탑재 가능.",
            "Transporteinheit. Kann Infanterie transportieren.",
            "Транспортный юнит. Может перевозить пехоту.",
            "运输单位，可搭载步兵。",
            "運輸單位，可搭載步兵。");

        Add("基础步兵单位可占领城市",
            "Basic infantry unit. Can capture cities.",
            "基本歩兵単位。都市を占領可能。",
            "기본 보병 유닛. 도시를 점령 가능.",
            "Basis-Infanterieeinheit. Kann Städte erobern.",
            "Базовый пехотный юнит. Может захватывать города.",
            "基础步兵单位，可占领城市。",
            "基礎步兵單位，可佔領城市。");

        Add("城市设施可占领并提供补给",
            "City facility. Can be captured and provides supply.",
            "都市施設。占領可能で補給を提供。",
            "도시 시설. 점령 가능하고 보급 제공.",
            "Stadteinrichtung. Kann erobert werden und bietet Versorgung.",
            "Городское сооружение. Можно захватить, обеспечивает снабжение.",
            "城市设施，可占领并提供补给。",
            "城市設施，可佔領並提供補給。");

        Add("基地设施可占领补给并生产地面单位",
            "Base facility. Can be captured, supplies, and produces ground units.",
            "基地施設。占領・補給・地上ユニット生産可能。",
            "기지 시설. 점령, 보급, 지상 유닛 생산 가능.",
            "Basiseinrichtung. Kann erobert werden, versorgt und produziert Bodeneinheiten.",
            "Базовое сооружение. Можно захватить, снабжает, производит наземных юнитов.",
            "基地设施，可占领、补给并生产地面单位。",
            "基地設施，可佔領、補給並生產地面單位。");

        Add("机场设施可占领补给并生产空军单位",
            "Airport facility. Can be captured, supplies, and produces air units.",
            "飛行場施設。占領・補給・空軍ユニット生産可能。",
            "공항 시설. 점령, 보급, 공군 유닛 생산 가능.",
            "Flugplatzeinrichtung. Kann erobert werden, versorgt und produziert Lufteinheiten.",
            "Аэродромное сооружение. Можно захватить, снабжает, производит воздушных юнитов.",
            "机场设施，可占领、补给并生产空军单位。",
            "機場設施，可佔領、補給並生產空軍單位。");

        Add("远程间接火力射程3-5格",
            "Long-range indirect fire. Range 3-5 tiles.",
            "遠距離間接火力。射程3-5マス。",
            "원거리 간접 화력. 사거리 3-5칸.",
            "Langstrecken-Indirektfeuer. Reichweite 3-5 Felder.",
            "Дальний непрямой огонь. Дальность 3-5 клеток.",
            "远程间接火力，射程 3-5 格。",
            "遠程間接火力，射程 3-5 格。");

        Add("重火力履带单位对轻甲毁灭性移动力5",
            "Heavy firepower treads unit. Devastating to light armor. Move 5.",
            "重火力キャタピラ単位。軽装甲に致命的。移動力5。",
            "중화력 궤도 유닛. 경장갑에 치명적. 이동력 5.",
            "Schwere Feuerkraft-Ketteneinheit. Vernichtend für leichte Panzerung. Bewegung 5.",
            "Тяжёлый огневой гусеничный юнит. Разрушителен для лёгкой брони. Передвижение 5.",
            "重火力履带单位，对轻甲毁灭性，移动力 5。",
            "重火力履帶單位，對輕甲毀滅性，移動力 5。");

        Add("直线激光兵器穿透攻击可配置角度射程冷却",
            "Linear laser weapon. Penetrating attack. Configurable angle/range/cooldown.",
            "直線レーザー兵器。貫通攻撃。角度/射程/クールダウン設定可。",
            "직선 레이저 병기. 관통 공격. 각도/사거리/쿨다운 설정 가능.",
            "Linear-Laser-Waffe. Durchdringender Angriff. Konfigurierbarer Winkel/Reichweite/Abklingzeit.",
            "Линейное лазерное оружие. Проникающая атака. Настраиваемый угол/дальность/перезарядка.",
            "直线激光兵器，穿透攻击，可配置角度/射程/冷却。",
            "直線雷射兵器，穿透攻擊，可配置角度/射程/冷卻。");

        Add("快速步兵单位可占领城市移动力5轮胎移动",
            "Fast infantry. Can capture cities. Move 5. Tire movement.",
            "高速歩兵。都市占領可。移動力5。タイヤ移動。",
            "고속 보병. 도시 점령 가능. 이동력 5. 타이어 이동.",
            "Schnelle Infanterie. Kann Städte erobern. Bewegung 5. Reifen-Bewegung.",
            "Быстрая пехота. Может захватывать города. Передвижение 5. Колёсное движение.",
            "快速步兵单位，可占领城市，移动力 5，轮胎移动。",
            "快速步兵單位，可佔領城市，移動力 5，輪胎移動。");
    }
}
