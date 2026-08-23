path = r'C:\Users\honor\Documents\aw-one-new\spricts\Infantry.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# 先把错误的内容整体替换掉
old = '''        // ✅ P-1 单位默认开启视野共享（如果team是Player且未手动设置）
        if (team == TeamHelper.Player && !isPublicVisionUnit)
        {
            isPublicVisionUnit = true;

        // ✅ 兜底：编辑器中放置的单位自动绑定到最近格子
        if (grid == null)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager?.SetGrid(this);
        }
        }

    }'''

new = '''        // ✅ P-1 单位默认开启视野共享（如果team是Player且未手动设置）
        if (team == TeamHelper.Player && !isPublicVisionUnit)
        {
            isPublicVisionUnit = true;
        }

        // ✅ 兜底：编辑器中放置的单位自动绑定到最近格子
        if (grid == null)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager?.SetGrid(this);
        }

    }'''

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed insertion position")
else:
    print("Pattern not found, trying alternative...")
    # 备用方案：直接重写整个文件末尾的相关部分
    # 先读取所有行
    with open(path, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # 找到需要修改的区域
    start_idx = -1
    end_idx = -1
    for i, line in enumerate(lines):
        if '// ✅ P-1 单位默认开启视野共享' in line:
            start_idx = i
        if start_idx >= 0 and i > start_idx + 10 and 'public virtual void ApplyUnitSpecificDefaults()' in line:
            end_idx = i
            break
    
    if start_idx >= 0 and end_idx >= 0:
        new_block = '''        // ✅ P-1 单位默认开启视野共享（如果team是Player且未手动设置）
        if (team == TeamHelper.Player && !isPublicVisionUnit)
        {
            isPublicVisionUnit = true;
        }

        // ✅ 兜底：编辑器中放置的单位自动绑定到最近格子
        if (grid == null)
        {
            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;
            gm?.gridManager?.SetGrid(this);
        }

    }

'''
        lines = lines[:start_idx] + [new_block] + lines[end_idx:]
        with open(path, 'w', encoding='utf-8') as f:
            f.writelines(lines)
        print("Fixed via line replacement")
    else:
        print(f"Could not find boundaries: start={start_idx}, end={end_idx}")
