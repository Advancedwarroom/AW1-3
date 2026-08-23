path = r'C:\Users\honor\Documents\aw-one-new\spricts\Infantry.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old = '''        // ✅ P-1 单位默认开启视野共享（如果team是Player且未手动设置）
        if (team == TeamHelper.Player && !isPublicVisionUnit)
        {
            isPublicVisionUnit = true;
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
    print("Added SetGrid fallback")
else:
    print("Pattern not found")
