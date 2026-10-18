using System;

namespace WagePerks;

// ============================================================
// 蛙娘 · 「门路」（2026-10-18 设计稿《假货原价与违禁品免罪》§2-§5 落地）
// 一句话：假货按原价出、违禁品正常收；代价=额度/抽成/好感/走漏。
// 两条链路分开（设计稿 §0 实锤）：
//   - 寄卖链路（Interact 收 + Consign 结算）本就不经原版犯罪系统 → A/B 是「显式写进规则 + 防回归断言」
//   - Lv3 柜台直售免罪（SecData Prefix）是唯一新增的免罪能力（§8.1-6，Prefix 挂载待拆包签名确认后补）
// 等级载体：B2ServiceStore.owned_蛙娘门路（复用等级框架，独立于蛙娘卡）
// ============================================================
internal static class WageGirlConn
{
    public const string PerkId = "蛙娘门路";

    private const string NS = "WageGirlConn";

    // ---- 存档键（§8.2，WageSaveStore 按 runID 自动隔离新档） ----
    private const string K_USED_DAY    = "used_day";
    private const string K_USED_DAY_OF = "used_day_of";
    private const string K_USED_RUN    = "used_run";
    private const string K_GAIN_RUN    = "gain_run";
    private const string K_CRIME_RUN   = "crime_run";  // Lv3 直售豁免犯罪值（Prefix 落地后用）
    private const string K_AFF_MONTH   = "aff_month";
    private const string K_AFF_MONTH_OF= "aff_month_of";
    private const string K_LAST_DAY    = "last_day";

    // ---- 等级（复用 B2ServiceStore；0=未购 1/2/3） ----
    internal static int Level()
    {
        try { return B2ServiceStore.GetEffectLevel(PerkId); } catch { return 0; }
    }

    // ---- 等级参数（§5.1，cfg 可覆盖，默认=设计稿） ----
    internal static int DailyCap(int lv)   // 每日额度：1/2/3
        => lv == 1 ? BuildConfig.WageGirlConnLv1Daily : lv == 2 ? BuildConfig.WageGirlConnLv2Daily : BuildConfig.WageGirlConnLv3Daily;
    internal static int RunCap(int lv)     // 每局总量：20/50/100
        => lv == 1 ? BuildConfig.WageGirlConnLv1RunTotal : lv == 2 ? BuildConfig.WageGirlConnLv2RunTotal : BuildConfig.WageGirlConnLv3RunTotal;
    internal static int ValueCap(int lv)   // 单件原价上限：600/1200/2500
        => lv == 1 ? BuildConfig.WageGirlConnLv1ValueCap : lv == 2 ? BuildConfig.WageGirlConnLv2ValueCap : BuildConfig.WageGirlConnLv3ValueCap;
    internal static int FeePct(int lv)     // 门路费：20/15/10
        => lv == 1 ? BuildConfig.WageGirlConnLv1FeePct : lv == 2 ? BuildConfig.WageGirlConnLv2FeePct : BuildConfig.WageGirlConnLv3FeePct;
    internal static int LeakPct(int lv)    // 走漏概率：6/3/0
        => lv == 1 ? BuildConfig.WageGirlConnLv1LeakPct : lv == 2 ? BuildConfig.WageGirlConnLv2LeakPct : BuildConfig.WageGirlConnLv3LeakPct;
    internal static int GainCap(int lv)    // 每局累计增益硬顶：8000/30000/80000
        => lv == 1 ? BuildConfig.WageGirlConnGainCapLv1 : lv == 2 ? BuildConfig.WageGirlConnGainCapLv2 : BuildConfig.WageGirlConnGainCapLv3;
    internal static int CrimeCap()         // Lv3 直售豁免犯罪值硬顶：30000
        => BuildConfig.WageGirlConnCrimeCapLv3;

    private static int Today()
    {
        try { var ss = Il2Cpp.StoreStation.Instance; if (ss != null) return ss.dayCounter; } catch { }
        return 0;
    }

    // ---- 当日已用额度（含走漏清零语义：走漏后 used_day 置满额 → 当日拒收） ----
    private static int UsedToday()
    {
        int dayOf = WageSaveStore.GetInt(NS, K_USED_DAY_OF, -1);
        if (dayOf != Today()) { WageSaveStore.SetInt(NS, K_USED_DAY_OF, Today()); WageSaveStore.SetInt(NS, K_USED_DAY, 0); }
        return WageSaveStore.GetInt(NS, K_USED_DAY, 0);
    }

    // ---- 触发条件判定（§3 全满足 + §4 四道闸） ----
    // 返回值 true = 可用并已消耗当日额度/局总量（调用方随后记 flags）；false = 不可用，why 给原因（调用方走旧口径）
    internal static bool TryUse(Il2Cpp.GameItem item, bool isContraband, out int fee, out string why)
    {
        fee = 0; why = "";
        try
        {
            if (!BuildConfig.WageGirlConnEnabled) { why = "门路总开关已关"; return false; }
            int lv = Level();
            if (lv < 1) { why = "还没门路"; return false; }
            // 条件2：好感节点 ≥ 4（≥50）
            try
            {
                double aff = WageGirlSystem.GetAffectionExact();
                if (WageGirlSystem.GetAffNode(aff) < 4) { why = "蛙娘好感还不够"; return false; }
            }
            catch { }
            // 条件4/5：当日额度 + 每局总量
            int used = UsedToday();
            if (used >= DailyCap(lv)) { why = "今日门路用完了"; return false; }
            int usedRun = WageSaveStore.GetInt(NS, K_USED_RUN, 0);
            if (usedRun >= RunCap(lv)) { why = "本档门路额度用尽了"; return false; }
            // 冷却（§5.1：Lv1=2天 / Lv2=1天 / Lv3=无）
            if (lv < 3)
            {
                int cd = lv == 1 ? 2 : 1;
                int last = WageSaveStore.GetInt(NS, K_LAST_DAY, -999);
                if (last >= 0 && (Today() - last) < cd) { why = "门路还在冷却"; return false; }
            }
            // 条件8：单件原价上限（口径同 Interact 寄卖 uv=item.unitValue）
            long uv = 0;
            try { uv = (int)item.unitValue; } catch { uv = 0; }
            if (uv > ValueCap(lv)) { why = "这单太大，压不住"; return false; }
            // 消耗额度（写档；走漏清零后 used=满额 自然拒收）
            WageSaveStore.SetInt(NS, K_USED_DAY, used + 1);
            WageSaveStore.SetInt(NS, K_USED_RUN, usedRun + 1);
            WageSaveStore.Flush();
            fee = FeePct(lv);
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘门路] TryUse 异常: " + ex.Message); why = "门路判定异常"; return false; }
    }

    // ---- 结算后（§8.1-3/4/5 配套）：好感-1（月上限+ Lv3 下限50）+ 冷却日 + 走漏判定 + 累计增益 ----
    // baseValue = 记录值（门路货=原价）；price = 实际结算价（含 margin 与门路费后）
    // gain 口径（写注释防误读）：门路增收 ≈ price − 旧口径期望（违禁品 60% / 假货 10%，margin 均值 1.2）。
    //   硬顶只截断极端选货（§5.2：正常玩法碰不到），量级对即可，不做精确 margin 反推。
    internal static void OnSettled(long baseValue, long price, bool isContraband, bool isFake)
    {
        try
        {
            int lv = Level(); if (lv < 1) return;
            // 冷却日记录（Lv3 无冷却但记录无害）
            WageSaveStore.SetInt(NS, K_LAST_DAY, Today());
            // 好感 -1：月上限 AffCostMonthlyCap（5）；Lv3 下限保护 50
            try
            {
                int month = Today() / 30;
                int affMonthOf = WageSaveStore.GetInt(NS, K_AFF_MONTH_OF, -1);
                if (affMonthOf != month) { WageSaveStore.SetInt(NS, K_AFF_MONTH_OF, month); WageSaveStore.SetInt(NS, K_AFF_MONTH, 0); }
                int affMonth = WageSaveStore.GetInt(NS, K_AFF_MONTH, 0);
                double aff = WageGirlSystem.GetAffectionExact();
                bool belowLv3Floor = lv >= 3 && aff <= 50; // 下限保护：Lv3 好感≤50 不再扣
                if (affMonth < BuildConfig.WageGirlConnAffCostMonthlyCap && !belowLv3Floor)
                {
                    WageGirlSystem.SetAffectionExact(aff - 1);
                    WageSaveStore.SetInt(NS, K_AFF_MONTH, affMonth + 1);
                }
            }
            catch { }
            // 累计增益（硬顶判定；超顶不再累计但已产生收益不追回）
            try
            {
                double oldEst = (isContraband ? baseValue * 0.6 : isFake ? baseValue * 0.1 : baseValue) * 1.2;
                long gain = price - (long)Math.Round(oldEst);
                if (gain > 0)
                {
                    int g = WageSaveStore.GetInt(NS, K_GAIN_RUN, 0) + (int)gain;
                    if (g > GainCap(lv)) g = GainCap(lv); // 封顶
                    WageSaveStore.SetInt(NS, K_GAIN_RUN, g);
                }
            }
            catch { }
            // 走漏判定（Lv1 6% / Lv2 3% / Lv3 0%）：好感-3 + 当日额度清零
            int leak = LeakPct(lv);
            if (leak > 0 && Core.Rng.Next(100) < leak)
            {
                try
                {
                    double aff2 = WageGirlSystem.GetAffectionExact();
                    WageGirlSystem.SetAffectionExact(aff2 - 3);
                }
                catch { }
                WageSaveStore.SetInt(NS, K_USED_DAY, DailyCap(lv)); // 满额=当日拒收
                Core.LogMsg("[蛙娘门路] 走漏风声：好感-3，当日额度清零");
            }
            WageSaveStore.Flush();
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘门路] OnSettled 异常: " + ex.Message); }
    }

    // ---- 面板显示（§8.1-8）：今日门路 N/M · 本档剩余 K 件 ----
    internal static string StatusText()
    {
        try
        {
            int lv = Level(); if (lv < 1) return "";
            int used = UsedToday();
            int cap = DailyCap(lv);
            int runLeft = RunCap(lv) - WageSaveStore.GetInt(NS, K_USED_RUN, 0);
            if (runLeft < 0) runLeft = 0;
            return LangHelper.T("今日门路 " + used + "/" + cap + " · 本档剩余 " + runLeft + " 件", "Connections today " + used + "/" + cap + " · " + runLeft + " left this run");
        }
        catch { return ""; }
    }

    // ---- Lv3 直售免罪（§2.2-C / §8.1-6）：Prefix 用判定口 ----
    // 返回 true = 本次免罪并已消耗额度（调用方 return false 拦截 SecData）；false = 照常记罪，why 给原因
    // 注意：直售免罪与寄卖共用「每日额度 + 每局总量」池（§2.3 取舍语义），但犯罪硬顶单独计（K_CRIME_RUN）
    internal static bool TryDirectExempt(Il2Cpp.GameItem item, int crimeValue, out string why)
    {
        why = "";
        try
        {
            if (!BuildConfig.WageGirlConnEnabled) { why = "门路总开关已关"; return false; }
            int lv = Level();
            if (lv < 3) { why = "需要门路 Lv3"; return false; }
            if (item == null) { why = "无物品"; return false; }
            // 额度/总量/冷却 同 TryUse（直售免罪也占池）
            int used = UsedToday();
            if (used >= DailyCap(lv)) { why = "今日门路用完了"; return false; }
            int usedRun = WageSaveStore.GetInt(NS, K_USED_RUN, 0);
            if (usedRun >= RunCap(lv)) { why = "本档门路额度用尽了"; return false; }
            // 犯罪值硬顶：整档累计豁免 ≤ CrimeCap（30,000）
            int crime = WageSaveStore.GetInt(NS, K_CRIME_RUN, 0);
            if (crime + crimeValue > CrimeCap()) { why = "免罪额度到顶了"; return false; }
            WageSaveStore.SetInt(NS, K_USED_DAY, used + 1);
            WageSaveStore.SetInt(NS, K_USED_RUN, usedRun + 1);
            WageSaveStore.SetInt(NS, K_CRIME_RUN, crime + crimeValue);
            WageSaveStore.Flush();
            return true;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘门路] TryDirectExempt 异常: " + ex.Message); why = "免罪判定异常"; return false; }
    }

    // ---- §8.1-6 直售免罪 Prefix（只挂 CheckSoldContraband / CheckSoldChem；CheckSoldDirtyWater 签名待拆包确认后补） ----
    // 设计稿 §1.4 实锤：CheckSoldContraband 首句查 client==null → client.isNoContributeToEvidence 才记罪；
    //   我们不碰 client 字段（污染该客户全部交易），只在 Prefix 逐笔拦截：Lv3 + 额度/硬顶通过 → return false 跳过原方法。
    // 犯罪值估算口径：Prefix 拿不到 GetNegociatedValue，用 item.unitValue 近似（硬顶 3 万是整档量级闸，量级对即可）
    // 10-18 审阅复核 §4（07:09）：原签名参数名 item 与游戏方法实参名 gameItem 不一致 → Harmony 按参数名绑定失败，
    //   两条 Prefix 启动即挂载失败（IL Compile Error）、Lv3 免罪完全未生效。已改 gameItem（拆包侧已给 CheckSoldDirtyWater 签名）。
    internal static class ConnDirectExemptPatch
    {
        public static bool PrefixCheckSoldContraband(Il2Cpp.GameItem gameItem)
        {
            try
            {
                if (!BuildConfig.WageGirlConnEnabled) return true;
                if (Level() < 3) return true;
                if (gameItem == null) return true;
                int crime = 0; try { crime = (int)gameItem.unitValue; } catch { }
                if (crime < 1) crime = 1;
                string why = "";
                if (TryDirectExempt(gameItem, crime, out why)) { Core.LogMsg("[蛙娘门路] 直售免罪(违禁): " + gameItem.identifier + " 豁免" + crime); return false; }
                return true;
            }
            catch (Exception ex) { Core.LogMsg("[蛙娘门路] 直售免罪Prefix异常: " + ex.Message); return true; }
        }

        public static bool PrefixCheckSoldChem(Il2Cpp.GameItem gameItem)
        {
            try
            {
                if (!BuildConfig.WageGirlConnEnabled) return true;
                if (Level() < 3) return true;
                if (gameItem == null) return true;
                int crime = 0; try { crime = (int)gameItem.unitValue; } catch { }
                if (crime < 1) crime = 1;
                string why = "";
                if (TryDirectExempt(gameItem, crime, out why)) { Core.LogMsg("[蛙娘门路] 直售免罪(化学品): " + gameItem.identifier + " 豁免" + crime); return false; }
                return true;
            }
            catch (Exception ex) { Core.LogMsg("[蛙娘门路] 直售免罪Prefix异常: " + ex.Message); return true; }
        }
    }
}
