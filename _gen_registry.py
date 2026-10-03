import re, os

src = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\PatchRegistry.cs"
with open(src, encoding="utf-8") as f:
    content = f.read()
    lines = content.split("\n")

# 匹配所有 ManualPatcher.TryPatch(...) 调用（含跨行）
# 用括号配对
pat = re.compile(r'ManualPatcher\.TryPatch\(')
entries = []
for i, line in enumerate(lines, 1):
    m = pat.search(line)
    if not m:
        continue
    # 从 ( 开始配对括号
    start = m.end() - 1  # 指向 (
    depth = 0
    j = i - 1  # 当前行索引（0-based）
    col = start
    buf = ""
    while j < len(lines):
        ln = lines[j]
        while col < len(ln):
            c = ln[col]
            buf += c
            if c == '(':
                depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    break
            col += 1
        if depth == 0:
            break
        j += 1
        col = 0
    # buf = "(...)"
    inner = buf[1:-1].strip()
    # 去掉末尾分号
    if inner.endswith(";"):
        inner = inner[:-1].strip()
    entries.append((i, inner))

print(f"共解析 {len(entries)} 个TryPatch")

# 解析每个entry的参数
def parse_args(s):
    # 简单按逗号拆，但要跳过 typeof()/Type[] 里的逗号
    args = []
    depth = 0
    cur = ""
    i = 0
    while i < len(s):
        c = s[i]
        if c in '({[':
            depth += 1
            cur += c
        elif c in ')}]':
            depth -= 1
            cur += c
        elif c == ',' and depth == 0:
            args.append(cur.strip())
            cur = ""
        else:
            cur += c
        i += 1
    if cur.strip():
        args.append(cur.strip())
    return args

# 生成 PatchEntry
out_lines = []
out_lines.append("// 自动生成初稿，人工核对24个多挂方法")
out_lines.append("new PatchEntry { Target = typeof(X), Method = \"M\", Prefix = ..., Postfix = ..., Host = ..., Priority = DEFAULT_PRIORITY, System = \"?\", YieldMod = null, Note = \"\" },")
out_lines.append("")

for ln, inner in entries:
    args = parse_args(inner)
    # args: [Target, Method, Prefix, Postfix, paramTypes?, Host?, priority?]
    target = args[0] if len(args) > 0 else "???"
    method = args[1] if len(args) > 1 else "???"
    prefix = args[2] if len(args) > 2 else "null"
    postfix = args[3] if len(args) > 3 else "null"
    # 找 Host (typeof(X)) 和 priority (数字)
    host = "typeof(Patches)"  # 默认
    priority = "DEFAULT_PRIORITY"
    for a in args[4:]:
        if a.startswith("typeof(") and "Type[" not in a and "System.Type" not in a:
            host = a
        elif re.match(r'^-?\d+$', a):
            priority = a
    # method 是字符串
    out_lines.append(f"// L{ln}")
    out_lines.append(f'new PatchEntry {{ Target = {target}, Method = {method}, Prefix = {prefix}, Postfix = {postfix}, Host = {host}, Priority = {priority}, System = "?", YieldMod = null, Note = "" }},')

out = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\_registry_generated.txt"
with open(out, "w", encoding="utf-8") as f:
    f.write("\n".join(out_lines))
print(f"已写 {out}")
