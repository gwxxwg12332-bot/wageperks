using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WagePerks;

// ============================================================
// 蛙娘寄卖数据层（2026-10-18 五项需求 §2 定案）
// 交互：面板按钮开 ConsignMode → 拖物品给蛙娘 → 吞噬瞬间写入记录（物品当场 QueueDestroy，
//   之后无法再读标签与价值）→ 次日 OnDayStart 结算（PostfixOnDayStart 挂点，与节点4 同点）。
// 数据（ns = "WageGirlSell"，一键一记录）：
//   Consign.<seq> = id|name|baseValue|flags|feedDay|pct
//   Consign.Seq   = 下一个序号（int）
// flags：F = 假货，S = 赃物；多标志以 + 连接；干净为空串。name 写入前把 | 替换为 ／。
// baseValue：吞噬瞬间已按 §3 折算（赃物=unitValue×60%，假货=unitValue×modifier%）——
//   结算不再重复折价（规格 §3 定价基准解耦 Q4）。
// pct：假货折算百分比（如 10 / 150），日报「按 {pct}% 结算」用；非假货为空。第 6 字段为最小扩展，解析兼容 5 字段旧记录。
// 结算：price = round(baseValue × (1 + margin))；margin 复用 FenceReturn.cs:181-192 同款
//   （好感加成 + 蛙娘卡等级加成 + 随机，参数 WageGirlFenceAffBonusPct/MarginBasePct/MarginLow/MarginHigh）
// 流拍：today - feedDay >= ConsignStaleDays → 按 baseValue × 80% 强制成交，日报标「折价」。
// ============================================================
internal static class WageGirlConsign
{
    private const string NS = "WageGirlSell"; // 与 WageGirlSellRules 同命名空间（旧档名单同层，互不冲突）
    private const string PREFIX = "Consign.";
    private const string SEQ_KEY = "Consign.Seq";

    private sealed class Record
    {
        internal string Suffix; // 序号键后缀（P2-10 审阅：精确删除用，防同 identifier 多条错位）
        internal string Id;
        internal string Name;
        internal long BaseValue;
        internal string Flags;   // "" / "S" / "F" / "S+F"（多标志以 + 连接）
        internal int FeedDay;
        internal string Pct;     // 假货折算百分比（空=非假货）
    }

    // ===================== 写入（吞噬瞬间） =====================
    /// <summary>写入一条寄卖记录，返回新序号；失败返回 -1。</summary>
    internal static int Add(string itemId, string name, long baseValue, string flags, int feedDay, string pct = "")
    {
        try
        {
            int seq = WageSaveStore.GetInt(NS, SEQ_KEY, 0);
            string safeName = (name ?? "").Replace("|", "／");
            WageSaveStore.SetString(NS, PREFIX + seq, itemId + "|" + safeName + "|" + baseValue + "|" + flags + "|" + feedDay + "|" + pct);
            // 10-18：SetString 在存档加载窗口内会静默丢弃写入（WageSaveStore:126 `if (!_loadingComplete) return;`），
            //   若不回读校验，会出现「物品被销毁但记录没落盘」的静默丢件。回读不到 → 返回 -1，调用方据此不销毁物品。
            string verify = WageSaveStore.GetString(NS, PREFIX + seq, null);
            if (verify == null)
            {
                Core.LogMsg("[蛙娘寄卖] 写入未落盘（存档仍在加载），已回退: " + itemId);
                return -1;
            }
            WageSaveStore.SetInt(NS, SEQ_KEY, seq + 1);
            Core.LogMsg("[蛙娘寄卖] 记录 #" + seq + ": " + itemId + " base=" + baseValue + " flags=[" + flags + "] day=" + feedDay + " pct=" + (pct ?? ""));
            return seq;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘寄卖] 写入异常: " + ex.Message); return -1; }
    }

    // ===================== 读取 =====================
    private static List<Record> LoadAll()
    {
        var list = new List<Record>();
        try
        {
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                if (!suffix.StartsWith(PREFIX)) continue;
                if (suffix == "Consign.Seq") continue;
                string val = WageSaveStore.GetString(NS, suffix, "");
                if (string.IsNullOrEmpty(val)) continue;
                var r = Parse(val);
                if (r != null) { r.Suffix = suffix; list.Add(r); } // P2-10：记录序号键后缀
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘寄卖] 读取异常: " + ex.Message); }
        return list;
    }

    private static Record Parse(string val)
    {
        try
        {
            var parts = val.Split('|');
            if (parts.Length < 5) return null;
            var r = new Record { Id = parts[0], Name = parts[1], BaseValue = 0, Flags = parts[3], FeedDay = 0, Pct = "" };
            try { r.BaseValue = long.Parse(parts[2]); } catch { }
            try { r.FeedDay = int.Parse(parts[4]); } catch { }
            if (parts.Length >= 6) r.Pct = parts[5];
            return r;
        }
        catch { return null; }
    }

    /// <summary>待售件数与估值（面板按钮文本用）。</summary>
    internal static void GetSummary(out int count, out long totalBase)
    {
        count = 0; totalBase = 0;
        try
        {
            foreach (var r in LoadAll()) { count++; totalBase += r.BaseValue; }
        }
        catch { }
    }

    /// <summary>今日已寄卖件数（每日上限校验）。</summary>
    internal static int CountFedToday(int today)
    {
        int n = 0;
        try { foreach (var r in LoadAll()) if (r.FeedDay == today) n++; } catch { }
        return n;
    }

    // ===================== 撤回（清空寄卖篮） =====================
    /// <summary>清空寄卖篮：按记录重建物品并退回玩家后库。返回成功件数。</summary>
    /// P1-5（审阅 20261018）：退回失败 → 保留记录（不删，防资产丢失）；P1-6：含 S/F 的记录禁止清空（无公开写口复原标签 → 防洗白）。
    internal static int ClearAll()
    {
        int ok = 0;
        try
        {
            foreach (var r in LoadAll())
            {
                // P1-6：赃物/假货记录不清空（重建会丢失 stolen/假货 feature → 寄卖篮变洗白通道）
                if (!string.IsNullOrEmpty(r.Flags))
                {
                    Core.LogMsg("[蛙娘寄卖] 清空跳过带标志记录（防洗白）: " + r.Name + " flags=[" + r.Flags + "]");
                    continue;
                }
                try
                {
                    if (string.IsNullOrEmpty(r.Id)) { RemoveKey(r); continue; }
                    GameItem it = WageAPI.WageItemFactory.Create(r.Id);
                    if (it == null) { Core.LogMsg("[蛙娘寄卖] 清空时重建失败: " + r.Id); continue; } // 重建失败 → 保留记录待下次（P1-5）
                    if (WageAPI.WageItemGrant.GrantToPlayerBackInv(it)) { ok++; RemoveKey(r); } // 仅成功后退还才删（P1-5）
                    else Core.LogMsg("[蛙娘寄卖] 退回背包失败，保留记录: " + r.Id);
                }
                catch (Exception ex) { Core.LogMsg("[蛙娘寄卖] 单条撤回异常: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘寄卖] 清空异常: " + ex.Message); }
        return ok;
    }

    /// <summary>清空时被跳过的带标志记录数（面板提示用：P1-6）。</summary>
    internal static int FlaggedCount()
    {
        int n = 0;
        try { foreach (var r in LoadAll()) if (!string.IsNullOrEmpty(r.Flags)) n++; } catch { }
        return n;
    }

    // P2-10：按序号键精确删除（记录含 Suffix；不再按内容匹配——同 identifier 多条不错位）
    private static void RemoveKey(Record r)
    {
        try
        {
            if (!string.IsNullOrEmpty(r.Suffix)) { WageSaveStore.RemoveKey(NS + "." + r.Suffix); return; }
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                if (!suffix.StartsWith(PREFIX) || suffix == "Consign.Seq") continue;
                string val = WageSaveStore.GetString(NS, suffix, "");
                if (val != null && val.StartsWith(r.Id + "|")) { WageSaveStore.RemoveKey(fullKey); return; }
            }
        }
        catch { }
    }

    // ===================== 次日结算（PostfixOnDayStart 调用） =====================
    /// <summary>
    /// 结算全部到期记录（feedDay < today）。返回结算件数与总现金。
    /// 流拍（today-feedDay >= ConsignStaleDays）按 8 折强制成交。
    /// 风险事件（§3）：S 5% 退货删记录 / F 10% 投诉好感-1——在结算内判定。
    /// 外出顺延：P3-18（审阅 20261018）RunDayEvents 已在 NeedsAffection.cs:298 提前 return → 本方法执行时必非外出，isOut 参数为死参数已删。
    /// </summary>
    internal static void SettleDay(int today)
    {
        try
        {
            var all = LoadAll();
            if (all.Count == 0) return;
            int staleDays = BuildConfig.ConsignStaleDays; if (staleDays < 1) staleDays = 1;
            int sold = 0; long total = 0;
            var details = new List<string>();
            foreach (var r in all)
            {
                if (r.FeedDay >= today) continue; // 当天新寄卖次日才结算
                // 10-18 蛙娘门路（§8.1-3）：门路货（flags 含 C/F0）不承担 S 退货 / F 投诉 两个风险事件
                bool connItem = r.Flags.Contains("C") || r.Flags.Contains("F0");
                // 风险事件（§3）：S 退货 / F 投诉 —— 仅非门路货判定
                if (!connItem && r.Flags.Contains("S"))
                {
                    int pct = BuildConfig.WageGirlConsignStolenReturnPct;
                    if (Core.Rng.Next(100) < pct)
                    {
                        RemoveKey(r);
                        NotifyLine(LangHelper.T("买家发现是赃物，退货了（寄卖「" + r.Name + "」）", "Buyer found it stolen, returned (" + r.Name + ")"));
                        continue;
                    }
                }
                if (!connItem && r.Flags.Contains("F"))
                {
                    int pct = BuildConfig.WageGirlConsignFakeComplaintPct;
                    if (Core.Rng.Next(100) < pct)
                    {
                        // 正常结算 + 好感 -1（P1-9：逐条加钱再删键，防中途异常货删钱不到）
                        long p = PriceFor(r, false);
                        ModCashN(p); sold++; total += p;
                        try { WageGirlSystem.SetAffectionExact(WageGirlSystem.GetAffectionExact() - 1); } catch { }
                        details.Add(LangHelper.T("「" + r.Name + "」卖 " + p + "（假货，买家投诉，好感 -1）", "\"" + r.Name + "\" sold " + p + " (fake, complaint, affection -1)"));
                        RemoveKey(r);
                        continue;
                    }
                }
                bool isStale = (today - r.FeedDay) >= staleDays;
                long price = PriceFor(r, isStale);
                // 10-18 蛙娘门路（§8.1-4）：门路货结算扣门路费（等级 FeePct），日报显示"门路费 -N"
                string feeNote = "";
                if (connItem)
                {
                    try
                    {
                        int lv = WageGirlConn.Level();
                        int feePct = WageGirlConn.FeePct(lv);
                        long fee = price * feePct / 100;
                        if (fee > 0) { price -= fee; if (price < 1) price = 1; feeNote = LangHelper.T("，门路费 -" + fee, ", conn fee -" + fee); }
                    }
                    catch (Exception exf) { Core.LogMsg("[蛙娘门路] 扣费异常: " + exf.Message); }
                }
                // §8.4 防回归断言：门路货/普通货结算路径均不得触碰 SecData（本方法无任何 SecData 调用）——
                //   寄卖链路隔离靠当前实现保证；若将来改成 PlayerStore.SellItemFromTable 等原版售出链路，
                //   免罪（§0 结论）会静默失效，玩家莫名其妙被罚款。此注释为哨兵，改动前必读。
                // P1-9：逐条「算价 → 加钱 → 删键」——异常中断只丢当前条，不整批丢
                ModCashN(price); sold++; total += price;
                // 10-18 蛙娘门路：结算后记好感/冷却/走漏/累计增益
                if (connItem)
                {
                    try
                    {
                        bool isC = r.Flags.Contains("C");
                        bool isF = r.Flags.Contains("F0") || r.Flags.Contains("F") || r.Flags.Contains("+F");
                        WageGirlConn.OnSettled(r.BaseValue, price, isC, isF);
                    }
                    catch (Exception exc) { Core.LogMsg("[蛙娘门路] 结算后处理异常: " + exc.Message); }
                }
                // P1-7：S+F 叠加时日报标「赃物·假货」与最终生效价一致
                string tag = isStale
                    ? LangHelper.T("（折价 8 折）", " (80% stale)")
                    : (connItem
                        ? LangHelper.T("（门路原价）", " (conn full price)")
                        : (r.Flags.Contains("S") && r.Flags.Contains("F")
                            ? LangHelper.T("（赃物·假货，按 " + (string.IsNullOrEmpty(r.Pct) ? "10" : r.Pct) + "% 再六折）", " (stolen+fake, " + (string.IsNullOrEmpty(r.Pct) ? "10" : r.Pct) + "% then 60%)")
                            : (r.Flags.Contains("S") ? LangHelper.T("（赃物，六折）", " (stolen, 60%)")
                                : (r.Flags.Contains("F") ? LangHelper.T("（假货，按 " + (string.IsNullOrEmpty(r.Pct) ? "10" : r.Pct) + "% 结算）", " (fake, " + (string.IsNullOrEmpty(r.Pct) ? "10" : r.Pct) + "%)")
                                    : ""))));
                details.Add(LangHelper.T("「" + r.Name + "」卖 " + price + tag + feeNote, "\"" + r.Name + "\" sold " + price + tag + feeNote));
                RemoveKey(r);
            }
            if (sold <= 0) return;
            NotifyLine(LangHelper.T("蛙娘把寄卖的 " + sold + " 件货卖掉了（+" + total + "）", "Wage Girl sold " + sold + " consigned items (+" + total + ")"));
            int show = Math.Min(3, details.Count);
            for (int i = 0; i < show; i++) NotifyLine(details[i]);
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘寄卖] 结算异常: " + ex.Message); }
    }

    // price = round(baseValue × (1 + margin))；baseValue 已含 §3 折算（赃物/假货），此处不再重复折价
    // margin 同款 FenceReturn.cs:181-192：好感加成(0~+20%) + 基准(+10% 蛙娘卡 Lv2 +5 / Lv3 +10) + 随机(-10%~+30%)
    private static long PriceFor(Record r, bool isStale)
    {
        try
        {
            double baseV = r.BaseValue;
            if (isStale) baseV = baseV * 0.8; // 流拍 8 折强制成交
            if (baseV <= 0) return 1;
            int aff = 0; try { aff = WageGirlSystem.GetAffection(); } catch { }
            float basePct = BuildConfig.WageGirlFenceMarginBasePct;
            try
            {
                int lv = B2ServiceStore.GetEffectLevel(WageGirlPerk.PerkId);
                if (lv >= 3) basePct += 10;
                else if (lv >= 2) basePct += 5;
            }
            catch { }
            float margin = (aff / (float)BuildConfig.WageGirlAffMax) * BuildConfig.WageGirlFenceAffBonusPct / 100f
                + basePct / 100f
                + Core.Rng.Next(BuildConfig.WageGirlFenceMarginLow, BuildConfig.WageGirlFenceMarginHigh + 1) / 100f;
            long price = (long)(baseV * (1f + margin));
            if (price < 1) price = 1;
            return price;
        }
        catch { return Math.Max(1, r.BaseValue); }
    }

    private static void NotifyLine(string s)
    {
        try { NotifyHelper.NightLogRaw(s); } catch { }
    }

    // 落账：与 State.cs:165-168 ModCashN 同款（PlayerStore.ModCash），独立实现因 State 版为 private
    private static void ModCashN(long n)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.ModCash((int)n); } catch { }
    }
}
