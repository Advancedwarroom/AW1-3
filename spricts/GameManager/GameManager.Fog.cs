// GameManager.cs - 添加完整的单位统计系统
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// GameManager · Fog（由 GameManager.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class GameManager
{

    // ========== ✅ 战争迷雾公共API ==========
    public void SetFogOfWarEnabled(bool enabled)
    {
        fogOfWarManager?.SetFogOfWarEnabled(enabled);
    }

    public bool IsFogOfWarEnabled()
    {
        return fogOfWarManager?.isFogOfWarEnabled ?? false;
    }
}
