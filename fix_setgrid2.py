path = r'C:\Users\honor\Documents\aw-one-new\spricts\Infantry.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# 找到 isPublicVisionUnit = true; 所在的行，在其后面插入 SetGrid 兜底
insert_after = -1
for i, line in enumerate(lines):
    if 'isPublicVisionUnit = true;' in line:
        insert_after = i
        break

if insert_after >= 0:
    # 找到对应的闭合大括号后的空行
    # 在 insert_after 后面找到下一个空行或右大括号
    insert_idx = insert_after + 1
    # 跳过右大括号和空行
    while insert_idx < len(lines) and lines[insert_idx].strip() in ['', '}']:
        insert_idx += 1
    # 实际上我们要在 Infantry._Ready() 的最后一个大括号之前插入
    # 找到第1034行附近的结构
    new_code = '''\n        // ✅ 兜底：编辑器中放置的单位自动绑定到最近格子\n        if (grid == null)\n        {\n            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;\n            gm?.gridManager?.SetGrid(this);\n        }\n'''
    lines.insert(insert_idx, new_code)
    with open(path, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    print(f"Inserted SetGrid fallback after line {insert_after}")
else:
    print("isPublicVisionUnit = true; not found")
