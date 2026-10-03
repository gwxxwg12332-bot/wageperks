import re

src = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\PatchRegistry.cs"
with open(src, encoding="utf-8") as f:
    lines = f.read().split("\n")

# 解析所有 TryPatch 调用
pat = re.compile(r'ManualPatcher\.TryPatch\(')
entries = []
for i, line in enumerate(lines, 1):
    m = pat.search(line)
    if not m:
        continue
    start = m.end() - 1
    depth = 0
    j = i - 1
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
    inner = buf[1:-1].strip()
    if inner.endswith(";"):
        inner = inner[:-1].strip()
    entries.append((i, inner))

def parse_args(s):
    args = []
    depth = 0
    cur = ""
    for c in s:
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
    if cur.strip():
        args.append(cur.strip())
    return args

# 生成条目
out_entries = []
for ln, inner in entries:
    args = parse_args(inner)
    target = args[0] if len(args) > 0 else "???"
    method = args[1] if len(args) > 1 else "???"
    prefix = args[2] if len(args) > 2 else "null"
    postfix = args[3] if len(args) > 3 else "null"
    host = "typeof(Patches)"
    priority = "DEFAULT_PRIORITY"
    for a in args[4:]:
        if a.startswith("typeof(") and "Type[" not in a and "System.Type" not in a:
            host = a
        elif re.match(r'^-?\d+$', a):
            priority = a
    # 特殊处理：SaveGame WageSaveStore → FLUSH_PRIORITY
    if "SaveGame" in method and "WageSaveStore" in host:
        priority = "FLUSH_PRIORITY"
    if "EndDay" in method and "WageSaveStore" in host:
        priority = "FLUSH_PRIORITY"
    out_entries.append(f'            new PatchEntry {{ Target = {target}, Method = {method}, Prefix = {prefix}, Postfix = {postfix}, Host = {host}, Priority = {priority}, System = "?", YieldMod = null, Note = "" }},')

# 写完整文件
header = '''using System;
using System.Linq;

namespace WagePerks
{
    /// <summary>阶段6：统一Patch注册入口（声明式注册表）。平移不改语义。</summary>
    internal static class PatchRegistryTable
    {
        public const int FLUSH_PRIORITY   = 0;
        public const int LAST_PRIORITY    = -1000;
        public const int DEFAULT_PRIORITY = 400;

        public sealed class PatchEntry
        {
            public Type   Target;
            public string Method;
            public string Prefix;
            public string Postfix;
            public Type   Host;
            public int    Priority;
            public string System;
            public string YieldMod;
            public string Note;
        }

        internal static readonly PatchEntry[] REGISTRY = new PatchEntry[]
        {
'''

footer = '''        };

        public static void ApplyAll()
        {
            int ok = 0, fail = 0, yield = 0;
            foreach (var e in REGISTRY)
            {
                try
                {
                    if (e.YieldMod != null && ModCompat.ShouldYield(e.Target, e.Method, out var mod))
                    {
                        Core.LogMsg($"[统一注册] 让路: [{e.System}] {e.Target.Name}.{e.Method} ← {mod}");
                        yield++; continue;
                    }
                    ManualPatcher.TryPatch(e.Target, e.Method, e.Prefix, e.Postfix,
                                           parameterTypes: null, patchHost: e.Host, priority: e.Priority);
                    ok++;
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[统一注册] 异常: [{e.System}] {e.Target.Name}.{e.Method}: {ex.Message}");
                    fail++;
                }
            }
            Core.LogMsg($"[统一注册] Patch 挂载: 成功 {ok} / 失败 {fail} / 让路 {yield}（共 {REGISTRY.Length}）");
            foreach (var group in REGISTRY.GroupBy(e => e.Target.Name + "." + e.Method).Where(g => g.Count() >= 2))
                Core.LogMsg($"[统一注册] 多挂 {group.Key} ×{group.Count()}: {string.Join(",", group.Select(e => e.System))}");
        }
    }
}
'''

full = header + "\n".join(out_entries) + "\n" + footer
out = r"D:\DoubaoWork\Project_001_WagesPerks\07_开发资产\ProbablyStolen_DevFiles\Mods_开发源码与临时文件\JacksonPerks\PatchRegistryTable.cs"
with open(out, "w", encoding="utf-8") as f:
    f.write(full)
print(f"生成 {len(out_entries)} 条 → {out}")
