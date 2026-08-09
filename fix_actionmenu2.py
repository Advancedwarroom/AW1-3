import sys
sys.stdout.reconfigure(encoding='utf-8')

with open(r'C:/Users/honor/Documents/aw-one-new/spricts/ActionMenu.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# 删除 ShowTransportLoadMenu 中的重复旧代码块
# 旧块：if (!IsNodeReady()) return; 后面紧跟着 { { currentUnit = unit; currentWeapon = null; if (transport is APC...
old_block = '''\t\tif (!IsNodeReady()) return;
\t\t{
    \t\t{
\tcurrentUnit = unit;
\tcurrentWeapon = null;
\tif (transport is APC apc)
\t{
\tcurrentTransport = apc;
\t}
\telse
\t{
\tcurrentTransport = transport;  // 非APC的运输单位
    \t}

\tif (!IsNodeReady()) return;'''

new_block = '''\t\tif (!IsNodeReady()) return;'''

content = content.replace(old_block, new_block)

# 也处理只有一层缩进的情况
old_block2 = '''\t\tif (!IsNodeReady()) return;
\t\t{
\t\t\t{
\t\t\t\tcurrentUnit = unit;
\t\t\t\tcurrentWeapon = null;
\t\t\t\tif (transport is APC apc)
\t\t\t\t{
\t\t\t\t\tcurrentTransport = apc;
\t\t\t\t}
\t\t\t\telse
\t\t\t\t{
\t\t\t\t\tcurrentTransport = transport;  // 非APC的运输单位
    \t\t\t\t}

\t\t\t\tif (!IsNodeReady()) return;'''

content = content.replace(old_block2, new_block)

with open(r'C:/Users/honor/Documents/aw-one-new/spricts/ActionMenu.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print("✅ 重复代码块已清理")
