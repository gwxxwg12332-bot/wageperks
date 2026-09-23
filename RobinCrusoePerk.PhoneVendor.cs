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

    // ===== T1 概率联动说明（v4.2 验收第13条）=====
    // T1 需求池 = 静态 List<工厂委托>，GetRandomT1XxxClient 用 RNG.GetRandomInt 均匀随机选一个（ISIL 实锤）。
    // 委托身份在 IL2CPP 二进制层不可枚举（ISIL 读不到），无法精确把状态客户占比乘 1.2/0.85——
    // 需运行时枚举池内委托（UnityExplorer）或拆生成链更深层才能精确落点。本轮不实装，避免猜测。
    // ============================================================
    // 胡安(wanted7)/李北文(wanted6)供应商（09-12 并入鲁滨逊职业；09-13 屠夫→上层厨师→胡安：卖食物）
    // 复用原版 wanted2（屠夫实体改造）/ wanted6（李北文）通缉犯实体，不自建 identifier；
    // 电话端仿原版红魔鬼/GP矿业双通道（StorePhoneClient）；名片简化=到店直接解锁电话簿（phoneState=4）；
    // 拨号即叫货（跳过原版 PhoneDialogList 对话选项——wanted 无电话对话定义）
    // ============================================================
    private const long BUTCHER_PHONE_NUMBER = 8800;   // 胡安电话（原屠夫/上层厨师，避开原版 8376/8815/56371/4615/3319/51189）
    private const long LI_BEIWEN_PHONE_NUMBER = 8801; // 李北文电话
    private static int BUTCHER_FIRST_VISIT_DAY => BuildConfig.ButcherVisitDay;   // 胡安首次上门（CFG 可调） 0-based，第14天=13；原14永不命中）
    private static int LI_BEIWEN_FIRST_VISIT_DAY => BuildConfig.LiBeiwenVisitDay; // 李北文首次上门（CFG 可调）
    private static int CALL_TO_ARRIVE_DAYS => BuildConfig.CallArriveDays;        // 电话叫货到店天数（CFG 可调）
    private static int CALL_COOLDOWN_DAYS => BuildConfig.CallCooldownDays;         // 电话冷却天数（CFG 可调）
    // 09-13 胡安货单相关：到店 SetBarterOffer 食物报价
    private static readonly string[] CHEF_FOOD_IDS = { "raw_meat", "processed_meat", "fat_meat", "small_raw_meat", "morsel", "small_morsel", "processed_cheese", "meat_scrap", "cup_noodle", "processed_juice", "energy_drink" };

    // 屠夫/李北文供应商每日调度（v1.1.6 统一挂 PlayerStore.BeginDay 可靠挂点——PostfixOnBeginDay 调用：
    // 原挂 StoreClientManager.OnNewDay 触发时机不可靠，且跳天数工具不走 BeginDay/OnNewDay 无法测试；
    // 单入口保证冷却只扣一次）
    internal static void TickWantedSupplierDaily()
    {
        try
        {
            if (!IsActive()) return;
            WantedSupplierSchedule(StoreStation.GetDayCounter());
            TickWantedPhoneCooldown();
            TryRegisterWantedPhones();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TickWantedSupplierDaily 异常: " + ex.Message); }
    }

    // 每日调度：第14/21天固定排首次上门（PostfixOnBeginDay 调用）
    // 09-20 修：day >= 首访日（错过当天补排）+ per-id 防重（查 futuredClients 列表）
    internal static void WantedSupplierSchedule(int day)
    {
        try
        {
            if (!IsActive() || PlayerStore.Instance == null) return;
            // 胡安（wanted7）
            if (day >= BUTCHER_FIRST_VISIT_DAY && !WantedQueued("wanted7"))
                { QueueWantedClient("wanted7"); }
            // 李北文（wanted6）
            if (day >= LI_BEIWEN_FIRST_VISIT_DAY && !WantedQueued("wanted6"))
                { QueueWantedClient("wanted6"); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] WantedSupplierSchedule 异常: " + ex.Message); }
    }

    // 09-20 修：per-id 防重（进程内标记，不写 PlayerPrefs 防 default_run 污染）
    private static bool _wanted7Queued = false;
    private static bool _wanted6Queued = false;
    private static bool WantedQueued(string id)
    {
        try { return id == "wanted7" ? _wanted7Queued : _wanted6Queued; }
        catch { return false; }
    }
    internal static void ClearWantedQueued() { _wanted7Queued = false; _wanted6Queued = false; } // 读档清

    private static void QueueWantedClient(string id)
    {
        try
        {
            if (id == "wanted7") { } // wanted7=胡安 原版字典已有（拆包 09-13 [L1]）
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.storeClientManager == null) return;
            try { ps.storeClientManager.RemoveDuplicateClientsByIdentifier(id); } catch { } // 防原版随机 wanted 同天撞车
            if (id == "wanted7") _wanted7Queued = true; else if (id == "wanted6") _wanted6Queued = true;
			ps.QueueFuturClient(id, 0);
            Core.LogMsg("[空间站鲁滨逊] 已预约" + (id == "wanted7" ? "胡安" : "李北文") + "当天到店（" + id + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] QueueWantedClient(" + id + ") 异常: " + ex.Message); }
    }

    // 工厂创建即设显示名（Core 注册 Postfix StoreClientList.CreateWanted2/CreateWanted6）：
    // 拆包 09-13 [L1]：ResolveClientNames 在 CreateClientInstance 实例化时读 displayName，实例化后设则头顶名/横幅已锁定——
    // 提前到工厂 Postfix，让 displayName 在实例化前即为"胡安"，所有显示点（对话/电话簿/头顶/横幅）一致
    public static void PostfixCreateWanted6(Il2Cpp.StoreClient __result)
    {
        try { if (__result != null) __result.displayName = LangHelper.T("李北文", "Li Beiwen"); } catch { }
    }

    // 到店处理（Patches.PostfixSpecialNpcStartDialogue 调用）：解锁电话簿 + 上货
    internal static void WantedSupplierOnArrived(StoreClient client)
    {
        try
        {
            if (!IsActive() || client == null) return;
            string id = client.identifier;
            if (id == "wanted7") { try { client.displayName = LangHelper.T("胡安", "Juan"); } catch { } int visits = WageSaveStore.GetInt("juan", "visit", 0) + 1; WageSaveStore.SetInt("juan", "visit", visits); int lv = visits <= 2 ? 0 : (visits <= 5 ? 1 : 2); try { client.SetBarterOffer(BuildJuanOffer(lv)); } catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 胡安报价异常: " + ex.Message); } Core.LogMsg("[空间站鲁滨逊] 胡安到店#" + visits + " Lv" + lv + "（" + (lv == 0 ? "基础" : (lv == 1 ? "中档" : "高档")) + "）"); UnlockWantedPhone(BUTCHER_PHONE_NUMBER, LangHelper.T("胡安", "Juan")); }
            else if (id == "wanted6") { try { client.displayName = LangHelper.T("李北文", "Li Beiwen"); } catch { } UnlockWantedPhone(LI_BEIWEN_PHONE_NUMBER, LangHelper.T("李北文", "Li Beiwen")); AddLiBeiwenGoods(); }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] WantedSupplierOnArrived 异常: " + ex.Message); }
    }

    // 名片简化：到店直接解锁电话簿（phoneState=4），不发实体名片物品（自建物品 id 需注册 sprite/名称，有空物品风险）
    private static void UnlockWantedPhone(long number, string name)
    {
        try
        {
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null) { Core.LogMsg("[空间站鲁滨逊] 电话端未注册 " + number); return; }
            if ((int)pc.phoneState < 4)
            {
                pc.phoneState = (Il2Cpp.StorePhoneClient.PhoneState)4; // Regular：电话簿显示 + 可拨
                pc.cooldownDuration = CALL_COOLDOWN_DAYS;
                pc.dialedBefore = true; // 拆包 09-12 [L1]：ShownInPhoneBook=(state∈{4,5} && dialedBefore≠0)，原设 false 导致电话簿不显示
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "的电话已存入电话簿，拨号即可叫货", name + "'s number saved. Dial to order supplies."), "green"); } catch { }
                Core.LogMsg("[空间站鲁滨逊] " + name + " 电话簿解锁（" + number + "）");
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] UnlockWantedPhone 异常: " + ex.Message); }
    }

    // 胡安货单：到店 SetBarterOffer 食物报价（原版 CreateBarterFoodMultiOffer）
    // 胡安三档货单（09-13 用户拍板：随到店次数升级价值；Lv0 前2次 / Lv1 3-5次 / Lv2 6次+）
    // 拆包 09-13 [L1]：offerSets Action 模式 = DirectoryMaster.Item 创建 + PlayerStore.AddDirectSellingItemToTable 进交易台
    private static Il2Cpp.BarterOffer BuildJuanOffer(int lv)
    {
        var offer = Il2Cpp.BarterOfferList.CreateBarterFoodMultiOffer(); // 09-13 修复：用原版工厂构造（字段完整），防 IsBarterAcceptable NPE
        try
        {
            var ids = new System.Collections.Generic.List<string>();
            if (lv <= 0) { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("large_bottled_water"); }
            else if (lv == 1) { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("large_bottled_water"); }
            else { ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_meat"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("processed_cheese"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("galaxy_blend"); ids.Add("soda_red"); ids.Add("soda_red"); ids.Add("energy_drink"); ids.Add("energy_drink"); ids.Add("large_bottled_water"); ids.Add("large_bottled_water"); }
            var sysAction = new System.Action(() =>
            {
                foreach (var fid in ids)
                {
                    try { var item = Il2Cpp.DirectoryMaster.Item(fid); if (item != null) Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(item, false, false, false, 0); } catch { }
                }
            });
            var action = Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(sysAction);
            try { offer.offerSets.Clear(); } catch { } // 清掉原版3组食物报价，只留我们的档位货单
            offer.offerSets.Add(action);
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] BuildJuanOffer 异常: " + ex.Message); }
        return offer;
    }

    // 李北文货单：梦尘×5 + 奥克莫吸×5
    private static void AddLiBeiwenGoods()
    {
        try
        {
            int added = 0;
            for (int i = 0; i < 5; i++)
            {
                try { if (MerchantHelper.AddItemToCounter("dream_dust", 0, false) != null) added++; } catch { }
            }
            for (int i = 0; i < 5; i++)
            {
                try { if (MerchantHelper.AddItemToCounter("oxycodone_pill", 0, false) != null) added++; } catch { }
            }
            Core.LogMsg("[空间站鲁滨逊] 李北文已上货 " + added + " 件（梦尘×5+奥克莫吸×5）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] AddLiBeiwenGoods 异常: " + ex.Message); }
    }

    // ============================================================
    // 博士夜晚商店专属货（09-12 用户拍板，鲁滨逊职业内独立改动，不牵扯博士之友特性）：
    // 原版货不动；不再追加机器/储存（白天博士到访的机器/储存逻辑不动）
    // 1) 加卖食物水：罐头 processed_meat ×2 + 大瓶纯水 large_bottled_water ×1（防堆叠）
    // 2) 每次拜访独立 roll：3% 出受限神经模组 system_capped_neural_core（09-13 用户拍板：未受限已删）
    // ============================================================
    internal static void AddDoctorNightGoods()
    {
        try
        {
            if (!IsActive()) return; // 鲁滨逊职业专属
            int added = 0;

            // 食物水：鲁滨逊职业即有（不牵扯博士之友特性，柜台无同 id 才补，防堆叠）
            if (!HasGoodOnFront("processed_meat"))
            {
                for (int i = 0; i < 2; i++)
                {
                    try { if (MerchantHelper.AddItemToCounter("processed_meat", 0, false) != null) added++; } catch { }
                }
            }
            if (!HasGoodOnFront("large_bottled_water"))
            {
                try
                {
                    GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 09-20 设计稿：带水大瓶（删 DirectoryMaster.Item+AddWater 链——工厂产物 AddWater 静默失败 → 空瓶）
                    if (hq != null) { if (MerchantHelper.AddItemToCounter(hq, 0, false) != null) added++; }
                }
                catch { }
            }

            // 神经模组概率：落实到博士之友特性（09-12 用户拍板：特性激活才 roll）
            // 标准版：独立 roll 3% 受限；09-13 用户拍板：未受限神经模组全删（不再生成）
            // 硬爽版：50% 受限（09-12 用户拍板；防堆叠保留）
            if (DrJacksonFriendPerk.IsActive())
            {
                float neuralChance = (BuildConfig.HardMode ? BuildConfig.NeuralChanceHard : BuildConfig.NeuralChanceNormal) / 100f;
            if (UnityEngine.Random.value < neuralChance)
                {
                    if (!HasGoodOnFront("system_capped_neural_core"))
                        try { if (MerchantHelper.AddItemToCounter("system_capped_neural_core", 0, false) != null) added++; } catch { }
                }
            }

            if (added > 0) Core.LogMsg("[空间站鲁滨逊] 博士夜晚商店加货 " + added + " 件（食物水/神经模组）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] AddDoctorNightGoods 异常: " + ex.Message); }
    }

    // 柜台是否已有同 id 的货（夜晚商店加货防堆叠；与 DrJacksonFriendPerk.CounterHasOnFront 同逻辑）
    private static bool HasGoodOnFront(string itemId)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return false;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && it.identifier == itemId) return true;
            }
        }
        catch { }
        return false;
    }

    // 柜台同 id 数量（博士夜晚商店保护器补足用）
    private static int CountGoodOnFront(string itemId)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null || em.frontInvinvElement.items == null) return 0;
            int n = 0;
            foreach (var it in em.frontInvinvElement.items)
            {
                if (it != null && it.identifier == itemId) n++;
            }
            return n;
        }
        catch { }
        return 0;
    }

    // 电话端注册（Core 注册 Postfix StorePhoneClient.InitPhoneClientDict）
    public static void PostfixInitPhoneClientDict(Il2CppSystem.Collections.Generic.Dictionary<long, Il2Cpp.StorePhoneClient> __result)
    {
        try
        {
            if (!IsActive() || __result == null) return;
            if (__result.ContainsKey(BUTCHER_PHONE_NUMBER) && __result.ContainsKey(LI_BEIWEN_PHONE_NUMBER)) return; // 已注册
            if (!__result.ContainsKey(BUTCHER_PHONE_NUMBER))
            {
                var butcher = new Il2Cpp.StorePhoneClient();
                butcher.phoneClientType = Il2Cpp.StorePhoneClient.PhoneClientType.Supplier;
                butcher.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None;
                butcher.displayName = LangHelper.T("胡安", "Juan");
                butcher.locID = "name_juan";
                butcher.dialogFuncId = "";
                butcher.cooldownDuration = CALL_COOLDOWN_DAYS;
                butcher.currentCooldown = 0;
                butcher.dialedBefore = false;
                __result.Add(BUTCHER_PHONE_NUMBER, butcher);
            }
            if (!__result.ContainsKey(LI_BEIWEN_PHONE_NUMBER))
            {
                var libw = new Il2Cpp.StorePhoneClient();
                libw.phoneClientType = Il2Cpp.StorePhoneClient.PhoneClientType.Supplier;
                libw.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None;
                libw.displayName = LangHelper.T("李北文", "Li Beiwen");
                libw.locID = "name_li_bei_wen";
                libw.dialogFuncId = "";
                libw.cooldownDuration = CALL_COOLDOWN_DAYS;
                libw.currentCooldown = 0;
                libw.dialedBefore = false;
                __result.Add(LI_BEIWEN_PHONE_NUMBER, libw);
            }
            Core.LogMsg("[空间站鲁滨逊] 电话端已注册 胡安8800/李北文8801");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixInitPhoneClientDict 异常: " + ex.Message); }
    }

    // 兜底补注册：InitPhoneClientDict 若在特性未激活时跑过，每日结算用 PlayerStore.PhoneClientDict 补
    private static void TryRegisterWantedPhones()
    {
        try
        {
            if (!IsActive()) return;
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.PhoneClientDict == null) return;
            if (!ps.PhoneClientDict.ContainsKey(BUTCHER_PHONE_NUMBER) || !ps.PhoneClientDict.ContainsKey(LI_BEIWEN_PHONE_NUMBER))
            {
                PostfixInitPhoneClientDict(ps.PhoneClientDict);
            }
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TryRegisterWantedPhones 异常: " + ex.Message); }
    }
    // ===== 09-13 修复：举报/击毙 wanted 后电话停用（不能再叫货）=====
    private static readonly System.Collections.Generic.HashSet<long> _wantedPhoneRemoved = new System.Collections.Generic.HashSet<long>();
    private static void MarkWantedPhoneRemoved(string identifier)
    {
        try
        {
            // 09-13：8800=胡安（wanted7，原版常客）；8801=李北文（wanted6）。屠夫 wanted2 已下线电话（原版随机出现，mod 不管）
            long num;
            string label;
            if (identifier == "wanted7") { num = BUTCHER_PHONE_NUMBER; label = "胡安"; }
            else if (identifier == "wanted6") { num = LI_BEIWEN_PHONE_NUMBER; label = "李北文"; }
            else return;
            _wantedPhoneRemoved.Add(num);
            try
            {
                var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(num);
                if (pc != null) pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.None; // 电话簿消失 + 拨号空号
            }
            catch { }
            Core.LogMsg("[空间站鲁滨逊] " + label + " 已被举报/击毙，电话停用");
        }
        catch { }
    }
    // 举报（Core 注册 Postfix WantedElement.OnArrested——通缉 UI 举报回调，identifier 字段实锤）
    public static void PostfixWantedElementOnArrested(Il2Cpp.WantedElement __instance)
    {
        try
        {
            if (__instance == null || __instance.identifier == null) return;
            if (__instance.identifier == "wanted6") // 李北文可举报（通缉犯）；胡安 wanted7 保持原版行为
                MarkWantedPhoneRemoved(__instance.identifier);
        }
        catch { }
    }
    // 击毙（Core 注册 Postfix AugHelper.CleanupKill——枪战对话击杀处理；拆包 09-13 [L1]：
    // 客户击杀=枪战对话（SecShootoutDialog），KillCurrentEntity 不在客户链（仅 Debug/Survival 调）。
    // 客户到店 id 由 PrefixStoreUIManagerOnGenericArrived 记录，击杀时用它标记停用电话）
    private static string _lastArrivedClientId = null;
    public static void PrefixStoreUIManagerOnGenericArrived(string id)
    {
        try { _lastArrivedClientId = id; } catch { }
    }
    public static void PostfixAugHelperCleanupKill()
    {
        try
        {
            string id = _lastArrivedClientId;
            if (id == "wanted7" || id == "wanted6")
                MarkWantedPhoneRemoved(id);
        }
        catch { }
    }

    // 电话簿显示名修正（Core 注册 Postfix ContactElement.OnInit）：原生读 locID，覆盖为 displayName（胡安/李北文）
    public static void PostfixOnContactInit(Il2Cpp.ContactElement __instance, long targetNumber)
    {
        try
        {
            if (__instance == null || __instance.titleTMP == null) return;
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(targetNumber);
            if (pc == null || string.IsNullOrEmpty(pc.displayName)) return;
            __instance.titleTMP.text = pc.displayName;
        }
        catch { }
    }

    // 接听拦截（Core 注册 Prefix PhoneUIManager.WillAnswerCall）：未解锁 → 空号提示
    public static bool PrefixWillAnswerCall(long number)
    {
        try
        {
            if (!IsActive()) return true;
            if (number != BUTCHER_PHONE_NUMBER && number != LI_BEIWEN_PHONE_NUMBER) return true; // 非我们号码放行原版
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null || (int)pc.phoneState < 4)
            {
                try { Il2Cpp.PhoneUIManager.Instance.NoNumber(); } catch { }
                return false; // 未解锁：空号
            }
            return true; // 已解锁：放行原版
        }
        catch { return true; }
    }

    // 拨号即叫货（Core 注册 Prefix PhoneUIManager.StartPhoneDialog）：接通瞬间自动排期 + 冷却，拦掉原版对话
    public static bool PrefixStartPhoneDialog(long currentNumber)
    {
        try
        {
            if (!IsActive()) return true;
            string id = null, name = null;
            if (currentNumber == BUTCHER_PHONE_NUMBER) { id = "wanted7"; name = LangHelper.T("胡安", "Juan"); }
            else if (currentNumber == LI_BEIWEN_PHONE_NUMBER) { id = "wanted6"; name = LangHelper.T("李北文", "Li Beiwen"); }
            else return true;
            if (id == "wanted7") { }
            // 09-13 修复：举报/击毙后电话停用（不能再叫货）
            if (_wantedPhoneRemoved.Contains(currentNumber))
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "已被举报/击毙，无法再叫货", name + " has been reported/killed, cannot order."), "red"); } catch { }
                try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { }
                return false;
            }
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(currentNumber);
            if (pc != null && (pc.currentCooldown > 0 || (int)pc.phoneState == 5))
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T(name + "还在忙，过几天再打", name + " is busy, call again in a few days."), "red"); } catch { }
                try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { } // 清拨号状态，防卡死（拆包 09-12 [L1]）
                return false; // 冷却中：占线
            }
            PlayerStore ps = PlayerStore.Instance;
            if (ps == null || ps.storeClientManager == null) return true;
            try { ps.storeClientManager.RemoveDuplicateClientsByIdentifier(id); } catch { }
            ps.QueueFuturClient(id, CALL_TO_ARRIVE_DAYS); // 排 2 天到店
            if (pc != null)
            {
                pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.Cooldown; // 冷却
                pc.currentCooldown = CALL_COOLDOWN_DAYS;
                pc.cooldownDuration = CALL_COOLDOWN_DAYS;
                pc.dialedBefore = true;
            }
            try { StoreUIManager.Instance.Notify(LangHelper.T("已预约" + name + " " + CALL_TO_ARRIVE_DAYS + " 天后到店", name + " will arrive in " + CALL_TO_ARRIVE_DAYS + " days."), "green"); } catch { }
            Core.LogMsg("[空间站鲁滨逊] 电话叫货成功：" + name + " " + CALL_TO_ARRIVE_DAYS + " 天后到店");
            try { Il2Cpp.PhoneUIManager.Instance.StopCall(); } catch { } // 清拨号状态，防卡死（拆包 09-12 [L1]：StartCalling 已挂 0x64=1，原版由 StartDialogue 收尾，被拦需 StopCall 清理）
            return false; // 拦掉原版对话显示
        }
        catch { return true; }
    }

    // 电话冷却自管（PostfixOnNewDay 调用；原版 OnNewDay 是否遍历递减不确定，自己维护最稳）
    private static void TickWantedPhoneCooldown()
    {
        try
        {
            if (!IsActive()) return;
            TickOnePhoneCooldown(BUTCHER_PHONE_NUMBER);
            TickOnePhoneCooldown(LI_BEIWEN_PHONE_NUMBER);
        }
        catch { }
    }
    private static void TickOnePhoneCooldown(long number)
    {
        try
        {
            var pc = Il2Cpp.StorePhoneClient.GetPhoneClientByNumber(number);
            if (pc == null) return;
            if (pc.currentCooldown > 0)
            {
                pc.currentCooldown--;
                if (pc.currentCooldown <= 0 && (int)pc.phoneState == 5)
                    pc.phoneState = Il2Cpp.StorePhoneClient.PhoneState.Regular; // 冷却结束恢复可拨
            }
        }
        catch { }
    }
}
