# -*- coding: utf-8 -*-
# sync_test_targets.py
# 从飞书开发任务看板同步"待测试"任务 → Mods\test_targets.txt
# mod 内 AutoSelfTest(-runtests) 读取该文件，按任务名关键词自动匹配测试逻辑。
# 总指挥/测试AI 只需在看板把任务状态改成"待测试"，下次自测自动包含它。
# 用法: python sync_test_targets.py   (成功返回 0)
import subprocess, json, os, sys, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE_TOKEN = "F87Vbih9Hap2RJsjgdhcbYHTnId"
TABLE_ID = "tblTFpT4GcgGFu3S"
GAME_DIR = r"D:\Steam\steamapps\common\Probably Stolen Playtest"
OUT = os.path.join(GAME_DIR, "Mods", "test_targets.txt")


def main():
    cmd = ["lark-cli", "base", "+record-list",
           "--base-token", BASE_TOKEN, "--table-id", TABLE_ID,
           "--page-size", "50", "--json"]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
    if r.returncode != 0 or not r.stdout.strip():
        print("FAIL: lark-cli 拉取看板失败")
        if r.stderr:
            print("stderr:", r.stderr[:500])
        return 1
    try:
        data = json.loads(r.stdout)
        rows = data["data"]["data"]
        ids = data["data"]["record_id_list"]
        fields = data["data"]["fields"]
    except Exception as e:
        print("FAIL: 解析看板返回失败:", e)
        return 1

    idx_name = fields.index("任务")
    idx_status = fields.index("状态")
    idx_desc = fields.index("需求描述")
    idx_owner = fields.index("负责人")

    lines = []
    for row, rid in zip(rows, ids):
        status = row[idx_status]
        if not status or status[0] != "待测试":
            continue
        name = (row[idx_name] or "").replace("|", " ").replace("\n", " ").strip()
        desc = (row[idx_desc] or "").replace("|", " ").replace("\n", " ").strip()
        owner = (row[idx_owner] or ["?"])[0]
        lines.append(f"{rid}|{name}|{owner}|{desc}")

    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    print(f"OK: 同步 {len(lines)} 个待测试任务 -> {OUT}")
    for l in lines:
        print("  " + l[:90])
    return 0


if __name__ == "__main__":
    sys.exit(main())
