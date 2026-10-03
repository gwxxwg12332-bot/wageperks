import re, os

src = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\PatchRegistry.cs"
with open(src, encoding="utf-8") as f:
    lines = f.readlines()

# 匹配 ManualPatcher.TryPatch(...) 调用（可能跨多行，按分号结束）
# 简单方案：逐行匹配，跳过跨行的（跨行的先标记）
pat = re.compile(r'ManualPatcher\.TryPatch\(([^;]+)\);')
entries = []
for i, line in enumerate(lines, 1):
    m = pat.search(line)
    if not m:
        continue
    args = m.group(1).strip()
    # 拆参数——简单按逗号拆，但 typeof/Type[] 里有逗号
    # 手动解析：Target, Method, Prefix, Postfix, paramTypes, Host, priority
    # 形式：TryPatch(typeof(X), "M", "pre"/null, "post"/null, [paramTypes], [host], [priority])
    # 先粗拆
    entries.append((i, args))

print(f"解析到 {len(entries)} 个TryPatch调用")
for i, (ln, args) in enumerate(entries[:5]):
    print(f"  L{ln}: {args[:120]}")

# 输出到文件
out = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\_registry_draft.txt"
with open(out, "w", encoding="utf-8") as f:
    for ln, args in entries:
        f.write(f"L{ln}\t{args}\n")
print(f"已写 {out}")
