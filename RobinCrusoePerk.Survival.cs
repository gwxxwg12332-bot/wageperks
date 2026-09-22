using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【空间站鲁滨逊】职业生存系统（startType=14）
// v4.2（2026-09-09，v4.2 终稿重构）：
//   - 饱食节点制：calorieBalance（卡，1单位=2200卡）替代 hunger 层数制，与原生 hunger(0-1000) 完全解耦
//   - 精神 5 档（昂扬/常态/低迷/低落/崩溃）：昂扬累计制（每2天+1%售价/+5%预算，封顶+5%/+25%，断档归零）
//   - 双击食物=摄入 cal（变质50%/腐烂20%）+ 已食用档 + 患病判定（变质10%/腐烂40%）
//   - 双击水=清零 thirstLevel（渴系统独立保留）
//   - 节点：濒饿(≤0且≥3天)/饥饿(≤0)/常态(1-5单位)/饱腹(>5单位)
//   - 粮仓充盈：余额≥7单位(15400卡) → 全店售价+5%
//   - 救场：连续≤0达5天 → 好心客户送食1-2份，不删档，归零
//   - 客流削减：低迷-1/低落-2/崩溃-4；禁外出：低落/崩溃
//   - 状态客户联动（Patches/Core 侧）：加价/概率权重/预算/出价
//
// 拆包锚点全部 [L1]（cheatsheet 2.3.9 / 2.3.10 / 2.3.12 / 2.5.16 / 4.6.8 / 4.6.9 / 设计AI v4.2）
// ============================================================
internal static partial class RobinCrusoePerk
{
    internal const string PERK_ID = "RobinCrusoe";
    internal const int START_TYPE = 14;

    // 新三状态（用户拍板 09-09：清洁度/睡眠/社交）
    internal static int CLEAN_START => BuildConfig.CleanStart;      // 清洁度初始（CFG 可调）
    internal static int SLEEP_START => BuildConfig.SleepStart;      // 睡眠初始（CFG 可调）
    internal static int SOCIAL_START => BuildConfig.SocialStart;      // 社交初始（CFG 可调）
    internal static int DAILY_CLEAN_LOSS => BuildConfig.CleanDailyLoss;   // 清洁每日衰减（CFG 可调）
    internal static int DAILY_SLEEP_GAIN => BuildConfig.DailySleepGain;  // 睡眠打烊（CFG 可调）
    internal static int SLEEP_SCAV_LOSS => BuildConfig.SleepScavLoss;    // 外出拾荒睡眠 -%（CFG 可调）
    internal static int DAILY_SOCIAL_GAIN => BuildConfig.DailySocialGain;  // 社交每日 +（开店接待，CFG 可调）
    internal static int DAILY_SOCIAL_LOSS => BuildConfig.DailySocialLoss;         // 濒危分界线
    internal static int MOOD_START => BuildConfig.MoodStart;        // 心情初始值（CFG 可调）
    internal static int DAILY_SAT_LOSS => BuildConfig.DailySatLoss;    // 饱食每日 -%（CFG 可调）
    internal static int DAILY_THIRST_LOSS => BuildConfig.DailyThirstLoss; // 口渴每日 -%（CFG 可调）
    internal static int DAILY_HEALTH_GAIN => BuildConfig.DailyHealthGain;       // 粮仓连续天数（CFG 可调）
    internal static int ELEV_EVERY => BuildConfig.ElevEvery;         // 昂扬结算间隔（CFG 可调）
    internal static int ELEV_MAX => BuildConfig.ElevMax;           // 昂扬累计封顶（CFG 可调）
    internal static int MOOD_UP => BuildConfig.MoodUp;            // 三项全好每日+（CFG 可调）
    internal static int MOOD_DOWN => BuildConfig.MoodDown;

    // ===== 三状态百分比制 + 心情（v5.7，PerkStatePersistence，runID 隔离）=====
    internal static int GetSatiety() => WageSaveStore.GetInt(PERK_ID, "sat", 100);          // 饱食 0-100
    internal static int GetThirstPct() => WageSaveStore.GetInt(PERK_ID, "thirst", 100);    // 口渴 0-100
    internal static int GetHealth() => WageSaveStore.GetInt(PERK_ID, "health", 100);       // 健康 0-100
    internal static int GetMood() => WageSaveStore.GetInt(PERK_ID, "mood", MOOD_START);    // 心情 0-100
    internal static int GetGranaryDays() => WageSaveStore.GetInt(PERK_ID, "granary", 0);   // 粮仓连续天数
    internal static int GetElevStreak() => WageSaveStore.GetInt(PERK_ID, "elevStreak", 0); // 昂扬连续天数
    internal static int GetElevCount() => WageSaveStore.GetInt(PERK_ID, "elevCount", 0);   // 昂扬累计（封顶5）
    internal static int GetStarveDays() => WageSaveStore.GetInt(PERK_ID, "starveDays", 0); // 濒饿持续（饱食<20或口渴<20）
    internal static int GetCritDays() => WageSaveStore.GetInt(PERK_ID, "critDays", 0);     // 病危持续（健康<20）
    internal static int GetThirstDeathDays() => WageSaveStore.GetInt(PERK_ID, "thirstDeath", 0); // 渴死持续（口渴<20）
    internal static void SetSatiety(int v) { WageSaveStore.SetInt(PERK_ID, "sat", v); InvalidateTradeCaches(); }
    internal static void SetThirstPct(int v) { WageSaveStore.SetInt(PERK_ID, "thirst", v); InvalidateTradeCaches(); }
    internal static void SetHealth(int v) { WageSaveStore.SetInt(PERK_ID, "health", v); InvalidateTradeCaches(); }
    internal static void SetMood(int v) { WageSaveStore.SetInt(PERK_ID, "mood", v); InvalidateTradeCaches(); }
    // 新三状态（v5.7+ 用户拍板：清洁度/睡眠/社交）
    internal static int GetClean() => WageSaveStore.GetInt(PERK_ID, "clean", CLEAN_START);       // 清洁 0-100
    internal static int GetSleep() => WageSaveStore.GetInt(PERK_ID, "sleep", SLEEP_START);       // 睡眠 0-100
    internal static int GetSocial() => WageSaveStore.GetInt(PERK_ID, "social", SOCIAL_START);    // 社交 0-100
    internal static void SetClean(int v) { WageSaveStore.SetInt(PERK_ID, "clean", v); InvalidateTradeCaches(); }
    internal static void SetSleep(int v) { WageSaveStore.SetInt(PERK_ID, "sleep", v); InvalidateTradeCaches(); }
    internal static void SetSocial(int v) { WageSaveStore.SetInt(PERK_ID, "social", v); InvalidateTradeCaches(); }

    // ===== Z 键调出/关闭状态面板（用户拍板；特性界面/主菜单不响应，硬约束守护）=====
    // 09-12 实锤：InputActionManager.Update 每帧可被多次调用（多实例/多Patch）→ 必须同帧去重，否则一次按键开→关双翻转，面板打不开
    private static int _zKeyFrame = -1;
    private static bool _autoPopup = true;  // 09-22 新增：自动弹面板开关（按 Z 切换）
    internal static void HandleHotkeys()
    {
        try
        {
            // 09-22 改：按 Z 只打开面板，不碰开关
            if (!UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Z)) return;
            int _zFrame = UnityEngine.Time.frameCount;
            if (_zFrame == _zKeyFrame) return;
            _zKeyFrame = _zFrame;
            if (!IsActive()) return;
            if (!IsActive()) return;
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr != null && mgr.IsOpen("rc_status")) { mgr.CloseWindow("rc_status"); } else { RefreshStatusPanel(force: true); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] HandleHotkeys 异常: " + ex.Message); }
    }
    // ===== 激活判定（职业，双来源）=====
    internal static bool IsActive()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null && (int)ps.startType == START_TYPE) return true;
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng != null && (int)ng.startType == START_TYPE) return true;
        }
        catch { }
        return false;
    }
    // ===== v5.9 失眠警觉：偷窃概率-50%（拆包 2.5.29：原生偷窃=PlayerStore.HandleInsurance，EndNight 链）=====
    public static bool PrefixHandleInsurance()
    {
        try { if (IsActive() && GetAntiTheftMult() < 1.0) {  return false; } } catch { }
        return true;
    }
    // 爆发辅助：收入惩罚（按当日营业额 % 扣款，空营业额日=0；营业额=RecordRevenue 当日累计）
    internal static int ApplyIncomePunish(int pct)
    {
        try
        {
            int revenue = WageSaveStore.GetInt(PERK_ID, "revenue", 0);
            if (revenue <= 0 || pct <= 0) return 0;
            int amt = (int)(revenue * pct / 100.0);
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps != null && amt > 0)
            {
                int cash = Math.Max(0, ps.playerCash - amt);
                ps.playerCash = cash;
                WageSaveStore.SetInt(PERK_ID, "revenue", 0);
                return amt;
            }
        }
        catch { }
        return 0;
    }
    // 爆发辅助：损失 1 件小货物（白名单：食物/水（杂货主体）；不损工具/机器/容器/钥匙卡；AddictOfficer 同款 GetAllItems 遍历）
    internal static int LostSmallItem(int count)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return 0;
            var all = em.GetAllItems();
            if (all == null) return 0;
            int lost = 0;
            foreach (var it in all)
            {
                if (lost >= count) break;
                if (it == null) continue;
                string id = GetId(it);
                if (IsToolOrKeyOrContainer(it)) continue;       // 不损工具/钥匙/容器
                if (!IsFood(it) && !IsDrink(it)) continue;      // 白名单：食物/水
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it); lost++;  }
                }
                catch { }
            }
            return lost;
        }
        catch { return 0; }
    }
    // 爆发辅助：损失 1 件水/容器（嗓子冒烟爆发：打烊失手）
    private static void LostWaterItem()
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return;
            var all = em.GetAllItems();
            if (all == null) return;
            foreach (var it in all)
            {
                if (it == null) continue;
                if (!IsDrink(it)) continue;
                try
                {
                    var inv = FindContainingInventory(it);
                    if (inv != null) { inv.Expel(it);  return; }
                }
                catch { }
            }
        }
        catch { }
    }
    // 找包含指定物品的库存（遍历店铺各库存容器 childItems）
    private static GameInventory FindContainingInventory(GameItem target)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null) return null;
            var invs = new Il2CppSystem.Collections.Generic.List<GameInventory>();
            try
            {
                if (em.frontInvinvElement != null) invs.Add(em.frontInvinvElement);
                if (em.backInvinvElement != null) invs.Add(em.backInvinvElement);
                if (em.hiddenElement != null) invs.Add(em.hiddenElement);
                if (em.afterhourInventory != null) invs.Add(em.afterhourInventory);
            }
            catch { }
            foreach (var inv in invs)
            {
                try
                {
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                        if (inv.childItems[i] == target) return inv;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
    private static bool IsToolOrKeyOrContainer(GameItem item)
    {
        try
        {
            string id = GetId(item);
            if (item.IsTag("IMPORTANT_TAG")) return true;
            if (item.IsTag("MACHINE")) return true;
            if (item.IsTag("LOCKED")) return true;
            if (id != null && (id.Contains("storage") || id.Contains("key") || id.Contains("tool") || id.Contains("machine") || id.Contains("module"))) return true;
            return false;
        }
        catch { return false; }
    }
    private static bool IsGroceries(GameItem item)
    {
        try { return item.IsTag("GROCERY"); } catch { return false; }
    }
    private static bool IsRaw(GameItem item)
    {
        try { return item.IsTag("RAW"); } catch { return false; }
    }
    // 当日营业额累计（PostfixStoreClientOnDealAccepted BUY 分支调用；打烊收入惩罚后清零）
    internal static void RecordRevenue(int amount)
    {
        if (amount <= 0) return;
        WageSaveStore.SetInt(PERK_ID, "revenue", WageSaveStore.GetInt(PERK_ID, "revenue", 0) + amount);
    }
    internal static int GetTodayRevenue() => WageSaveStore.GetInt(PERK_ID, "revenue", 0);
    // 心情提升（上限 100）
    internal static void BoostMood(int amount, string reason)
    {
        try
        {
            int m = Math.Min(100, GetMood() + amount);
            SetMood(m);
            try { StoreUIManager.Instance.Notify(LangHelper.T("心情 +" + amount + "（" + reason + "）", "Mood +" + amount + " (" + reason + ")"), "yellow"); } catch { }
            RefreshStatusPanel(); // 心情实时刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] BoostMood 异常: " + ex.Message); }
    }

    // ===== v5.7 玩家常驻状态面板（拆包回填4：CustomUIManager overlay + 进度条，重建法最稳）=====
    // 饱食/口渴/健康进度条 + 具体数值（用户要求直观数值），心情与加成 label
    internal static void RefreshStatusPanel(bool force = false)
    {
        try
        {
            if (!_autoPopup && !force) return;  // 09-22 用户拍板：自动弹关闭时不打开（Z键强制打开除外）
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen("rc_status")) mgr.CloseWindow("rc_status");
            var b = mgr.CreateWindow("rc_status", LangHelper.T("鲁滨逊 · 生存状态", "Robinson · Survival"), "overlay");
            if (b == null) return;
            int sat = GetSatiety(), th = GetThirstPct(), h = GetHealth();
            // 显示具体单位：饱食 100%=2200 kcal（v5.7 锁定）、口渴 100%=2000 ml（与喝水 200ml=10% 自洽）
            int satCal = (int)(sat * 22f);
            int thMl = (int)(th * 20f);
            b.SetSize(300, 500).SetPosition(Vector2.zero);
            // 固定右上角（09-10 用户拍板：锚点(1,1) pivot(1,1) 右上角内侧 16px，不随分辨率变化）
            try
            {
                var w = mgr.GetWindow("rc_status");
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -16);
                }
            }
            catch { }
            b.BeginColumn(4f);
            b.AddLabel(LangHelper.T("饱食 ", "Satiety ") + satCal + "/2200 kcal", "sat_l");
            b.AddProgressBar(sat / 100f, "sat");
            b.AddLabel(LangHelper.T("口渴 ", "Thirst ") + thMl + "/2000 ml", "th_l");
            b.AddProgressBar(th / 100f, "th");
            b.AddLabel(LangHelper.T("健康 ", "Health ") + h + "/100", "h_l");
            b.AddProgressBar(h / 100f, "h");
            b.AddLabel(LangHelper.T("血量 ", "Blood ") + GetBlood() + "/6000", "blood_l");
            b.AddProgressBar(GetBlood() / (float)BLOOD_MAX, "blood");
            var sellBtnOnClick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { TrySellBlood(); } catch (Exception ex) { Core.LogMsg("[鲁滨逊] 面板卖血异常: " + ex.Message); } }));
            b.AddButton(LangHelper.T("卖血 -500ml", "Sell Blood -500ml"), sellBtnOnClick, "sell_blood_btn");
            // 09-22 自动弹出开关
            var autoPopupBtn = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { _autoPopup = !_autoPopup; try { Il2Cpp.StoreUIManager.Instance.Notify(LangHelper.T(_autoPopup ? "鲁滨逊面板：自动弹开启" : "鲁滨逊面板：自动弹关闭", "Crusoe panel: auto-popup " + (_autoPopup ? "ON" : "OFF"))); } catch { } RefreshStatusPanel(); }));
            b.AddButton(LangHelper.T("自动弹出：" + (_autoPopup ? "开" : "关"), "Auto-popup: " + (_autoPopup ? "ON" : "OFF")), autoPopupBtn, "auto_popup_btn"); // 09-20 用户拍板：面板按钮为唯一采血入口（替代采血包）
            if (IsForcedRest()) b.AddLabel(LangHelper.T("昏迷中 · 剩余 " + WageSaveStore.GetInt(PERK_ID, "blood_rest", 0) + " 天", "Coma - " + WageSaveStore.GetInt(PERK_ID, "blood_rest", 0) + "d left"), "blood_rest_l");
            else if (IsBloodWeak()) b.AddLabel(LangHelper.T("虚弱（血量过低）", "Too weak (low blood)"), "blood_weak_l");
            // 新三状态（v5.7+ 用户拍板）：清洁/睡眠/社交 进度条+数值
            int clean = GetClean(), sleep = GetSleep(), social = GetSocial();
            b.AddLabel(LangHelper.T("清洁 ", "Cleanliness ") + clean + "/100", "clean_l");
            b.AddProgressBar(clean / 100f, "clean");
            b.AddLabel(LangHelper.T("睡眠 ", "Sleep ") + sleep + "/100", "sleep_l");
            b.AddProgressBar(sleep / 100f, "sleep");
            b.AddLabel(LangHelper.T("社交 ", "Social ") + social + "/100", "social_l");
            b.AddProgressBar(social / 100f, "social");
            // v5.8-8：逐节点状态显示（六状态 + 心情，每个当前节点一行：名称 + 锁定/抽取效果）
            b.AddLabel(LangHelper.T("── 节点状态 ──", "── Node Status ──"), "node");
            int[] allNodes = { SatietyNode(), ThirstNode(), HealthNode(), CleanNode(), SleepNode(), SocialNode(), MoodNode() };
            string domKey = GetStoredNodeKey();
            foreach (int n in allNodes)
            {
                if (n < 0 || n >= NODES.Length) continue;
                NodeDef d = NODES[n];
                string fxDesc = "";
                foreach (string f in d.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                if (d.Key == domKey) // 主导节点：追加本次抽取效果
                {
                    string cur = GetNodeFx();
                    if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                }
                string line = d.DisplayName;
                if (fxDesc.Length > 0) line += "｜" + fxDesc.Trim();
                b.AddLabel(line, "node");
            }
            int sellB = GetSellBonusPct(), budB = GetBudgetBonusPct(), moodNow = GetMood();
            // 09-21 改：心情加成单独标识，总售价单独一行
            int moodSell = moodNow >= 60 ? 10 : (moodNow < 40 ? -10 : 0);
            string moodLine = LangHelper.T("心情 ", "Mood ") + moodNow;
            if (moodSell != 0) moodLine += "｜" + LangHelper.T("售价", "Sell") + (moodSell > 0 ? "+" : "") + moodSell + "%";
            b.AddLabel(moodLine, "mood");
            // 总加成单独一行（列出所有分项）
            int granaryB = (GetGranaryDays() >= GRANARY_DAYS) ? 5 : 0;
            int elevB = Math.Min(ELEV_MAX, GetElevCount());
            int fxSellB = FxNum("sell");
            int compB = (int)GetCompBuffSellBonus();
            int moodSellB = moodNow >= 60 ? 10 : (moodNow < 40 ? -10 : 0);
            b.AddLabel(LangHelper.T("── 售价加成明细 ──", "── Sell Bonus Breakdown ──"), "sell_detail_hdr");
            if (granaryB != 0) b.AddLabel(LangHelper.T("粮仓 ", "Granary ") + (granaryB > 0 ? "+" : "") + granaryB + "%", "sell_granary");
            if (elevB != 0) b.AddLabel(LangHelper.T("昂扬 ", "Elevate ") + (elevB > 0 ? "+" : "") + elevB + "%", "sell_elev");
            if (fxSellB != 0) b.AddLabel(LangHelper.T("节点 ", "Nodes ") + (fxSellB > 0 ? "+" : "") + fxSellB + "%", "sell_nodes");
            if (compB != 0) b.AddLabel(LangHelper.T("精打细算 ", "Penny Pincher ") + (compB > 0 ? "+" : "") + compB + "%", "sell_comp");
            if (moodSellB != 0) b.AddLabel(LangHelper.T("心情 ", "Mood ") + (moodSellB > 0 ? "+" : "") + moodSellB + "%", "sell_mood");
            b.AddLabel(LangHelper.T("总售价 ", "Total Sell ") + (sellB > 0 ? "+" : "") + sellB + "%", "sell_total");
            if (budB != 0) b.AddLabel(LangHelper.T("总预算 ", "Total Budget ") + (budB > 0 ? "+" : "") + budB + "%", "budget_total");
            b.End();
            b.Show();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 状态面板异常: " + ex.Message); }
    }

    // 09-22 新档防 default_run 残留污染：鲁滨逊开局全部持久化 key（TrySetupNewRun 18 + 运行期固定 3 + 补偿天数 cb_* 13）
    private static readonly string[] ALL_KEYS = new string[]
    {
        "robinson_hard","sat","thirst","health","blood","mood","granary","elevStreak","elevCount",
        "starveDays","thirstDeath","critDays","clean","sleep","social","nodeKey","nodeFxIdx","deals",
        "revenue","blood_rest","hbuffDay",
        "cb_eatEff","cb_thirstEff50","cb_thirstEff10","cb_wearEff","cb_drugEff","cb_antiTheft","cb_moodDamp",
        "cb_forage20","cb_sell5","cb_mood2","cb_mood3","cb_sleepR10","cb_contraEff"
    };
    internal static void CleanDefaultRunOnNewGame()
    {
        try { PerkStatePersistence.CleanDefaultRun(PERK_ID, ALL_KEYS); } catch { } // 阶段1保留：清的是旧层(PlayerPrefs) default_run 残留，属旧层卫生，新层无此问题
    }
    // ===== 开局（HandleInitialItem Postfix 调用）=====
    internal static void TrySetupNewRun()
    {
        try
        {
            if (!IsActive()) return;
            WandererPerk.ClearBackpack(); // 09-20 设计稿：清原版发放（invElement+dossier）——先清后发，防误清自己物资
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 原生困难模式开关（开局界面）
            WageSaveStore.SetInt(PERK_ID, "robinson_hard", hard ? 1 : 0); // 随档（原生 hardMode 退出重进重置，存 mod 状态）
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null)
            {
                // 09-20 设计稿：金钱随机——hard 清零；普通 50%→0 / 50%→1~600（原固定 360）
                if (hard) ps.playerCash = 0;
                else if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
                else ps.playerCash = Core.Rng.Next(1, 601);
            }

            // 09-21 物资发放移到 PostfixStartNewGame（StartNewGame 在原版发放之后触发：先清原版 4 件+文档再发，根治清太早）
            // HardMode：无开局物资

            WageSaveStore.SetInt(PERK_ID, "sat", 100);        // v5.7 三状态初始
            WageSaveStore.SetInt(PERK_ID, "thirst", 100);
            WageSaveStore.SetInt(PERK_ID, "health", 100);
            WageSaveStore.SetInt(PERK_ID, "blood", BLOOD_MAX); // 卖血：开局满血 6000ml（09-17）
            WageSaveStore.SetInt(PERK_ID, "mood", MOOD_START);
            WageSaveStore.SetInt(PERK_ID, "granary", 0);
            WageSaveStore.SetInt(PERK_ID, "elevStreak", 0);
            WageSaveStore.SetInt(PERK_ID, "elevCount", 0);
            WageSaveStore.SetInt(PERK_ID, "starveDays", 0);
            WageSaveStore.SetInt(PERK_ID, "thirstDeath", 0);
            WageSaveStore.SetInt(PERK_ID, "critDays", 0);
            WageSaveStore.SetInt(PERK_ID, "clean", CLEAN_START);   // 新三状态初始（用户拍板）
            WageSaveStore.SetInt(PERK_ID, "sleep", SLEEP_START);
            WageSaveStore.SetInt(PERK_ID, "social", SOCIAL_START);
            WageSaveStore.SetString(PERK_ID, "nodeKey", "");       // v5.8-8 节点池：开局清空，首次打烊抽取
            WageSaveStore.SetInt(PERK_ID, "nodeFxIdx", -1);
            WageSaveStore.SetInt(PERK_ID, "deals", 0);             // 接待计数
            SyncRentDisplay(); // 开局第一天就同步租金显示字段（拆包：日历读 dayUntilRent+rentValue）
            RefreshStatusPanel(); // 开局建常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TrySetupNewRun 异常: " + ex.Message); }
    }

    // ===== 09-21 开局物资：PlayerStore.StartNewGame Postfix（发放后清——原版 4 件+文档在 StartNewGame 发放，HandleInitialItem 清太早白清）=====
    internal static void PostfixStartNewGame()  // PlayerStore.StartNewGame Postfix（Core.cs 注册）
    {
        try
        {
            if (!IsActive()) return;
            WandererPerk.ClearBackpack(); // 清原版发放（magnifier/labeler/topical_bandage_item/fanny_pack + dossier 文档）
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 09-20 修：不依赖PerkStatePersistence时序——直接读原生开局开关（偶尔StartNewGame先跑导致读旧档残留0）
            if (!hard) GiveStartingGoods();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStartNewGame 异常: " + ex.Message); }
    }

    private static void GiveStartingGoods()
    {
        GiveToBackpack("processed_meat", 3);      // 口粮×3（三天量）
        GiveToBackpack("raw_meat", 2);            // 大肉×2（用户拍板 09-09：另加生肉）
        GivePureWaterToBackpack(3);               // 大瓶纯水×3（三天量）
        GiveToBackpack("bandage_item", 5);        // 绷带×5（bandage_item 正确 id）
    }

    private static void GiveToBackpack(string id, int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) continue;
                // 重叠bug修复（用户拍板 09-09：参考 QuickItemSpawner F3 先例）：
                // 正确链 = TryFindOneValidInventorySlot(item) → slot.TryAcceptOnce()（slot 持有格子坐标，真正落格）；
                // 之前丢弃 slot 直接 UncheckedAccept → 不设坐标 → 同格重叠。TryAcceptOnce 失败才 UncheckedAccept 兜底。
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(item);
            }
        }
        catch { }
    }

    private static void GivePureWaterToBackpack(int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 09-20 设计稿：直接生成带水大瓶（删 DirectoryMaster.Item+AddWater 链——工厂产物 AddWater 静默失败 → 空瓶）
                GameItem spawn = item;
                try { spawn.DisableTag("stolen", true); } catch { }
                // 同 GiveToBackpack：slot.TryAcceptOnce 真正落格，防重叠
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(spawn, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(spawn);
            }
        }
        catch { }
    }

    // 09-11 用户确认：取消"前3天无随机客户"设定（PrefixHandleNormalClient/AugClient/AnyClient 已删，只保留次要客户永久拦截）
    public static bool PrefixHandleMinorClient()
    {
        // 09-10 用户拍板：次要客户（拾荒客/上层医生等）永久删掉，不限前3天
        try { if (IsActive()) return false; }
        catch { }
        return true;
    }

    // ===== 客流减量（精神档位：低迷-1 / 低落-2 / 崩溃-4；v5.9 病恹恹爆发当日客流-50% 隔一skip一）=====
    private static int _pickSkipToday = 0;
    private static bool _burstSkipFlip = false;
    public static bool PrefixPickClient()
    {
        try
        {
            if (!IsActive()) return true;
            int skip = GetClientReduction();
            if (_pickSkipToday < skip) { _pickSkipToday++; return false; }
            if (_burstClientCut >= 50) { _burstSkipFlip = !_burstSkipFlip; if (_burstSkipFlip) return false; } // 病恹恹爆发：当日客流-50%
        }
        catch { }
        return true;
    }

    // ===== 禁外出（低落/崩溃，含生病）=====
    public static bool PrefixOpenGoOutsideConfirm()
    {
        try { if (IsActive() && IsForbiddenOutside()) return false; }
        catch { }
        return true;
    }

    // ===== 腐烂/已食用食物客户拒买 =====
    private static int _rejectLogCount = 0;
    public static bool PrefixCanClientExposeAnyFeature(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem))
            {
                bool reject = false;
                if (GetFoodQuality(gameItem) >= 3) { reject = true; }
                else if (IsEaten(gameItem)) { reject = true; }
                if (reject)
                {
                    _rejectLogCount++;
                    if (_rejectLogCount % 60 == 1)
                    return false;
                }
            }
        }
        catch { }
        return true;
    }

    // ===== 吃过的食物放不上柜台 =====
    public static bool PrefixAddedItemToWeightedArea(GameItem gameItem)
    {
        try
        {
            if (IsActive() && gameItem != null && IsFood(gameItem) && IsEaten(gameItem))
            {
                return false;
            }
        }
        catch { }
        return true;
    }

    // ===== 双击食用（吃一口/喝一口/吃药治病）=====
    // 判定：物品是否在博士夜晚商店库存（afterhourInventory）里——没买不能吃（用户反馈修复）
    private static bool IsInDoctorNightInventory(GameItem item)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.afterhourInventory == null || item == null) return false;
            long ptr = (long)item.Pointer;
            if (em.afterhourInventory.childItems != null)
            {
                for (int i = 0; i < em.afterhourInventory.childItems.Count; i++)
                {
                    var it = em.afterhourInventory.childItems[i];
                    if (it != null && (long)it.Pointer == ptr) return true;
                }
            }
        }
        catch { }
        return false;
    }

    // 基础价值（口径稳定：unitBaseValue；失败退 GetCurrentValue）
    private static int GetItemBaseValue(GameItem item)
    {
        try { return (int)item.unitBaseValue; } catch { }
        try { return (int)item.GetCurrentValue(); } catch { }
        return 1;
    }

    // ===== 每日结算（OnNewDay Postfix）=====
    public static void PostfixOnNewDay()
    {
        try
        {
            if (!IsActive()) return;
            _pickSkipToday = 0;
            SyncRentDisplay(); // 租金显示同步为100天制（拆包：日历读 dayUntilRent+rentValue）

            // ===== v5.9 节点池抽取（nodeKey 变化 → 先爆发 RollBurst 再抽池；正面节点无爆发）=====
            RollNodeFx();

            // ===== v5.9 三状态自然变化（eatEff 只影响吃食物、thirstEff 影响口渴衰减、wearEff 影响健康衰减）=====
            int sat = Math.Max(0, GetSatiety() - DAILY_SAT_LOSS - FxNum("satD"));   // 饱食 -20% + 节点衰减（嗓子冒烟 satD+10）
            int thBase = (int)(DAILY_THIRST_LOSS * GetThirstEffMult());             // 口渴 -25% × 耐旱/省水系数（v5.9 CompBuff）
            int th = Math.Max(0, GetThirstPct() - thBase - FxNum("thD"));           // 口渴 -25% + 节点衰减（肚里打鼓 thD+5）
            int h0 = GetHealth();
            int healthGain = DAILY_HEALTH_GAIN + FxNum("hR");   // 健康 +10% + 节点恢复修正（病恹恹 hR-5 / 透心凉 hR+5）
            int hDecay = (int)(FxNum("hD") * GetWearEffMult());                     // 健康衰减 × 糙人抗造系数（v5.9 CompBuff）
            int h = Math.Min(100, Math.Max(0, h0 + healthGain - hDecay));           // 健康自然变化 + 衰减修正
            if (IsBloodWeak()) h = Math.Max(0, h - 10); // 卖血虚弱（<3000）：健康衰减加速（09-17）
            SetSatiety(sat); SetThirstPct(th); SetHealth(h);
            // 新三状态结算（v5.8-8）：清洁 -10 + 节点衰减/恢复；睡眠 打烊+30（拾荒当天已 -15）+ 节点睡眠恢复 + 补觉高效；社交 接待日+5/无客日-5 + 节点
            SetClean(Math.Max(0, Math.Min(100, GetClean() - DAILY_CLEAN_LOSS - FxNum("cleanD") + FxNum("cleanR"))));
            SetSleep(Math.Min(100, Math.Max(0, GetSleep() + DAILY_SLEEP_GAIN + FxNum("sleepR") + GetCompBuffSleepRestore()))); // sleepR 符号修正（拆包 09-10：'sleepR-10'=恢复-10，减号负负得正，改加号）
            AddBlood(100); // 睡觉回血（09-17 卖血）
            TickBloodRest(); // 09-20 M5：虚弱强制休息 3 天 → 结束 ±20%
            int deals = WageSaveStore.GetInt(PERK_ID, "deals", 0);
            int social = GetSocial() + (deals > 0 ? DAILY_SOCIAL_GAIN : -DAILY_SOCIAL_LOSS) + FxNum("socD") + FxNum("socR");
            SetSocial(Math.Max(0, Math.Min(100, social)));
            WageSaveStore.SetInt(PERK_ID, "deals", 0); // 接待计数清零

            // ===== v5.9 CompBuff 每日递减（Duration 制：到期移除）=====
            TickCompBuffs();

            // ===== 心情结算（v5.7 拍板：三项≥80→+5；任一项<60→-10；60-79→不掉不涨；仅看饱/渴/健三项）+ 节点心情 + CompBuff 心情 =====
            int mood = GetMood();
            bool satOK = sat >= SATIETY_GOOD, thOK = th >= THIRST_GOOD, hOK = h >= HEALTH_GOOD;
            if (satOK && thOK && hOK) mood = Math.Min(100, mood + MOOD_UP);
            else if (sat < 60 || th < 60 || h < 60) mood = Math.Max(0, mood - (int)(MOOD_DOWN * GetMoodDampMult())); // 摆烂反弹：-10→-5（v5.9）
            mood = Math.Max(0, Math.Min(100, mood + FxNum("mood")));
            mood = Math.Min(100, mood + GetCompBuffMoodBonus()); // 病中专注/松弛自洽/清静自处 每日心情+（v5.9 CompBuff）
            SetMood(mood);

            // ===== 患病（节点池 sick+20：饿疯池恶，每日结算概率患病）=====
            if (GetSickChanceAdd() > 0)
            {
                TryInfect(GetSickChanceAdd() / 100.0);
            }

            // ===== v5.9 觅食（店内翻找，打烊结算概率；躺板板30% / 清洁<50 15% + 独狼专注觅食+20%）=====
            int hNow = GetHealth(), cNow = GetClean();
            int forageBonus = GetForageBonus(); // 独狼专注：觅食概率+20%（v5.9 CompBuff）
            if (hNow < NODE_CRIT && UnityEngine.Random.value < (0.30f + forageBonus / 100f))      // 躺板板（健康<20）：30% 翻出 1-2 份食物
                ForageIndoor(UnityEngine.Random.value < 0.5f ? 2 : 1, LangHelper.T("躺板板翻找", "Bedridden rummaging"));
            else if (cNow < 50 && UnityEngine.Random.value < (0.15f + forageBonus / 100f))         // 清洁<50（蓬头垢面+灰头土脸）：15% 翻出 1 份
                ForageIndoor(1, LangHelper.T("店内翻找", "In-store rummaging"));

            // ===== 连续计数（濒饿/渴/病危）=====
            bool starving = sat < NODE_CRIT || th < NODE_CRIT;          // 濒饿：饱食<20 或 口渴<20
            int sd5 = starving ? GetStarveDays() + 1 : 0;
            int td5 = th < NODE_CRIT ? GetThirstDeathDays() + 1 : 0;    // 渴死独立（口渴<20 连续）
            int cd5 = h < NODE_CRIT ? GetCritDays() + 1 : 0;            // 病危：健康<20 连续
            WageSaveStore.SetInt(PERK_ID, "starveDays", sd5);
            WageSaveStore.SetInt(PERK_ID, "thirstDeath", td5);
            WageSaveStore.SetInt(PERK_ID, "critDays", cd5);

            // ===== 救场安全网（v5.7：濒饿5天送食 / 病危3天送药；2026-09-09 修复：渴死4天快于濒饿救场5天 → 渴死前第3天补送水）=====
            if (sd5 == 5) RescueFeed();
            else if (td5 >= 3 && sd5 < 5) RescueWater();                       // 濒渴第3天送水（第4天渴死前；饿优先）
            else if (cd5 == 3 && sd5 < 5 && td5 < 3) RescueMedicine();         // 病危第3天送药（第4天病死前）

            // ===== 三种生存死亡（用户拍板 09-09 方案A：饿5/渴4/病4，持续天数；救场当天豁免、送食/药后仍恶化才死）=====
            if (sd5 > 5) { ExecuteGameOverBy("starvation"); return; }          // 濒饿第5天送食，仍持续（第6天起）饿死
            if (td5 >= 4 && sd5 < 5) { ExecuteGameOverBy("thirst"); return; }  // 濒渴 4 天渴死（饿优先；救场赶不上属设计）
            if (cd5 > 3 && sd5 < 5 && td5 < 4) { ExecuteGameOverBy("disease"); return; } // 病危第3天送药，仍持续（第4天起）病死

            // ===== 粮仓充盈（v5.7：饱食≥80 连续 7 天，断档归零）=====
            int gd = sat >= SATIETY_GOOD ? GetGranaryDays() + 1 : 0;
            WageSaveStore.SetInt(PERK_ID, "granary", gd);
            // ===== 昂扬累计（v5.7：饱食≥80 且健康≥80 每2天 +1%售价 +5%预算，封顶5，封顶后断档维持）=====
            int es = (sat >= SATIETY_GOOD && h >= HEALTH_GOOD) ? GetElevStreak() + 1 : 0;
            int ec = GetElevCount();
            if (es >= ELEV_EVERY)
            {
                ec = Math.Min(ELEV_MAX, ec + 1);
                es = 0;
            }
            WageSaveStore.SetInt(PERK_ID, "elevStreak", es);
            WageSaveStore.SetInt(PERK_ID, "elevCount", ec);
            _burstClientCut = 0; _burstSkipFlip = false; // v5.9 病恹恹爆发客流-50% 当日标记清零

            // ===== 食物衰减 + 口粮统计 =====
            int fresh = 0, stale = 0, rotten = 0;
            int foodCount = DecayFoodsAndCount(out fresh, out stale, out rotten);

            // ===== 播报（v5.8-8：锁定节点名 + 叙事标签 + 锁定效果 + 加成）=====
            int day = StoreStation.GetDayCounter();
            int sellB = GetSellBonusPct();
            int budB = GetBudgetBonusPct();
            NodeDef dn = CurrentNode();
            string nodeTxt;
            bool badNode = false;
            if (dn == null) nodeTxt = LangHelper.T("状态平稳（饱食" + sat + " 口渴" + th + " 健康" + h + "）", "Stable (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            else
            {
                badNode = dn.Sev >= 4;
                string fxDesc = "";
                foreach (string f in dn.Lock) { string lb = FxLabel(f); if (lb.Length > 0) fxDesc += lb + " "; }
                string cur = GetNodeFx();
                if (!string.IsNullOrEmpty(cur) && cur != "flavor") { string lb = FxLabel(cur); if (lb.Length > 0) fxDesc += lb + " "; }
                nodeTxt = dn.DisplayName + LangHelper.T("：", ": ") + dn.DisplayTag + (fxDesc.Length > 0 ? "｜" + fxDesc.Trim() : "") + LangHelper.T("（饱食" + sat + " 口渴" + th + " 健康" + h + "）", " (" + sat + " satiety / " + th + " thirst / " + h + " health)");
            }
            string moodTxt = LangHelper.T("心情 ", "Mood ") + mood + (sellB > 0 || budB > 0 ? "｜" + LangHelper.T("售价+" + sellB + "% 预算+" + budB + "%", "Sell +" + sellB + "% Budget +" + budB + "%") : "");
            string granaryTxt = gd >= GRANARY_DAYS ? LangHelper.T("｜★粮仓充盈 售价+5%", "| Granary full, Sell +5%") : (gd > 0 ? LangHelper.T("｜粮仓 " + gd + "/7 天", "| Granary " + gd + "/7 days") : "");
            try
            {
                var ps = PlayerStore.Instance;
                if (ps != null)
                {
                    ps.AddNightLog(LangHelper.T("—— 鲁滨逊的账本 · 第 " + day + " 天 ——", "-- Robinson's Ledger · Day " + day + " --"), "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(nodeTxt + "｜" + moodTxt + granaryTxt, "#7FC97F"); // 09-22 统一柔和绿
                    ps.AddNightLog(LangHelper.T("口粮：新鲜 " + fresh + "｜变质 " + stale + "｜腐烂 " + rotten + "（共" + foodCount + "份可吃）", "Rations: fresh " + fresh + " | stale " + stale + " | rotten " + rotten + " (" + foodCount + " edible)"), "#7FC97F"); // 09-22 统一柔和绿
                }
            }
            catch { }
            try { StoreUIManager.Instance.Notify(LangHelper.T("第" + day + "天：", "Day " + day + ": ") + nodeTxt + "｜" + moodTxt, "white"); } catch { }
            RefreshStatusPanel(); // 每日结算刷新常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixOnNewDay 异常: " + ex.Message); }
    }
    // ===== 接待计数（OnDealAccepted Postfix 调用，社交结算用）=====
    internal static void RecordDeal()
    {
        try { if (IsActive()) WageSaveStore.SetInt(PERK_ID, "deals", WageSaveStore.GetInt(PERK_ID, "deals", 0) + 1); } catch { }
    }
}
