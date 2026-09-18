using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static class WageGirlSystem
{
    public const string ENTITY_ID = "wage_girl";
    public const string ICON_ATLAS = "custom_atlas";
    public const string ICON = "wage_girl_icon";
    public const string TAG = "WAGE_GIRL_TAG";
    private const string NS = "wage_girl";
    private const int STAT_MAX = 100;
    private const int STAT_INIT = 60;
    private const int DAILY_DECAY = 3;   // 每日六维衰减（未照顾）
    private const int AFF_MAX = 100;
    private const int AFF_DAILY_DROP = 1; // 好感每日回落（不照顾）

    private const string K_SAT = "sat", K_TH = "th", K_HEALTH = "health", K_MOOD = "mood", K_CLEAN = "clean", K_SLEEP = "sleep";
    private const string K_AFF = "affection", K_LAST_STEAL = "lastStealDay", K_LEAVE = "leaveDay", K_STARVE = "starveStreak";
    private const string K_EXIST = "exists";
    private const string K_STEAL_AMT = "lastStealAmount"; // 上次偷钱额（回归带物比例用）
    private const int STEAL_INTERVAL = 7; // 偷钱周期（天）

    private static Sprite _sprite;

    static WageGirlSystem()
    {
        try { _sprite = SpriteFromPixels(WageGirlIconsArt.Pixels(), 32, 48); } // 09-22 外部生成 32×48（2×3 格）
        catch (Exception ex) { Core.LogMsg("[蛙娘] 图标加载异常: " + ex.Message); }
    }

    // ===================== 状态读写（全局 NS 随档） =====================
    internal static int GetStat(string k) { try { return PerkStatePersistence.GetInt(NS, k, STAT_INIT); } catch { return STAT_INIT; } }
    internal static void SetStat(string k, int v)
    {
        try { PerkStatePersistence.SetInt(NS, k, Math.Max(0, Math.Min(STAT_MAX, v))); } catch { }
    }
    internal static int GetAffection() { try { return PerkStatePersistence.GetInt(NS, K_AFF, 0); } catch { return 0; } }
    internal static void SetAffection(int v) { try { PerkStatePersistence.SetInt(NS, K_AFF, Math.Max(0, Math.Min(AFF_MAX, v))); } catch { } }
    internal static bool Exists() { try { return PerkStatePersistence.GetInt(NS, K_EXIST, 0) == 1; } catch { return false; } }
    internal static void SetExists(bool v) { try { PerkStatePersistence.SetInt(NS, K_EXIST, v ? 1 : 0); } catch { } }

    // ===================== 实体注册（Patches.PostfixInitDirectory 调） =====================
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            ((Directory<GameItem>)(object)dir).Add(ENTITY_ID, (Il2CppSystem.Func<GameItem>)(() => CreateWageGirl()));
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 注册失败: " + ex.Message); }
    }

    private static GameItem CreateWageGirl()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it);
            it.EnableTag(TAG);
            it.SetName(LangHelper.T("蛙娘", "Wage Girl"));
            SetField(it, "_identifier_k__BackingField", ENTITY_ID);
            SetField(it, "_identifierName_k__BackingField", "TYPE-STRING_" + ENTITY_ID);
            it.shortDescription = LangHelper.T("蛙娘——蛙哥（Wage）留下的仿生女仆实体：会自己吃喝、干活，心情不好还会偷拿你的钱和货。照顾好她，她会帮你叫客、抬价、销赃。双击打开状态面板。", "Wage Girl - a biomimetic maid entity left by Wage: she eats and works on her own, and when moody she steals your money and goods. Take care of her and she'll call customers, boost prices and fence for you. Double-click to open her status panel.");
            it.longDescription = it.shortDescription;
            it.unitValue = 3000; it.unitBaseValue = 3000;
            // 2×3 占地（09-21 用户拍板）
            try { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 3); it.SetShape(gsb.Build()); } catch { }
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 创建失败: " + ex.Message); return null; }
    }

    private static void ApplyIcon(GameItem it)
    {
        try { it.SetSpriteAndShape(ICON_ATLAS, ICON); }
        catch { try { it.SetSpriteAndShape("custom_atlas", "custom_storage_box_sprite"); } catch { } }
    }

    // 像素数组 → Texture2D → Sprite（照 GuMachineSystem.SpriteFromPixels；ppu=100）
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }
        catch { return null; }
    }

    // 拦截 RenderHandler.LoadFromAtlas：custom_atlas + 蛙娘图标 → 自定义 sprite
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try { if (atlasPath == ICON_ATLAS && name == ICON && _sprite != null) { __result = _sprite; return false; } } catch { }
        return true;
    }

    // 反射设字段（照 GuMachineSystem.SetField）
    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch { }
    }

    // ===================== 常驻面板（照鲁滨逊 RefreshStatusPanel） =====================
    internal static void ShowPanel()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("wage_girl_panel")) mgr.CloseWindow("wage_girl_panel");
            var b = mgr.CreateWindow("wage_girl_panel", LangHelper.T("蛙娘 · 状态", "Wage Girl · Status"), "overlay");
            if (b == null) return;
            b.SetSize(300, 460).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow("wage_girl_panel");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch { }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + GetStat(K_SAT) + "/100", "wg_sat_l");
            b.AddProgressBar(GetStat(K_SAT) / 100f, "wg_sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + GetStat(K_TH) + "/100", "wg_th_l");
            b.AddProgressBar(GetStat(K_TH) / 100f, "wg_th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + GetStat(K_HEALTH) + "/100", "wg_h_l");
            b.AddProgressBar(GetStat(K_HEALTH) / 100f, "wg_h");
            b.AddLabel(LangHelper.T("心情 ", "Mood ") + GetStat(K_MOOD) + "/100", "wg_m_l");
            b.AddProgressBar(GetStat(K_MOOD) / 100f, "wg_m");
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + GetStat(K_CLEAN) + "/100", "wg_c_l");
            b.AddProgressBar(GetStat(K_CLEAN) / 100f, "wg_c");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + GetStat(K_SLEEP) + "/100", "wg_s_l");
            b.AddProgressBar(GetStat(K_SLEEP) / 100f, "wg_s");
            b.AddLabel(LangHelper.T("（喂食/照顾提升状态与好感——后续开放）", "(Feed & care to raise stats & affection - coming soon)"), "wg_note");
            // 外出状态（阶段 5：偷钱消失期）
            try
            {
                int leave = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
                if (leave > 0 && CurrentDay() <= leave)
                    b.AddLabel(LangHelper.T("（外出中——躲债去了，明天回）", "(Out - dodging debts, back tomorrow)"), "wg_leave");
            }
            catch { }
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 面板异常: " + ex.Message); }
    }



    // ===================== 阶段 3：在场增益-议价 +50（GetDealMakerBonus Postfix——拆包 09-21 二次实锤：GetBargainSuccessChance 只被 tooltip 调用=纯显示；GetDealMakerBonus 是显示+实际判定共用唯一加成项，7 调用点覆盖 OfferMarkup/OfferBuyingMarkup/Blackmail） =====================
    public static void PostfixGetDealMakerBonus(ref int __result)
    {
        try
        {
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            __result += 50;
            if (__result > 100) __result = 100;
        }
        catch { }
    }

    // ===================== 阶段 3：在场增益-预算 ×4（ApplyBudgetModifier Postfix——照鲁滨逊预算联动先例） =====================
    public static void PostfixApplyBudgetModifier(StoreClient __instance)
    {
        try
        {
            if (__instance == null) return;
            if (!Exists()) return; // 蛙娘未出现 → 无增益
            if (__instance.identifier == ENTITY_ID) return; // 蛙娘自己不是客户时不受益
            // 预算 ×4（+300%，话术 v9：OverrideBudget(GetBudget()*4)）
            int budget = __instance.GetBudget();
            long newBudget = (long)budget * 4;
            if (newBudget > 2147483646L) newBudget = 2147483646L;
            __instance.OverrideBudget((int)newBudget);
            __instance.clientCash = (int)newBudget;
            __instance.useClientBudget = true;
        }
        catch { }
    }

    // ===================== 阶段 2：拖放喂食/喝水/照顾（照命运骰子拖放吸收链） =====================
    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (IsGirl(targetItem) && CanFeed(__instance)) { __result = true; return false; } // hover 可拖
        }
        catch { }
        return true;
    }
    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)
    {
        return PrefixMayTarget(__instance, targetItem, ref __result);
    }
    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)
    {
        try
        {
            if (__instance == null || targetItem == null) return true;
            if (!IsGirl(targetItem)) return true;
            if (!IsDragRelease()) return true;
            if (TryFeed(__instance, targetItem)) return false; // 喂食成功：拦截原生放入
        }
        catch { }
        return true;
    }
    private static bool IsDragRelease()
    {
        try { var h = Il2Cpp.ItemMouseDragHandler.current; return h != null && h.IsDraggingItem; } catch { return false; }
    }
    private static bool IsGirl(GameItem it)
    {
        try { return it != null && it.identifier == ENTITY_ID; } catch { return false; }
    }
    private static bool CanFeed(GameItem item)
    {
        try { return RobinCrusoePerk.IsFood(item) || RobinCrusoePerk.IsDrink(item) || RobinCrusoePerk.IsDailyNeed(item); } catch { return false; }
    }
    private static bool TryFeed(GameItem item, GameItem girl)
    {
        try
        {
            if (item == null) return false;
            if (Patches.CurrentUITradeMode != 0) return false;
            int gain = 0; int aff = 1; string msg = "";
            if (RobinCrusoePerk.IsDailyNeed(item)) { gain = 20; aff = 2; msg = LangHelper.T("蛙娘洗得干干净净、心情大好！清洁 +20 心情 +10（照顾）", "Wage Girl cleaned up & cheered up! Cleanliness +20 Mood +10 (care)"); SetStat(K_CLEAN, GetStat(K_CLEAN) + gain); SetStat(K_MOOD, GetStat(K_MOOD) + 10); }
            else if (RobinCrusoePerk.IsFood(item)) { gain = 25; aff = 1; msg = LangHelper.T("蛙娘吃饱了！饱食 +25", "Wage Girl ate! Satiety +25"); SetStat(K_SAT, GetStat(K_SAT) + gain); }
            else if (RobinCrusoePerk.IsDrink(item)) { gain = 25; aff = 1; msg = LangHelper.T("蛙娘喝饱了！口渴 +25", "Wage Girl drank! Thirst +25"); SetStat(K_TH, GetStat(K_TH) + gain); }
            else return false;
            SetAffection(GetAffection() + aff);
            // 消耗源物品（吃掉）：Destroy → Expel 兜底（照命运骰子吸收）
            try { item.Destroy(); } catch { try { item.parentInventory?.Expel(item); } catch { } }
            try { StoreUIManager.Instance.Notify(msg, "green"); } catch { }
            try { if (Il2Cpp.CustomUIManager.Instance != null && Il2Cpp.CustomUIManager.Instance.IsOpen("wage_girl_panel")) ShowPanel(); } catch { }
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 喂食异常: " + ex.Message); return false; }
    }

    // ===================== 双击（全局——不依赖任何特性） =====================
    public static void PostfixDoubleClickAction(GameItem newItem, Vector2 mousePosition)
    {
        try
        {
            if (newItem == null) return;
            if (newItem.identifier != ENTITY_ID) return;
            if (Patches.CurrentUITradeMode != 0) return;
            ShowPanel();
        }
        catch { }
    }

    // ===================== 每日结算（OnDayStart Postfix——同养蛊机挂点） =====================
    public static void PostfixOnDayStart()
    {
        try
        {
            // 全局发放：存档里未出现过 → 发 1 个蛙娘实体到背包（玩家自己摆出来）
            if (!Exists())
            {
                TryGiveToBackpack();
                SetExists(true);
            }
            // 六维每日衰减（睡眠除外——仿生女仆夜间自然恢复睡眠）
            foreach (var k in new[] { K_SAT, K_TH, K_HEALTH, K_MOOD, K_CLEAN })
                SetStat(k, GetStat(k) - DAILY_DECAY);
            // 睡眠自然增长（过夜充电/睡觉恢复）
            SetStat(K_SLEEP, GetStat(K_SLEEP) + 15);
            // 好感每日回落（不照顾）
            SetAffection(GetAffection() - AFF_DAILY_DROP);
            // 阶段 5：回归 / 自主偷拿 / 偷钱循环
            RunDayEvents();
        }
        catch { }
    }

    // ===================== 阶段 5：偷钱循环 + 自主偷拿 + 回归（话术 v9） =====================
    private static int CurrentDay()
    {
        try { return Il2Cpp.StoreStation.GetDayCounter(); } catch { return 1; }
    }

    // 夜报三件套（09-17 统一规范：原生 AddNightLog + mod 队列 + 弹窗）
    private static void ReportLine(string line)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#E2B93B"); } catch { }
        try { Core.AddNightReportLine(line); } catch { }
        try { Il2Cpp.StoreUIManager.Instance.Notify(line); } catch { }
    }

    private static void ModCashN(int n)
    {
        try { var ps = Il2Cpp.PlayerStore.Instance; if (ps != null) ps.ModCash(n); } catch { }
    }

    // 每日事件：回归 →（消失期不活动）→ 初次偷拿 → 日常偷拿 → 偷钱循环
    private static void RunDayEvents()
    {
        try
        {
            int day = CurrentDay();
            int leaveDay = PerkStatePersistence.GetInt(NS, K_LEAVE, 0);
            // 1) 回归：消失期已过 → 带物品回来 + 实体重新发放
            if (leaveDay > 0 && day > leaveDay)
            {
                PerkStatePersistence.SetInt(NS, K_LEAVE, 0);
                int amt = PerkStatePersistence.GetInt(NS, K_STEAL_AMT, 0);
                if (amt > 0) { GiveBackItem(amt); PerkStatePersistence.SetInt(NS, K_STEAL_AMT, 0); }
                else ReportLine(LangHelper.T("蛙娘回来了", "Wage Girl is back"));
                TryGiveToBackpack(); // 实体重新发放（消失期实体已移除）
            }
            // 2) 消失期：不偷拿不偷钱
            if (leaveDay > 0 && day <= leaveDay) return;
            int lastSteal = PerkStatePersistence.GetInt(NS, K_LAST_STEAL, 0);
            // 3) 初次偷拿（lastSteal==0 → 初次偷 1 件 + 50 钱，然后设 today）
            if (lastSteal <= 0)
            {
                int stolen = StealItems("random", 1, "highest");
                if (stolen > 0) ModCashN(-50);
                ReportLine(LangHelper.T("蛙娘初次见面就偷偷拿走了你的东西，还顺走了 50 块钱……", "On first meeting Wage Girl swiped something and pocketed 50 credits..."));
                PerkStatePersistence.SetInt(NS, K_LAST_STEAL, day);
                return;
            }
            // 4) 日常自主偷拿（状态触发）
            TrySnatch();
            // 5) 偷钱循环（≥7 天）
            if (day - lastSteal >= STEAL_INTERVAL)
            {
                int aff = GetAffection();
                int steal = 50 + (int)((aff / 100f) * 450f); // 好感 0→50、100→500
                ModCashN(-steal);
                PerkStatePersistence.SetInt(NS, K_STEAL_AMT, steal);
                PerkStatePersistence.SetInt(NS, K_LEAVE, day); // 当天消失（明天回归）
                PerkStatePersistence.SetInt(NS, K_LAST_STEAL, day);
                RemoveGirlFromScene(); // 实体真消失（回归时重发）
                ReportLine(LangHelper.T("蛙娘偷走了 " + steal + " 块钱，出门躲债去了（明天回来）", "Wage Girl stole " + steal + " credits and went out (back tomorrow)"));
            }
        }
        catch { }
    }

    // 日常自主偷拿：饥饿/口渴/心情差自拿对应类恢复；心情好随机顺走（件数/价值按好感）
    private static void TrySnatch()
    {
        try
        {
            int sat = GetStat(K_SAT), th = GetStat(K_TH), mood = GetStat(K_MOOD);
            string mode = null; string msg = null;
            if (sat < 30) { mode = "food"; msg = "蛙娘饿坏了，偷吃了你的食物"; }
            else if (th < 30) { mode = "drink"; msg = "蛙娘渴坏了，偷喝了你的饮品"; }
            else if (mood < 30) { mode = "care"; msg = "蛙娘心情很差，拿走了你的日用品"; }
            else if (mood >= 60) { mode = "random"; msg = "蛙娘心情不错，顺走了你点小东西"; }
            else return;
            int aff = GetAffection();
            int count = aff < 30 ? 1 : (aff < 70 ? 2 : 3);
            string valueMode = aff < 30 ? "highest" : (aff < 70 ? "random" : "low");
            int stolen = StealItems(mode, count, valueMode);
            if (stolen > 0)
            {
                if (mode == "food") SetStat(K_SAT, GetStat(K_SAT) + 30);
                else if (mode == "drink") SetStat(K_TH, GetStat(K_TH) + 30);
                else if (mode == "care") { SetStat(K_MOOD, GetStat(K_MOOD) + 20); SetStat(K_CLEAN, GetStat(K_CLEAN) + 10); }
                ReportLine(LangHelper.T(msg + "（" + stolen + " 件）", msg + " (" + stolen + " items)"));
            }
        }
        catch { }
    }

    // 从店里找目标偷拿：mode 限定类别；count 件数；valueMode highest/random/low
    private static int StealItems(string mode, int count, string valueMode)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return 0;
            var invs = new GameInventory[] {
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement,
                (GameInventory)em.invElement
            };
            var candidates = new System.Collections.Generic.List<GameItem>();
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null) continue;
                    if (it.identifier == ENTITY_ID) continue;
                    if (it.IsTag("STANDARD_MACHINE_TAG") || it.IsTag("CONTAINER_TAG")) continue;
                    if (mode == "food" && !RobinCrusoePerk.IsFood(it)) continue;
                    if (mode == "drink" && !RobinCrusoePerk.IsDrink(it)) continue;
                    if (mode == "care" && !RobinCrusoePerk.IsDailyNeed(it)) continue;
                    candidates.Add(it);
                }
            }
            if (candidates.Count == 0) return 0;
            var picked = new System.Collections.Generic.List<GameItem>();
            if (valueMode == "highest")
            {
                GameItem best = candidates[0];
                foreach (var c in candidates) if (c.unitValue > best.unitValue) best = c;
                picked.Add(best);
            }
            else if (valueMode == "low")
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                pool.Sort((a, b) => a.unitValue.CompareTo(b.unitValue));
                for (int i = 0; i < Math.Min(count, pool.Count); i++) picked.Add(pool[i]);
            }
            else
            {
                var pool = new System.Collections.Generic.List<GameItem>(candidates);
                for (int i = 0; i < Math.Min(count, pool.Count); i++)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    picked.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }
            int stolen = 0;
            foreach (var p in picked)
            {
                try { p.Destroy(); stolen++; } catch { try { if (p.parentInventory != null) { p.parentInventory.Expel(p); stolen++; } } catch { } }
            }
            return stolen;
        }
        catch { return 0; }
    }

    // 回归带物：按偷钱额 × 好感比例预算，随机生成物品（单件/累计价值 ≤ 预算）放入柜台
    private static void GiveBackItem(int stealAmt)
    {
        try
        {
            int aff = GetAffection();
            float ratio = 0.2f + (aff / 100f) * 0.6f; // 好感 0→20%、100→80%
            long budget = Math.Max(1, (int)(stealAmt * ratio));
            EmporiumEntry em = EmporiumEntry.Instance;
            var ids = DirectoryMaster.GetIdentifierList<GameItem>(null);
            if (ids == null || ids.Count == 0) { ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)")); return; }
            var pool = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.IsNullOrEmpty(ids[i]) || ids[i] == ENTITY_ID) continue;
                if (System.Array.IndexOf(Core.ExcludedItemIds, ids[i]) >= 0) continue; // 全局黑名单（rare_electronic 等）
                pool.Add(ids[i]);
            }
            long spent = 0;
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 2 && pool.Count > 0 && spent < budget; i++)
            {
                GameItem it = null; string id = null; int tries = 0;
                while (tries < 12 && pool.Count > 0)
                {
                    int idx = Core.Rng.Next(pool.Count);
                    id = pool[idx];
                    try
                    {
                        it = DirectoryMaster.Item(id, true);
                        if (it == null) { pool.RemoveAt(idx); tries++; continue; }
                        if (it.IsTag("STANDARD_MACHINE_TAG") || it.IsTag("CONTAINER_TAG")) { pool.RemoveAt(idx); tries++; continue; }
                        long v = it.unitValue;
                        if (v <= 0 || v > budget - spent) { pool.RemoveAt(idx); tries++; continue; } // 超预算/无价值 → 换
                        break;
                    }
                    catch { pool.RemoveAt(idx); tries++; }
                }
                if (it == null) continue;
                spent += it.unitValue;
                try
                {
                    if (em != null && em.frontInvinvElement != null)
                    {
                        var inv = (GameInventory)em.frontInvinvElement;
                        var slot = em.frontInvinvElement.TryFindOneValidInventorySlot(it, false);
                        if (slot != null) { try { slot.TryAcceptOnce(); } catch { } }
                        else inv.UncheckedAccept(it);
                        names.Add(id);
                    }
                }
                catch { }
            }
            if (names.Count > 0) ReportLine(LangHelper.T("蛙娘回来了，带了点东西回来：" + string.Join("、", names), "Wage Girl is back with: " + string.Join(", ", names)));
            else ReportLine(LangHelper.T("蛙娘回来了（没带什么值钱的东西）", "Wage Girl is back (empty-handed)"));
        }
        catch { }
    }

    // 偷钱消失：从场景主要网格移除蛙娘实体（回归时 TryGiveToBackpack 重发）
    private static void RemoveGirlFromScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return;
            var invs = new GameInventory[] {
                (GameInventory)em.invElement,
                (GameInventory)em.backInvinvElement,
                (GameInventory)em.frontInvinvElement,
                (GameInventory)em.showcaseElement
            };
            foreach (var inv in invs)
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = inv.childItems.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var it = inv.childItems[i];
                        if (it == null) continue;
                        if (it.identifier == ENTITY_ID)
                        {
                            try { it.Destroy(); } catch { }
                            try { inv.childItems.RemoveAt(i); } catch { }
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static void TryGiveToBackpack()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            GameItem item = DirectoryMaster.Item(ENTITY_ID, true);
            if (item == null) return;
            // 照 GiveToBackpack 先例：TryFindOneValidInventorySlot → TryAcceptOnce（防同格重叠）；失败 UncheckedAccept 兜底
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return; } catch { } }
            inv.UncheckedAccept(item);
            Core.LogMsg("[蛙娘] 已发放实体到背包（全局常驻）");
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 发放失败: " + ex.Message); }
    }
}

// ===================== 蛙娘占位像素图标（32×32 透明底，绿色蛙身+眼睛+腮红） =====================
internal static class WageGirlIcons
{
    public static Color[] Pixels()
    {
        const int S = 32;
        var px = new Color[S * S];
        // 透明底
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
        Color body = new Color(0.36f, 0.72f, 0.42f, 1f);   // 蛙绿
        Color dark = new Color(0.12f, 0.34f, 0.20f, 1f);   // 深描边
        Color eyeW = new Color(0.95f, 0.98f, 0.95f, 1f);   // 眼白
        Color eyeB = new Color(0.10f, 0.10f, 0.12f, 1f);   // 瞳孔
        Color blush = new Color(0.95f, 0.55f, 0.60f, 1f);  // 腮红

        void Set(int x, int y, Color c) { if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = c; }
        // 身体（圆头 + 方身）：中心 16, 行 6-25
        for (int y = 5; y <= 25; y++)
        {
            int half = (y < 14) ? 9 : 8; // 头圆身方
            int x0 = 16 - half, x1 = 16 + half;
            if (y >= 14) { x0 = 7; x1 = 24; }
            for (int x = x0; x <= x1; x++) Set(x, y, body);
        }
        // 深色描边：左右各 1 列 + 底边
        for (int y = 5; y <= 25; y++) { Set(6, y, dark); Set(25, y, dark); }
        for (int x = 6; x <= 25; x++) { Set(x, 25, dark); Set(x, 5, dark); }
        // 眼睛（两枚 3×3 白 + 1px 瞳孔）
        for (int y = 10; y <= 12; y++) { for (int x = 11; x <= 13; x++) Set(x, y, eyeW); for (int x = 19; x <= 21; x++) Set(x, y, eyeW); }
        Set(12, 11, eyeB); Set(20, 11, eyeB);
        // 腮红（两枚 2×2）
        Set(9, 17, blush); Set(10, 17, blush); Set(9, 18, blush); Set(10, 18, blush);
        Set(21, 17, blush); Set(22, 17, blush); Set(21, 18, blush); Set(22, 18, blush);
        // 嘴（微笑 3px）
        Set(15, 20, dark); Set(16, 21, dark); Set(17, 20, dark);
        return px;
    }
}
