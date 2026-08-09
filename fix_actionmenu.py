import sys
sys.stdout.reconfigure(encoding='utf-8')

with open(r'C:/Users/honor/Documents/aw-one-new/spricts/ActionMenu.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 1. 替换 GetUnitAnim：直接读基类 animSprite，去掉所有 is 硬编码
old_getunit = '''    private AnimatedSprite2D GetUnitAnim(Infantry u)
    {
        // 1. 优先通过反射获取 animSprite 字段（通用，不硬编码类型）
        var animField = u.GetType().GetField("animSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (animField != null)
        {
            var anim = animField.GetValue(u) as AnimatedSprite2D;
            if (anim != null && IsInstanceValid(anim)) return anim;
        }

        // 2. 兼容旧硬编码（保留所有类型）
        if (u is Mech m && m?.animSprite != null) return m.animSprite;
        if (u is LightTank t && t?.animSprite != null) return t.animSprite;
        if (u is APC a && a?.animSprite != null) return a.animSprite;
        if (u is Oozium o && o?.animSprite != null) return o.animSprite;
        if (u is Artillery ar && ar?.animSprite != null) return ar.animSprite;
        if (u is PipeRunner pr && pr?.animSprite != null) return pr.animSprite;
        if (u is Flare fl && fl?.animSprite != null) return fl.animSprite;
        if (u is Bike bk && bk?.animSprite != null) return bk.animSprite;
        if (u is AntiAir aa && aa?.animSprite != null) return aa.animSprite;
        if (u is AntiTank at && at?.animSprite != null) return at.animSprite;
        if (u is Recon rc && rc?.animSprite != null) return rc.animSprite;
        if (u is MdTank md && md?.MdSprite != null) return md.MdSprite;
        if (u is Rocket rk && rk?.animSprite != null) return rk.animSprite;
        if (u is FlyBomb fb && fb?.animSprite != null) return fb.animSprite;

        // 3. 递归查找子节点（备用）
        foreach (var child in u.GetChildren())
        {
            if (child is AnimatedSprite2D anim) return anim;
            foreach (var grandChild in (child as Node)?.GetChildren() ?? new Godot.Collections.Array<Node>())
                if (grandChild is AnimatedSprite2D deepAnim) return deepAnim;
        }

        return null;
    }'''

new_getunit = '''    private AnimatedSprite2D GetUnitAnim(Infantry u)
    {
        // 1. 基类 animSprite（迁移后所有单位统一走这里）
        if (u.animSprite != null && IsInstanceValid(u.animSprite))
            return u.animSprite;

        // 2. 反射获取子类可能自定义的动画字段（如 MdTank.MdSprite）
        var animField = u.GetType().GetField("animSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (animField != null)
        {
            var anim = animField.GetValue(u) as AnimatedSprite2D;
            if (anim != null && IsInstanceValid(anim)) return anim;
        }
        var mdField = u.GetType().GetField("MdSprite", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (mdField != null)
        {
            var md = mdField.GetValue(u) as AnimatedSprite2D;
            if (md != null && IsInstanceValid(md)) return md;
        }

        // 3. 递归查找子节点（备用）
        foreach (var child in u.GetChildren())
        {
            if (child is AnimatedSprite2D anim) return anim;
            foreach (var grandChild in (child as Node)?.GetChildren() ?? new Godot.Collections.Array<Node>())
                if (grandChild is AnimatedSprite2D deepAnim) return deepAnim;
        }

        return null;
    }'''

content = content.replace(old_getunit, new_getunit)

# 2. 替换 transport is APC → 通用运输单位处理
old_apc_check = '''\tif (transport is APC apc)
\t{
\tcurrentTransport = apc;
\t}
\telse
\t{
\tcurrentTransport = transport;  // 非APC的运输单位
    \t}'''

new_apc_check = '''\tcurrentTransport = transport;  // ✅ 通用：任何 canTransportUnits 的单位'''

content = content.replace(old_apc_check, new_apc_check)

# 3. 替换单位类型名硬编码 → UnitRegistry.DisplayName
old_type_name = '''        string unitType = currentUnit is FlyBomb ? TranslateGroup.T("飞弹") :
                currentUnit is Oozium ? "Oozium" :
                currentUnit is AntiTank ? TranslateGroup.T("反坦克炮") :
                currentUnit is Mech ? TranslateGroup.T("反坦步兵") :
                currentUnit is LightTank ? TranslateGroup.T("轻型坦克") :
                currentUnit is Recon ? TranslateGroup.T("侦察车") :
                currentUnit is Artillery ? TranslateGroup.T("自行火炮") :
                currentUnit is APC ? TranslateGroup.T("运输车") : 
                currentUnit is Rocket ? TranslateGroup.T("火箭炮") :
                currentUnit is MdTank ? TranslateGroup.T("重型坦克") :
                currentUnit is AntiAir ? TranslateGroup.T("防空高炮") :
                currentUnit is PipeRunner ? TranslateGroup.T("管道炮") :
                currentUnit is Infantry ? TranslateGroup.T("步兵") : "Unknown";'''

new_type_name = '''        // ✅ 从 UnitRegistry 读取单位显示名，零硬编码适配新单位
        string unitType = UnitRegistry.Get(currentUnit.GetType().Name)?.DisplayName
            ?? currentUnit.GetType().Name;'''

content = content.replace(old_type_name, new_type_name)

# 4. 替换装载单位类型名硬编码 → UnitRegistry.DisplayName
old_loaded_type = '''                string loadedType = transportedUnit is Mech ? TranslateGroup.T("机甲") :
                                  transportedUnit is LightTank ? TranslateGroup.T("坦克") :
                                  transportedUnit is Oozium ? TranslateGroup.T("史莱姆") :
                                  transportedUnit is Artillery ? TranslateGroup.T("火炮") :
                                  transportedUnit is Infantry ? TranslateGroup.T("步兵") : TranslateGroup.T("未知");'''

new_loaded_type = '''                // ✅ 从 UnitRegistry 读取装载单位显示名
                string loadedType = UnitRegistry.Get(transportedUnit.GetType().Name)?.DisplayName
                    ?? transportedUnit.GetType().Name;'''

content = content.replace(old_loaded_type, new_loaded_type)

with open(r'C:/Users/honor/Documents/aw-one-new/spricts/ActionMenu.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("✅ ActionMenu.cs 硬编码已清除")
