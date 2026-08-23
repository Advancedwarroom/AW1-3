path = r'C:\Users\honor\Documents\aw-one-new\spricts\Infantry.cs'
with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# 1. 删除错误插入的代码（第1037-1042行，0-based index 是1036-1041）
# 找到 "// ✅ 兜底：编辑器中放置的单位自动绑定到最近格子" 到 "}" 的块
start_del = -1
end_del = -1
for i, line in enumerate(lines):
    if '// ✅ 兜底：编辑器中放置的单位自动绑定到最近格子' in line:
        start_del = i
    if start_del >= 0 and i > start_del and line.strip() == '}':
        end_del = i
        break

if start_del >= 0 and end_del >= 0:
    # 删除从 start_del 到 end_del+1 的行（包括可能的空行）
    # 还要删除后面的空行
    del_lines = []
    for i in range(start_del, min(end_del + 2, len(lines))):
        if i >= start_del:
            del_lines.append(i)
    # 从后往前删，避免索引偏移
    for i in reversed(del_lines):
        del lines[i]
    print(f"Deleted wrongly inserted block at lines {start_del}-{end_del}")
else:
    print("Wrongly inserted block not found")

# 2. 在 Infantry._Ready() 的最后一个 } 之前插入正确的代码
# 找到 "isPublicVisionUnit = true;" 后面的 "}" 和空行结构
insert_idx = -1
for i, line in enumerate(lines):
    if 'isPublicVisionUnit = true;' in line:
        # 向下找到紧跟的 }（Infantry._Ready 的结束）
        for j in range(i+1, min(i+5, len(lines))):
            if lines[j].strip() == '}':
                insert_idx = j
                break
        break

if insert_idx >= 0:
    new_code = '''\n        // ✅ 兜底：编辑器中放置的单位自动绑定到最近格子\n        if (grid == null)\n        {\n            var gm = GetTree()?.GetFirstNodeInGroup("game_manager") as GameManager;\n            gm?.gridManager?.SetGrid(this);\n        }\n'''
    lines.insert(insert_idx, new_code)
    with open(path, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    print(f"Inserted SetGrid fallback before line {insert_idx}")
else:
    print("Insert position not found")
