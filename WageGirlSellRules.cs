using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WagePerks;

// ============================================================
// 蛙娘代售规则引擎（售卖规矩 / Wage Girl Sell Rules）
// 设计稿 v2（2026-10-17 已审阅批准）：memory\06_设计稿\蛙娘代售控制机制设计_20261017.md
// 状态：P0 引擎（无 UI 也能用 MelonPreferences 文本兜底配置，先跑通逻辑）
// 裁决：①硬锁不可覆盖 → ②单件>名单（具体性优先）→ ③同级 Protect 胜 Allow（误卖不可逆）
// 键设计硬约束：名单键 = 内容规则键，禁 loc+idx 位置键（WageGirlSell.Protect.<n> / Allow.<n>，n 仅条目序号）
// 路线开关 UseTagAsMarker：甲=tag 随档直用 / 乙=名单兜底判定 + tag 仅 UI 层。
//   ⚠️ 拆包 AUDIT-P1-2-E §二-5 未回传前默认 false（乙：结论前单件标记不得作唯一保护手段）
// ============================================================
internal enum SellDecision { Sell, Protected, NotAllowed, HardLocked, Disabled, Error }

internal static class WageGirlSellRules
{
    private const string NS = "WageGirlSell";
    internal const string TAG_NO_SELL = "WAGE_NO_SELL";
    internal const string TAG_FORCE_SELL = "WAGE_FORCE_SELL";

    /// <summary>拆包结论开关（甲=true tag 随档直用；乙=false 名单兜底+tag 仅 UI 层）。默认乙。</summary>
    internal static bool UseTagAsMarker = false;

    // ===================== 配置（P0.5 审阅 A：四开关读 cfg 跨档=玩家策略偏好；名单走 WageSaveStore 不跨档） =====================
    internal static int Mode { get { try { return BuildConfig.WageGirlSellMode; } catch { return 0; } } }
    internal static int DailyQuota { get { try { return Math.Max(1, BuildConfig.WageGirlSellDailyQuota); } catch { return 1; } } }
    internal static bool AutoSellOn { get { try { return BuildConfig.WageGirlSellAutoSellOn; } catch { return true; } } }

    // ===================== 规则缓存（BeginEvaluate 一次性读入，避免逐件 EnumerateKeys O(n·m)） =====================
    private static HashSet<string> _protectRules = new HashSet<string>();
    private static HashSet<string> _allowRules = new HashSet<string>();
    private static bool _rulesLoaded = false;

    // ===================== 打烊结算入口：一次性把规则读进内存 =====================
    internal static void BeginEvaluate()
    {
        try
        {
            _protectRules.Clear();
            _allowRules.Clear();
            // 1) 存档名单（面板/操作写入）
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                string val = WageSaveStore.GetString(NS, suffix, "");
                if (string.IsNullOrEmpty(val)) continue;
                if (suffix.StartsWith("Protect.")) _protectRules.Add(val);
                else if (suffix.StartsWith("Allow.")) _allowRules.Add(val);
            }
            // 2) BuildConfig 文本兜底（高级配置，可直接手改 MelonPreferences.cfg）
            //    格式：分号分隔；每条 "P:<规则>" 或 "A:<规则>"，如 "P:tag:MODULE_TAG;A:val:0-50"
            string text = BuildConfig.WageGirlSellRulesText;
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var seg in text.Split(';'))
                {
                    string s = seg.Trim();
                    if (s.StartsWith("P:")) _protectRules.Add(s.Substring(2).Trim());
                    else if (s.StartsWith("A:")) _allowRules.Add(s.Substring(2).Trim());
                }
            }
            _rulesLoaded = true;
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] BeginEvaluate 异常: " + ex.Message); }
    }

    // ===================== 核心裁决 =====================
    /// <summary>返回是否可售 + 原因（原因用于日报/调试）。</summary>
    internal static SellDecision Resolve(GameItem item, out string reason)
    {
        reason = "";
        if (item == null) { reason = "空物品"; return SellDecision.Disabled; }
        try
        {
            // ① 硬锁层（玩家不可覆盖）
            if (!AutoSellOn) { reason = "代售总开关已关"; return SellDecision.Disabled; }
            if (Il2Cpp.ContrabandHelper.GetContrabandLevel(item) > 0) { reason = "违禁品"; return SellDecision.HardLocked; }
            if (item.IsTag("ORDER_TICKET_TAG")) { reason = "点货签"; return SellDecision.HardLocked; }
            if (item.IsTag("wage_bro_card") || item.IsTag("destiny_dice_tag")) { reason = "mod 特殊物"; return SellDecision.HardLocked; }
            int uv = 0; try { uv = (int)item.unitValue; } catch { }
            if (uv <= 0) { reason = "无价值"; return SellDecision.HardLocked; }

            // ② 单件标记层（最具体）
            if (UseTagAsMarker) // 路线甲：tag 直接作为判定
            {
                if (item.IsTag(TAG_NO_SELL)) { reason = "单件标记·免售"; return SellDecision.Protected; }
                if (item.IsTag(TAG_FORCE_SELL)) { reason = "单件标记·必售"; return SellDecision.Sell; }
            }
            // 路线乙：单件标记实际由名单承载（ToggleProtect 同时写 Protect.id:<identifier>），此处②③同源，行为等价

            // ③ 名单层（判定权威层；同级 Protect 胜 Allow —— 先查 Protect）
            if (_rulesLoaded && MatchAny(_protectRules, item)) { reason = "免售名单"; return SellDecision.Protected; }
            if (_rulesLoaded && MatchAny(_allowRules, item)) { reason = "可售名单"; return SellDecision.Sell; }

            // ④ 模式层（兜底）
            if (Mode == 1) { reason = "白名单未命中"; return SellDecision.NotAllowed; } // 白名单：只卖命中 Allow 的
            // 黑名单（默认）：未命中 Protect 即可售
            return SellDecision.Sell;
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] Resolve 异常: " + ex); reason = "裁决异常:" + ex.Message; return SellDecision.Error; }
    }

    // ===================== 规则匹配 =====================
    private static bool MatchAny(HashSet<string> rules, GameItem item)
    {
        if (rules == null || rules.Count == 0) return false;
        foreach (var rule in rules)
        {
            if (string.IsNullOrEmpty(rule)) continue;
            try
            {
                if (rule == "*") return true;
                if (rule.StartsWith("id:"))
                {
                    string id = rule.Substring(3).Trim().ToLowerInvariant();
                    string itemId = (item.identifier ?? "").ToLowerInvariant();
                    if (itemId == id) return true;
                    continue;
                }
                if (rule.StartsWith("tag:"))
                {
                    string t = rule.Substring(4).Trim();
                    if (item.IsTag(t)) return true;
                    continue;
                }
                if (rule.StartsWith("cat:"))
                {
                    string cat = rule.Substring(4).Trim().ToLowerInvariant();
                    if (cat == "contraband" && Il2Cpp.ContrabandHelper.GetContrabandLevel(item) > 0) return true;
                    if (cat == "food" && ItemHelper.IsFood(item)) return true;
                    if (cat == "drink" && ItemHelper.IsDrink(item)) return true;
                    if (cat == "weapon" && IsWeaponOrTool(item)) return true;
                    continue;
                }
                if (rule.StartsWith("val:"))
                {
                    // val:<min>-<max>，如 val:0-50
                    string range = rule.Substring(4).Trim();
                    int dash = range.IndexOf('-');
                    if (dash > 0 && int.TryParse(range.Substring(0, dash), out int lo) && int.TryParse(range.Substring(dash + 1), out int hi))
                    {
                        int v = 0; try { v = (int)item.unitValue; } catch { }
                        if (v >= lo && v <= hi) return true;
                    }
                    continue;
                }
            }
            catch { }
        }
        return false;
    }

    // 武器/工具类别判定（id 关键词匹配，照 WageGirlSystem.StealAI_Fence.ItemHelpers.IsWeaponOrTool 先例）
    // ⚠️ cat:weapon 为 id 关键词硬编码判定（关键词表见 :165-172），漏判与误判均存在，非权威类别判定
    private static bool IsWeaponOrTool(GameItem item)
    {
        try
        {
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(id)) return false;
            return id.Contains("weapon") || id.Contains("tool") || id.Contains("knife") || id.Contains("gun")
                || id.Contains("pistol") || id.Contains("shotgun") || id.Contains("smg") || id.Contains("rifle")
                || id.Contains("tazer") || id.Contains("stun") || id.Contains("grenade") || id.Contains("baton")
                || id.Contains("machete") || id.Contains("sword") || id.Contains("axe") || id.Contains("c4")
                || id.Contains("screwdriver") || id.Contains("welder") || id.Contains("flashlight") || id.Contains("scanner")
                || id.Contains("hammer") || id.Contains("wrench") || id.Contains("surgery") || id.Contains("combat");
        }
        catch { return false; }
    }

    // ===================== 单件标记（路线乙时：同时写 Protect.id:<identifier> 名单） =====================
    /// <summary>切 WAGE_NO_SELL；返回切换后是否处于保护态。P0.5 审阅 D：与必售标记互斥（Toggle 时清理对方名单）。</summary>
    internal static bool ToggleProtect(GameItem item)
    {
        if (item == null) return false;
        try
        {
            bool now = item.IsTag(TAG_NO_SELL);
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (now)
            {
                WageAPI.WageTag.Disable(item, TAG_NO_SELL);
                if (!UseTagAsMarker && !string.IsNullOrEmpty(id)) RemoveRule(true, "id:" + id);
                return false;
            }
            else
            {
                WageAPI.WageTag.SetInt(item, TAG_NO_SELL, 1);
                if (!UseTagAsMarker && !string.IsNullOrEmpty(id))
                {
                    RemoveRule(false, "id:" + id); // 互斥：清掉同 id 的必售名单（无该键时 RemoveRule 为空操作，见 :250-269）
                    AddRule(true, "id:" + id);
                }
                return true;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] ToggleProtect 异常: " + ex.Message); return false; }
    }

    /// <summary>切 WAGE_FORCE_SELL；返回切换后是否处于必售态。P0.5 审阅 D：与免售标记互斥（Toggle 时清理对方名单）。</summary>
    internal static bool ToggleForceSell(GameItem item)
    {
        if (item == null) return false;
        try
        {
            bool now = item.IsTag(TAG_FORCE_SELL);
            string id = (item.identifier ?? "").ToLowerInvariant();
            if (now)
            {
                WageAPI.WageTag.Disable(item, TAG_FORCE_SELL);
                if (!UseTagAsMarker && !string.IsNullOrEmpty(id)) RemoveRule(false, "id:" + id);
                return false;
            }
            else
            {
                WageAPI.WageTag.SetInt(item, TAG_FORCE_SELL, 1);
                if (!UseTagAsMarker && !string.IsNullOrEmpty(id))
                {
                    RemoveRule(true, "id:" + id); // 互斥：清掉同 id 的免售名单（无该键时 RemoveRule 为空操作，见 :250-269）
                    AddRule(false, "id:" + id);
                }
                return true;
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] ToggleForceSell 异常: " + ex.Message); return false; }
    }

    // ===================== 名单管理 =====================
    /// <summary>新增规则（protect=true 免售 / false 可售）。规则串语法见设计稿 §2 层 B。</summary>
    internal static void AddRule(bool protect, string rule)
    {
        try
        {
            if (string.IsNullOrEmpty(rule)) return;
            string prefix = protect ? "Protect." : "Allow.";
            int n = NextFreeIndex(prefix);
            WageSaveStore.SetString(NS, prefix + n, rule);
            Core.LogMsg("[蛙娘代售] 添加" + (protect ? "免售" : "可售") + "规则 " + prefix + n + " = " + rule);
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] AddRule 异常: " + ex.Message); }
    }

    /// <summary>删除匹配规则（按规则串内容匹配，删全部命中项）。</summary>
    internal static void RemoveRule(bool protect, string rule)
    {
        try
        {
            if (string.IsNullOrEmpty(rule)) return;
            string prefix = protect ? "Protect." : "Allow.";
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                if (suffix.StartsWith(prefix))
                {
                    string val = WageSaveStore.GetString(NS, suffix, "");
                    if (val == rule) WageSaveStore.RemoveKey(fullKey);
                }
            }
            Core.LogMsg("[蛙娘代售] 删除" + (protect ? "免售" : "可售") + "规则 " + rule);
        }
        catch (System.Exception ex) { Core.LogMsg("[蛙娘代售] RemoveRule 异常: " + ex.Message); }
    }

    /// <summary>列出规则（protect=true 免售 / false 可售）。</summary>
    internal static List<string> ListRules(bool protect)
    {
        var result = new List<string>();
        try
        {
            string prefix = protect ? "Protect." : "Allow.";
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                if (suffix.StartsWith(prefix))
                {
                    string val = WageSaveStore.GetString(NS, suffix, "");
                    if (!string.IsNullOrEmpty(val)) result.Add(val);
                }
            }
        }
        catch { }
        return result;
    }

    // ===================== 内部 =====================
    // 找下一个空闲条目序号（仅作条目序号，与物品库存位置完全无关——键设计硬约束）
    private static int NextFreeIndex(string prefix)
    {
        int max = -1;
        try
        {
            foreach (var fullKey in WageSaveStore.EnumerateKeys(NS))
            {
                if (fullKey == null) continue;
                string suffix = fullKey.StartsWith(NS + ".") ? fullKey.Substring(NS.Length + 1) : fullKey;
                if (suffix.StartsWith(prefix))
                {
                    string numStr = suffix.Substring(prefix.Length);
                    if (int.TryParse(numStr, out int n) && n > max) max = n;
                }
            }
        }
        catch { }
        return max + 1;
    }
}
