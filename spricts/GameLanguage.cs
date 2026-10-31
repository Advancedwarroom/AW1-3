// TranslateGroup.cs - 翻译组 (Translation Group) v2.0
// 支持自动检测系统语言 + 左下角语言切换按钮UI
// 支持7种语言: 英语/日语/韩语/德语/俄语/简体中文/繁体中文
using Godot;
using System;
using System.Collections.Generic;

// 由 TranslateGroup.cs 拆出的共享类型

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
