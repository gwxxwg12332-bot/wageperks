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
    // ===== 房东批发商库存随机化（拆包 [L1] 2.5.20.2：4件硬编码在闭包 b__31_0，Prefix 替换跳原生）=====
    public static bool PrefixLandlordWholesaleStock()
    {
        try
        {
            if (!IsActive()) return true; // 非职业走原生
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            int day = DeterministicSchedule.CurrentDay;
            int seed = (DeterministicSchedule.GetRunKey() + "_ld" + day).GetHashCode(); // 同日确定性（读档不换货），System.Random 不污染 Unity RNG
            var rng = new System.Random(seed);
            int n = rng.Next(3, 7); // 3-6 件
            string[] pool = {
                "cat_bar", "li_eat_snackbar", "toilet_paper", "neuroactive_perfume",
                "processed_meat", "small_morsel", "morsel", "processed_juice", "cup_noodle", "raw_meat", "small_raw_meat", "processed_cheese", "meat_scrap",
                "bottled_water", "soda_red", "energy_drink", "nudka", "red_beer",
                "bandage_item"
            };
            for (int i = 0; i < n; i++)
            {
                string id = pool[rng.Next(pool.Length)];
                try
                {
                    var item = Il2Cpp.ItemSpawner.Spawn(id);
                    if (item != null) ps.AddDirectSellingItemToTable(item);
                }
                catch { }
            }
            return false; // 跳过原生 4 件
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixLandlordWholesaleStock 异常: " + ex.Message); }
        return true;
    }
    // ===== 同行+供货商同来（拆包 [L1] 2.3.14：原生每周日 HandleSupplierClient→CreateSupplier；Postfix 追加 CreateMerchant）=====
    public static void PostfixHandleSupplierClient()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day <= 0 || day % 7 != 0) return; // 每周日（7/14/21…）
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var merchant = Il2Cpp.StoreClientList.CreateMerchant();
            if (merchant == null) return;
            scm.AddClient(merchant); // AddClient 追加不替换（拆包：同天可加多个）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleSupplierClient 异常: " + ex.Message); }
    }
    // ===== 房租改造：前99天免租，每100天收一次递增房租（第100天15000、第200天20000、第300天25000…）拆包 2.5.20 =====
    public static bool PrefixCheckRentDay()
    {
        try
        {
            if (!IsActive()) return true;
            int day = DeterministicSchedule.CurrentDay;
            int rent = RentForDay(day);
            if (rent <= 0) return false; // 非收租日：跳过原生每周收租
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            // A1 防重（09-17）：读档重放当天 CheckRentDay → 不重复扣租
            // 阶段1：标记入统一层——强退后标记随 ES3 回退，重放当天重新扣租，与现金回退一致（修强退逃租漏洞）
            if (WageSaveStore.GetInt(PERK_ID, "rent_paid_day", -1) == day) return false;
            bool enough = ps.playerCash >= rent;
            ps.playerCash -= rent; // 09-17 强制扣（现金不足也扣成负数——用户拍板）
            WageSaveStore.SetInt(PERK_ID, "rent_paid_day", day); // A1 防重记录（内存态，打烊落盘）
            if (enough)
                try { StoreUIManager.Instance.Notify(LangHelper.T("交租 " + rent + "（第" + day + "天）", "Rent due: " + rent + " (day " + day + ")"), "green"); } catch { }
            else
                try { StoreUIManager.Instance.Notify(LangHelper.T("房东来收 " + rent + "，现金不足，强制扣除！", "The landlord is here for " + rent + " - not enough cash, forcibly deducted!"), "red"); } catch { }
            return false; // 不走原生收租链（原生是每周涨租模式）
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PrefixCheckRentDay 异常: " + ex.Message); }
        return true;
    }

    // ===== 房东每周来卖货（拆包 2.5.20：原生仅 day18 一次；仿 XIAOWO 4.5.6 每周生成 LandlordWholesale）=====
    // 触发日：第4天起每周（day%7==4：第4/11/18/25…天）；day18 原生已有房东，mod 跳过避免双房东
    public static void PostfixHandleNormalClient()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day % 7 != 4 || day == 18) return; // 每周四（第4天起），day18 交给原生
            var scm = GetStoreClientManager();
            if (scm == null) return;
            var landlord = Il2Cpp.StoreClientUniqueList.LandlordWholesale();
            if (landlord == null) return;
            scm.AddClient(landlord);
            // 友商（merchant）陪房东一起来（拆包 09-10：原版收租日 CheckRentDay 内生成，被 PrefixCheckRentDay 拦截 → 改由每周房东日补生成；merchant 只买玩家非违禁品）
            try
            {
                var merchant = Il2Cpp.StoreClientList.CreateMerchant();
                if (merchant != null) { scm.AddClient(merchant); Core.LogMsg("[空间站鲁滨逊] 友商(merchant)已与房东同日加入队列"); }
            }
            catch (Exception mex) { Core.LogMsg("[空间站鲁滨逊] 生成友商异常: " + mex.Message); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixHandleNormalClient 异常: " + ex.Message); }
    }
    private static StoreClientManager GetStoreClientManager()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return null;
            return ps.storeClientManager;
        }
        catch { }
        return null;
    }


    // ===== 租金表（09-17 用户拍板：49/50 各2500、100→10000、150→20000、200→50000、250→70000、250后每50天封顶70000；不足强制扣负）=====
    private static int RentForDay(int day)
    {
        if (day == 49 || day == 50) return 2500;
        if (day == 100) return 10000;
        if (day == 150) return 20000;
        if (day == 200) return 50000;
        if (day == 250) return 70000;
        if (day > 250 && day % 50 == 0) return 70000;
        return 0; // 非收租日
    }
    private static int NextRentDay(int day)
    {
        if (day < 49) return 49;
        if (day < 50) return 50;
        if (day < 100) return 100;
        if (day < 150) return 150;
        if (day < 200) return 200;
        if (day < 250) return 250;
        return ((day / 50) + 1) * 50; // 250 后每 50 天
    }
    // ===== 租金显示同步为100天制（拆包 [L1]：日历/开始日/店内日历都读 dayUntilRent+rentValue）=====
    private static void SyncRentDisplay()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            ps.dayUntilRent = nextDay - day;
            ps.rentValue = RentForDay(nextDay);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] SyncRentDisplay 异常: " + ex.Message); }
    }

    // 日历收租行强制显示 + 自定义文案（拆包：temporaryRent==0 时原生隐藏；landlordTMP@0x138 / landlordNoticeBox@0x148）
    public static void PostfixOnCalendarButtonClicked(Il2Cpp.AdvCalendarUIManager __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return; // 房产已付清不显示
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            int due = nextDay - day;
            int rent = RentForDay(nextDay);
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.landlordTMP != null)
                __instance.landlordTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
            if (__instance.landlordNoticeBox != null) __instance.landlordNoticeBox.SetActive(true);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixOnCalendarButtonClicked 异常: " + ex.Message); }
    }

    // 店内日历（墙上日历 StoreCalendar.Update，拆包：原生用 GetRentDayCounter 每周显示）→ 覆盖为100天制（dayTMP@0x18）
    //
    // 09-23 性能：StoreCalendar.Update 是**每帧**调用的 Unity Update。原先每帧都要走
    //   NextRentDay / RentForDay / 多次 LangHelper.T / 4 次字符串拼接 —— 只为了最后那个
    //   "text 是否变化"的判空（每次 set 前先比较）。等于每帧白做一遍文案构造 + GC。
    // 处理：文案只依赖 day（nextDay/due/rent 都由 day 推出），故按 day 缓存；day 未变则直接复用。
    private static int _calTxtDay = int.MinValue;
    private static string _calTxt;
    public static void PostfixStoreCalendarUpdate(Il2Cpp.StoreCalendar __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return;
            int day = DeterministicSchedule.CurrentDay;
            // 同一天内文案恒定 → 只在跨天时重建（跨天/读档切换都会自然失效）
            if (day != _calTxtDay || _calTxt == null)
            {
                int nextDay = NextRentDay(day);
                int due = nextDay - day;
                int rent = RentForDay(nextDay);
                string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
                _calTxt = LangHelper.T("房租 " + rent + " " + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
                _calTxtDay = day;
            }
            if (__instance.dayTMP != null && __instance.dayTMP.text != _calTxt) // 防每帧重复 set
                __instance.dayTMP.text = _calTxt;
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStoreCalendarUpdate 异常: " + ex.Message); }
    }

    // 开始新一天界面收租提醒：强制显示 + 100天制文案（拆包：原生仅 GetRentDayCounter()≤2 才显示）
    public static void PostfixStartOfDayInitPanel(Il2Cpp.StartOfDayUIManager __instance)
    {
        try
        {
            if (!IsActive() || __instance == null) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return;
            if (ps.IsPropertyPaid) return;
            int day = DeterministicSchedule.CurrentDay;
            int nextDay = NextRentDay(day);
            int due = nextDay - day;
            int rent = RentForDay(nextDay);
            string dueTxt = due == 0 ? LangHelper.T("今天", "today") : (due == 1 ? LangHelper.T("明天", "tomorrow") : LangHelper.T(due + "天后", "in " + due + " days"));
            if (__instance.rentReminderTMP != null)
                __instance.rentReminderTMP.text = LangHelper.T("房租 " + rent + " 将于" + dueTxt + "收取", "Rent " + rent + " due " + dueTxt);
            if (__instance.rentReminder != null)
                __instance.rentReminder.SetActive(true);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStartOfDayInitPanel 异常: " + ex.Message); }
    }
}
