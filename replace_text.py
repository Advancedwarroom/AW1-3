# replace_text.py
# 放在你的 Godot 项目根目录，和 spricts/ 文件夹同级，然后运行

import re
import os

def replace_in_file(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    replaced = 0
    
    # 匹配: xxx.Text = TranslateGroup.Instance.Get("key"); 或 TranslateGroup.T("key");
    pattern = r'^([ \t]*)([\w\.]+)\.Text\s*=\s*TranslateGroup\.(Instance\.Get|T)\(\"([^\"]+)\"\)\s*;(.*)$'
    
    def replacer(m):
        nonlocal replaced
        indent = m.group(1)
        ctrl = m.group(2)
        key = m.group(4)
        tail = m.group(5) if m.group(5) else ""
        replaced += 1
        return f'{indent}TranslateGroup.SetText({ctrl}, "{key}");{tail}'
    
    new_content = re.sub(pattern, replacer, content, flags=re.MULTILINE)
    
    if replaced > 0:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"  ✓ {os.path.basename(path)}: {replaced} 处")
    
    return replaced

# 遍历 spricts 目录（改为你自己的脚本目录）
total = 0
for root, dirs, files in os.walk('spricts'):
    for fname in files:
        if fname.endswith('.cs'):
            total += replace_in_file(os.path.join(root, fname))

print(f"\n✅ 总共替换: {total} 处")