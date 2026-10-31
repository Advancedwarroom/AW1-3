// GameTranslation.cs - 接入 Godot 原生 TranslationServer 的翻译资源
// 每种语言注册一个实例；所有 Control（Label/Button/Tooltip/OptionButton/PopupMenu/TabBar/Window 标题等）
// 在 auto_translate 开启（默认）时都会经由此类翻译，切换语言时引擎会自动重新翻译整棵场景树。
using Godot;

public partial class GameTranslation : Translation
{
    public GameLanguage Language;

    public GameTranslation() { }

    public GameTranslation(GameLanguage lang, string locale)
    {
        Language = lang;
        Locale = locale;
    }

    public override StringName _GetMessage(StringName srcMessage, StringName context)
    {
        string src = srcMessage;
        if (string.IsNullOrEmpty(src)) return srcMessage;
        var tg = TranslateGroup.Instance;
        if (tg == null) return srcMessage;
        // 始终返回非空结果，避免 Godot 回退到 fallback 语言（默认 en）把中文误翻成英文
        return tg.Translate(src, Language);
    }
}
