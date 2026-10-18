using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// B2-R 蛙哥服务面板 · 原生 WildUI 改造（2026-10-14 派活单 B2-R）
// ------------------------------------------------------------
// 用户拍板三点：
//   ① 蛙哥电话 1005（拨通即开原生服务面板）
//   ② 面板重做 = 王尔德原生查看服务面板（WildUIManager），
//      21 特性 + 吞噬季 = 22 条注入 networkUpgrade(0x150)，
//      移除自建 B2ServicePanel（CustomUIManager 440×780）
//   ③ 购买（未拥有）→ 附赠开局物品（已拥有不重复附赠）
// ------------------------------------------------------------
// 拆包实锤（B2-R 探查轮）：
//   WildUIManager.OnElementClicked = networkUpgrade(0x150).TryGetValue(id)
//     → 右侧详情/按钮态全由字典驱动（WildUIManager.txt ISIL OnElementClicked）
//   OnUnlockClicked = 原生支付（wildFavor 直扣→不够清零+playerCash 补差）
//     → NetworkUpgrade.Unlock() → state=2 + UnlockActions[id] 执行
//   InitUpgradeDict 在 PlayerStore .ctor 每档调用 → 注入自然持久化（0x150 整字典 ES3）
//   networkElements 为 prefab 静态槽，注入不自动出现 → 动态克隆 NetworkElement
//   NetworkElement.UpdateElement/OnEnable 按 id 自刷显示（id 设对即可）
// ============================================================
internal static class B2WildPanel
{
    private const string PREFIX = "b2_";
    internal const string CANNIBAL_ID = "b2_cannibal";
    internal const int CANNIBAL_PRICE = 3000;
    internal const long WAGE_PHONE_NUMBER = 1005;

    // ===== 服务条目（10-15 用户拍板：养蛊机/AI/保护器从博士供货改服务解锁 + 流浪者=服务购买效果） =====
    // 前置困难（价格+混合）：蛙哥牛逼 Lv2 + 蛙娘好感≥40 + 价格阶梯
    internal const string SVC_WANDERER = "b2_svc_wanderer";   // 10-17 重回第一天已迁流浪者 Lv3（本常量仅作 Lv3 价格锚，不再有服务条目）
    internal const string SVC_GU = "b2_svc_gu";               // 养蛊机（一次性）
    internal const string SVC_AI = "b2_svc_ai";               // AI 生成器（一次性）
    internal const string SVC_PROTECTOR = "b2_svc_protector"; // 保护器核心（可重复购买）
    internal const string SVC_PROTECTOR_CHANCE = "b2_svc_protector_chance"; // 保护器出现概率提升（可重复购买，每级+10%）
    private const int SVC_PRICE_WANDERER = 20000;  // 10-15 重定价（锚=充电器3=50000）：流浪者·重回第一天
    private const int SVC_PRICE_GU = 30000;        // 养蛊机（永久机器，3天融合×0.6）
    private const int SVC_PRICE_AI = 40000;        // AI 生成器（永久机器，每天抽卡核心产线）
    private const int SVC_PRICE_PROTECTOR = 3000; // 10-15 修正：消耗品定价对齐原版消耗带（原版顶500×6），防AI产线运营成本崩坏
    private const int SVC_PRICE_PROTECTOR_CHANCE = 3000; // 10-15 修正：概率服务=保护器价对齐（白嫖回本≈10次到店，无套利）

    // ===== 10-15 许可/充电器搬进电话簿（用户拍板：也搬进电话簿；柜台一级保留+合成升级不变） =====
    internal const string SVC_PERMIT1 = "b2_svc_permit1";
    internal const string SVC_PERMIT2 = "b2_svc_permit2";
    internal const string SVC_PERMIT3 = "b2_svc_permit3";
    internal const string SVC_CHARGER1 = "b2_svc_charger1";
    internal const string SVC_CHARGER2 = "b2_svc_charger2";
    internal const string SVC_CHARGER3 = "b2_svc_charger3";
    // 10-18 蛙娘门路（设计稿《假货原价与违禁品免罪》§8.1-7）：独立升级线，等级存 B2ServiceStore.owned_蛙娘门路
    internal const string SVC_CONN1 = "b2_svc_conn1";
    internal const string SVC_CONN2 = "b2_svc_conn2";
    internal const string SVC_CONN3 = "b2_svc_conn3";
    private const int SVC_PRICE_PERMIT1 = 10000;   // 与柜台一级价一致
    private const int SVC_PRICE_PERMIT2 = 30000;
    private const int SVC_PRICE_PERMIT3 = 50000;
    private const int SVC_PRICE_CHARGER1 = 5000;   // 与柜台一级价一致
    private const int SVC_PRICE_CHARGER2 = 25000;
    private const int SVC_PRICE_CHARGER3 = 50000;
    // 10-18 蛙娘门路升级价（§5.1：1500/6000/20000；定价锚=本作最高服务价8000，Lv3=2.5倍=终局投资）
    private const int SVC_PRICE_CONN1 = 1500;
    private const int SVC_PRICE_CONN2 = 6000;
    private const int SVC_PRICE_CONN3 = 20000;
    private const int SVC_AFFECTION_GATE = 40; // 好感门槛

    private sealed class WildEntry
    {
        internal string Id;
        internal string PerkId;    // 特性条目
        internal string ServiceId; // 服务条目（null=特性/吞噬季）
        internal bool IsCannibal => PerkId == null && ServiceId == null;
        internal bool IsService => ServiceId != null;
    }

    private static readonly WildEntry[] Entries = BuildEntries();
    private static bool _cloned = false;          // 进程级防重复克隆
    private static bool _actionsRegistered = false; // 防 UnlockActions 重复注册

    private static WildEntry[] BuildEntries()
    {
        var list = new List<WildEntry>();
        foreach (var perk in CustomStartingPerks.All)
            list.Add(new WildEntry { Id = PREFIX + perk.Id, PerkId = perk.Id });
        list.Add(new WildEntry { Id = CANNIBAL_ID, PerkId = null });
        list.Add(new WildEntry { Id = SVC_GU, ServiceId = "gu_machine" });
        list.Add(new WildEntry { Id = SVC_AI, ServiceId = "ai_generator" });
        list.Add(new WildEntry { Id = SVC_PROTECTOR, ServiceId = "protector" });
        list.Add(new WildEntry { Id = SVC_PROTECTOR_CHANCE, ServiceId = "protector_chance" });
        // 10-15 许可/充电器进电话簿（3级各一条，柜台一级+合成升级保留）
        list.Add(new WildEntry { Id = SVC_PERMIT1, ServiceId = "permit1" });
        list.Add(new WildEntry { Id = SVC_PERMIT2, ServiceId = "permit2" });
        list.Add(new WildEntry { Id = SVC_PERMIT3, ServiceId = "permit3" });
        list.Add(new WildEntry { Id = SVC_CHARGER1, ServiceId = "charger1" });
        list.Add(new WildEntry { Id = SVC_CHARGER2, ServiceId = "charger2" });
        list.Add(new WildEntry { Id = SVC_CHARGER3, ServiceId = "charger3" });
        // 10-18 蛙娘门路（等级制：同一升级线三档，独立于蛙娘卡）
        list.Add(new WildEntry { Id = SVC_CONN1, ServiceId = "conn1" });
        list.Add(new WildEntry { Id = SVC_CONN2, ServiceId = "conn2" });
        list.Add(new WildEntry { Id = SVC_CONN3, ServiceId = "conn3" });
        return list.ToArray();
    }

    private static WildEntry FindEntry(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith(PREFIX)) return null;
        foreach (var e in Entries) if (e.Id == id) return e;
        return null;
    }

    private static bool IsB2(NetworkUpgrade u) => u != null && FindEntry(u.id) != null;

    internal static CustomStartingPerk FindPerk(string perkId)
    {
        if (string.IsNullOrEmpty(perkId)) return null;
        foreach (var p in CustomStartingPerks.All) if (p.Id == perkId) return p;
        return null;
    }

    // ================= ① 注入 0x150（PlayerStore .ctor 每档跑） =================
    public static void PostfixInitUpgradeDict(ref Il2CppSystem.Collections.Generic.Dictionary<string, NetworkUpgrade> __result)
    {
        try
        {
            if (__result == null) return;
            foreach (var e in Entries)
            {
                if (__result.ContainsKey(e.Id)) continue;
                var u = new NetworkUpgrade(e.Id);
                u.subtitle = "";
                u.cost = 0;                      // 定价走 Patch GetCost
                u.isRepeatable = e.IsCannibal || e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine";   // 吞噬季/保护器/概率提升/养蛊机：可重复购买
                u.cooldownCurrent = 0;
                u.cooldownDuration = 0;
                u.prerequisite = null;
                u.lockInDemo = false;
                u.state = 0;
                __result.Add(e.Id, u);
            }
            RegisterUnlockActions();
            Core.LogMsg("[B2R] networkUpgrade(0x150) 注入 " + Entries.Length + " 条（21 特性 + 吞噬季）");
            // FIX3 保险：数据注入若晚于面板打开（极端时序），面板已开时补一次克隆+分流刷新（幂等：_cloned 防重克隆）
            try
            {
                var wui = WildUIManager.Instance;
                if (wui != null && wui.panel != null && wui.panel.gameObject.activeSelf)
                    MelonCoroutines.Start(ApplyWageModeDelayed(wui));
            }
            catch (Exception ex) { Core.LogMsg("[B2R] 注入后面板刷新触发异常: " + ex.Message); }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 注入异常: " + ex.Message); }
    }

    // 读档兜底：PlayerStore..ctor 每档重建 0x150 字典（PatchRegistryTable.cs:268），缺 b2_ 条目时补（幂等）
    internal static void EnsureInjected()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            var dict = ps.networkUpgrade;
            if (dict == null) return;
            bool added = false;
            foreach (var e in Entries)
            {
                if (dict.ContainsKey(e.Id)) continue;
                var u = new NetworkUpgrade(e.Id);
                u.subtitle = "";
                u.cost = 0;
                u.isRepeatable = e.IsCannibal || e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine"; // 对齐 :137（审阅 §3.4：漏 protector_chance 防未来踩坑；10-18 加养蛊机）
                u.cooldownCurrent = 0;
                u.cooldownDuration = 0;
                u.prerequisite = null;
                u.lockInDemo = false;
                u.state = 0;
                dict.Add(e.Id, u);
                added = true;
            }
            RegisterUnlockActions();
            if (added) Core.LogMsg("[B2R] 读档兜底注入 " + Entries.Length + " 条（缺字典条目）");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] EnsureInjected 异常: " + ex.Message); }
    }

    public static void RegisterUnlockActions()
    {
        try
        {
            if (_actionsRegistered) return;
            var dict = Il2Cpp.NetworkUpgradeList.UnlockActions;
            if (dict == null) { Core.LogMsg("[B2R] UnlockActions 为空，跳过注册"); return; }
            foreach (var e in Entries)
            {
                var act = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => OnUnlock(e)));
                dict[e.Id] = act; // 索引器覆盖，防重复 Add 抛异常
            }
            _actionsRegistered = true;
            Core.LogMsg("[B2R] UnlockActions 注册 " + Entries.Length + " 条");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] UnlockActions 注册异常: " + ex.Message); }
    }

    // ================= ② Patch 显示 / 定价 / 按钮态 =================
    public static void PostfixGetCost(NetworkUpgrade __instance, ref int __result)
    {
        try { if (IsB2(__instance)) __result = B2Cost(__instance); }
        catch (Exception ex) { Core.LogMsg("[B2R] GetCost 异常: " + ex.Message); }
    }

    private static int B2Cost(NetworkUpgrade u)
    {
        var e = FindEntry(u.id);
        if (e == null) return 0;
        if (e.IsService) return ServiceCost(e); // 服务条目（机器/流浪者）
        if (e.IsCannibal)
        {
            return B2ServiceStore.IsCannibalOwned ? 0 : CANNIBAL_PRICE; // 已购再点 = 切换模式（免费）
        }
        var perk = FindPerk(e.PerkId);
        if (perk == null) return 0;
        int lv = B2ServiceStore.GetLevel(e.PerkId);
        if (perk.Cost < 0) // 负面：已消除/已净化免费占位，未处理显示消除价
        {
            if (B2ServiceStore.IsEliminated(e.PerkId) || B2ServiceStore.IsPurified(e.PerkId)) return 0;
            if (e.PerkId == InfamousPerk.PerkId) return 0; // 声名狼藉特殊：分势力恢复（免费开窗）
            if (e.PerkId == ThiefMagnetPerk.PerkId) return 5000; // 招贼体质固定消除价（B2 定价沿用）
            return WageBrother.PriceForCost(perk.Cost);
        }
        if (lv >= 3) return 0; // 满级
        if (lv == 0) return BuyPrice(perk);
        // 10-17 用户拍板：重回第一天迁流浪者 Lv3（对齐原服务价 20000，防免费白嫖清档）
        if (e.PerkId == WandererPerk.PerkId && lv == 2) return SVC_PRICE_WANDERER;
        return UpgradePrice(perk, lv + 1);
    }

    internal static int BuyPrice(CustomStartingPerk perk)
    {
        int c = perk.Cost;
        if (c <= 0) return 0;
        return c * 500;
    }

    internal static int UpgradePrice(CustomStartingPerk perk, int toLevel)
    {
        int baseP = BuyPrice(perk);
        if (baseP <= 0) return 0;
        return toLevel == 2 ? baseP : baseP * 2;
    }

    // ===== 服务条目：定价 / 标题 / 副标题 / 描述 / 解锁 / 发放（10-15） =====
    private static int ServiceCost(WildEntry e)
    {
        if (e.ServiceId == "protector") return SVC_PRICE_PROTECTOR; // 可重复购买
        if (e.ServiceId == "protector_chance") return SVC_PRICE_PROTECTOR_CHANCE; // 可重复购买（概率提升）
        if (B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id)) return 0; // 补发态免费（已扣费未到货）
        if (e.ServiceId == "gu_machine") return SVC_PRICE_GU; // 10-18 玩家拍板：养蛊机可重复购买——恒收费（不享"已购 0 元"）
        if (B2ServiceStore.GetLevel(e.Id) >= 1) return 0; // 一次性已购
        switch (e.ServiceId)
        {
            case "gu_machine": return SVC_PRICE_GU;
            case "ai_generator": return SVC_PRICE_AI;
            case "permit1": return SVC_PRICE_PERMIT1;
            case "permit2": return SVC_PRICE_PERMIT2;
            case "permit3": return SVC_PRICE_PERMIT3;
            case "charger1": return SVC_PRICE_CHARGER1;
            case "charger2": return SVC_PRICE_CHARGER2;
            case "charger3": return SVC_PRICE_CHARGER3;
            // 10-18 门路：等级在 B2ServiceStore.owned_蛙娘门路（e.Id 无键），已购判定用 WageGirlConn.Level()
            case "conn1": return WageGirlConn.Level() >= 1 ? 0 : SVC_PRICE_CONN1;
            case "conn2": return WageGirlConn.Level() >= 2 ? 0 : SVC_PRICE_CONN2;
            case "conn3": return WageGirlConn.Level() >= 3 ? 0 : SVC_PRICE_CONN3;
        }
        return 0;
    }

    private static string ServiceTitle(WildEntry e)
    {
        // 10-18 §5 D：存量补发态按钮文本（已解锁未到货 → 「📦 补发到柜台」）
        // P2-12（审阅 20261018）：补发态保留原服务名（switch 不中断），玩家可分辨许可/充电器等级
        string baseTitle;
        switch (e.ServiceId)
        {
            case "gu_machine": baseTitle = LangHelper.T("蛙哥养蛊机", "Wage's Swarm Forge"); break;
            case "ai_generator": baseTitle = LangHelper.T("蛙哥不稳定AI生成器", "Wage's Unstable AI Generator"); break;
            case "protector": baseTitle = LangHelper.T("蛙哥保护器核心", "Wage's Protector Core"); break;
            case "protector_chance": baseTitle = LangHelper.T("蛙哥保护器·随机出现率", "Protector Spawn Chance"); break;
            case "permit1": baseTitle = LangHelper.T("蛙哥的许可（一级）", "Wage's Permit (Tier 1)"); break;
            case "permit2": baseTitle = LangHelper.T("蛙哥的许可（二级）", "Wage's Permit (Tier 2)"); break;
            case "permit3": baseTitle = LangHelper.T("蛙哥的许可（三级）", "Wage's Permit (Tier 3)"); break;
            case "charger1": baseTitle = LangHelper.T("蛙哥充电器（一级）", "Wage's Charger (Tier 1)"); break;
            case "charger2": baseTitle = LangHelper.T("蛙哥充电器（二级）", "Wage's Charger (Tier 2)"); break;
            case "charger3": baseTitle = LangHelper.T("蛙哥充电器（三级）", "Wage's Charger (Tier 3)"); break;
            // 10-18 门路：标题带当前等级（升级语义）
            case "conn1": baseTitle = LangHelper.T("蛙娘 · 门路 Lv1（以假乱真）", "Wage Girl · Connections Lv1 (Fake at full price)"); break;
            case "conn2": baseTitle = LangHelper.T("蛙娘 · 门路 Lv2（白手套）", "Wage Girl · Connections Lv2 (Gloves)"); break;
            case "conn3": baseTitle = LangHelper.T("蛙娘 · 门路 Lv3（锅她背）", "Wage Girl · Connections Lv3 (She takes the fall)"); break;
            default: baseTitle = e.Id; break;
        }
        if (e.ServiceId != "protector" && e.ServiceId != "protector_chance"
            && B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id))
            return LangHelper.T("📦 补发到柜台 · " + baseTitle, "📦 Redeliver · " + baseTitle);
        return baseTitle;
    }

    private static string ServiceSubtitle(WildEntry e)
    {
        string s = LangHelper.T("服务", "Service");
        if (e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine") s += LangHelper.T(" · 可重复", " · repeatable"); // 10-18 加养蛊机
        return s;
    }

    private static string ServiceDescription(WildEntry e)
    {
        string req = LangHelper.T("\n\n解锁条件：蛙哥牛逼 Lv2 + 蛙娘好感≥" + SVC_AFFECTION_GATE, "\n\nRequires: WageBrother Lv2 + affection >= " + SVC_AFFECTION_GATE);
        switch (e.ServiceId)
        {
            case "gu_machine":
                return LangHelper.T("传奇当铺主蛙哥留下的炼蛊机器：打烊充能（3天）后自动炼蛊（三属性之和×0.6，上限150%）；舱内老鼠自动互食。可重复购买，每台独立炼蛊。", "A forging machine left by Wage: 3-day charge then forge (sum x0.6, cap 150%); rats in bay devour each other. Repeatable, each forge works independently.") + req;
            case "ai_generator":
                return LangHelper.T("不稳定 AI 生成器：打烊自动抽卡；装保护器=稳定×1.2，不装=倍率随机 0.05~0.8（多数亏）。", "Unstable AI generator: auto-draw at close; with Protector=stable x1.2, without=random 0.05~0.8 (usually a loss).") + req;
            case "protector":
                return LangHelper.T("稳定器：放入 AI 生成器舱即稳定版（100%成功，属性×1.2），每次抽卡消耗 1 个，可重复购买。", "Stabilizer: place in AI generator bay for stable mode (100%, x1.2), 1 per draw, repeatable.") + req;
            case "protector_chance":
                return LangHelper.T("提升蛙哥到店时随机带保护器的概率：每级 +10%（初始 30%，上限 90%），当前 " + (30 + WageSaveStore.GetInt(WageBrother.CLIENT_ID, WageBrother.PROTECTOR_SPAWN_KEY, 0) * 10) + "%。可重复购买。", "Increase Wage Brother's random protector carry chance: +10% per tier (base 30%, cap 90%), currently " + (30 + WageSaveStore.GetInt(WageBrother.CLIENT_ID, WageBrother.PROTECTOR_SPAWN_KEY, 0) * 10) + "%. Repeatable.") + req;
            case "permit1":
                return LangHelper.T("蛙哥的许可（一级）：每晚外出次数 +1（可叠加），拾荒仍可能受伤。购买后直接上柜台。", "Wage's Permit (Tier 1): +1 night outing (stacks), scavenging may still injure. Placed on counter after purchase.") + req;
            case "permit2":
                return LangHelper.T("蛙哥的许可（二级）：每晚外出次数 +1（可叠加），拾荒受伤概率减半。购买后直接上柜台。", "Wage's Permit (Tier 2): +1 night outing (stacks), injury chance halved. Placed on counter after purchase.") + req;
            case "permit3":
                return LangHelper.T("蛙哥的许可（三级）：每晚外出次数 +1（可叠加），拾荒时完全不会受伤。购买后直接上柜台。", "Wage's Permit (Tier 3): +1 night outing (stacks), complete immunity to scavenging injuries. Placed on counter after purchase.") + req;
            case "charger1":
                return LangHelper.T("蛙哥充电器（一级）：每晚自动给背包所有电池充 3 点电量（拥有即生效，不消耗）。", "Wage's Charger (Tier 1): +3 charge to all backpack batteries nightly (persistent).") + req;
            case "charger2":
                return LangHelper.T("蛙哥充电器（二级）：每晚自动给背包所有电池充 6 点电量（拥有即生效，不消耗）。", "Wage's Charger (Tier 2): +6 charge to all backpack batteries nightly (persistent).") + req;
            case "charger3":
                return LangHelper.T("蛙哥充电器（三级）：每晚自动给背包所有电池充满电（拥有即生效，不消耗）。", "Wage's Charger (Tier 3): fully charges all backpack batteries nightly (persistent).") + req;
            // 10-18 门路卡描述（§2.2：A 以假乱真 / B 白手套 / C 锅她背）
            case "conn1":
                return LangHelper.T("蛙娘门路 Lv1：假货按原价收（每日1件、每局20件、单件≤600、抽成20%、冷却2天、走漏6%）。", "Wage Girl Connections Lv1: fake items sell at full price (1/day, 20/run, ≤600 each, 20% fee, 2d cooldown, 6% leak).") + req;
            case "conn2":
                return LangHelper.T("蛙娘门路 Lv2：违禁品也按原价收（每日2件、每局50件、单件≤1200、抽成15%、冷却1天、走漏3%）。", "Wage Girl Connections Lv2: contraband also at full price (2/day, 50/run, ≤1200 each, 15% fee, 1d cooldown, 3% leak).") + req;
            case "conn3":
                return LangHelper.T("蛙娘门路 Lv3：直售免罪——你直接把违禁品/假货卖给客户不留证据（每日3件、每局100件、单件≤2500、抽成10%、无冷却无走漏、整档免罪上限3万）。", "Wage Girl Connections Lv3: direct sales leave no evidence (3/day, 100/run, ≤2500 each, 10% fee, no cooldown/leak, 30k crime exemption cap).") + req;
        }
        return e.Id + req;
    }

    private static bool ServiceUnlockable()
    {
        try
        {
            if (B2ServiceStore.GetLevel(WagePowerPerk.PerkId) < 2) return false; // 蛙哥牛逼 Lv2 起步
            if (WageGirlSystem.GetAffectionExact() < SVC_AFFECTION_GATE) return false;
            return true;
        }
        catch { return false; }
    }

    private static string ServiceRequirementText()
    {
        // 10-15 好感可视化：失败提示动态显示当前好感+差值（好感不足时）；好感达标则提示 Lv2
        try
        {
            int gate = SVC_AFFECTION_GATE;
            int cur = (int)WageGirlSystem.GetAffectionExact();
            if (cur < gate)
                return LangHelper.T("解锁条件不足：蛙娘好感 " + cur + "/" + gate + "（还差 " + (gate - cur) + "）", "Need affection " + cur + "/" + gate + " (+" + (gate - cur) + ")");
        }
        catch { }
        return LangHelper.T("解锁条件不足：需蛙哥牛逼 Lv2", "Need WageBrother Lv2");
    }

    // 10-18 §5 A/C/F：返回 bool（成功=AddItemToCounter 非null 且柜台可查实例）；失败按原因分流：
    //   柜台未就绪（EmporiumEntry.Instance 或 frontInvinvElement 为 null，WageBrother.cs:222 同款判据）→ 直接入队（重试可达）；
    //   AddItemToCounter 返回 null → 永久失败（NoDirectory/CreateFailed/AddThrew）或瞬时（NoInstance）——统一入队，队列消费者按原因分档丢弃/重试。
    //   P1-8（审阅 20261018）：队列携带 (entryId, itemId) 二元组——delivered 键必须写条目 id（面板读 e.Id），否则补发态永不恢复
    // 10-18 用户拍板：许可/充电器「买了直接进后库」，不再先摆到柜台让玩家自己拿。
    //   toBackInv=true 的服务（permit1-3 / charger1-3）走 GrantToPlayerBackInv；其余（养蛊机/AI生成器/保护器）仍上柜台。
    private static bool GrantServiceItem(string entryId, string itemId, bool toBackInv = false)
    {
        try
        {
            // F：不再白造实例判空——直接用 WageItemFactory.Create 结果走 AddItemToCounter(GameItem) 重载
            GameItem it = WageAPI.WageItemFactory.Create(itemId);
            if (it == null) { Notify(LangHelper.T("物品生成失败", "Item create failed")); return false; }
            if (toBackInv)
            {
                // 直接进后库：先清「未购买」标记、盖「已拥有」，再塞进玩家后库
                try
                {
                    it.DisableTag("not_purchased", true);
                    it.DisableTag("TAG_NOT_PURCHASED", true);
                    it.EnableTag("IS_OWNED_TAG", true);
                }
                catch { }
                if (WageAPI.WageItemGrant.GrantToPlayerBackInv(it, includeOwnedTransfer: true))
                {
                    Core.LogMsg("[B2R] 服务发放 → 后库: " + itemId + " entry=" + entryId);
                    return true;
                }
                _pendingServiceItems.Enqueue(new PendingService(entryId, itemId, true));
                Core.LogMsg("[B2R] 服务发放后库失败 → 入延迟重试队列: " + itemId);
                return false;
            }
            // C：柜台未就绪 → 直接入队（不再 try/catch 吞掉——旧逻辑 AddItemToCounter 内部 catch return null，外层 catch 永不进入）
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null)
            {
                _pendingServiceItems.Enqueue(new PendingService(entryId, itemId));
                Core.LogMsg("[B2R] 服务发放柜台未就绪 → 入延迟重试队列: " + itemId);
                return false;
            }
            // A：AddItemToCounter 返回非 null 且柜台内可查到该实例才算成功
            var placed = MerchantHelper.AddItemToCounter(it, 0, true);
            if (placed == null || !IsOnCounter(placed))
            {
                _pendingServiceItems.Enqueue(new PendingService(entryId, itemId));
                Core.LogMsg("[B2R] 服务发放上桌失败 → 入延迟重试队列: " + itemId);
                return false;
            }
            Core.LogMsg("[B2R] 服务发放 → 柜台(isOwend=true): " + itemId);
            return true;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[B2R] GrantServiceItem 异常: " + ex.Message);
            _pendingServiceItems.Enqueue(new PendingService(entryId, itemId));
            return false;
        }
    }

    // P1-8：队列条目（条目 id + 物品 id）——补发成功后写 delivered_<entryId>（面板读键）
    private sealed class PendingService
    {
        internal string EntryId; // e.Id（如 "b2_svc_permit1"）——delivered/SetLevel 写此键
        internal string ItemId;  // 物品 id（如 "wage_permit_1"）——AddItemToCounterEx 用此 id
        internal bool ToBackInv; // 10-18：true=补发时直接进后库（许可/充电器），false=上柜台
        internal PendingService(string entryId, string itemId, bool toBackInv = false) { EntryId = entryId; ItemId = itemId; ToBackInv = toBackInv; }
    }

    // 10-18 §5 A：柜台内是否可查到该实例（frontInvinvElement.childItems 引用比对）
    private static bool IsOnCounter(GameItem item)
    {
        try
        {
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null) return false;
            var inv = (GameInventory)em.frontInvinvElement;
            if (inv == null || inv.childItems == null) return false;
            for (int i = 0; i < inv.childItems.Count; i++)
                if (inv.childItems[i] != null && inv.childItems[i].Pointer == item.Pointer) return true; // P2-13（审阅）：Pointer 比较，与全库 5 处一致
            return false;
        }
        catch { return false; }
    }

    // FIX16：服务发放延迟重试队列（柜台未就绪时先入队，帧轮询柜台就绪后补上桌——防"买了没上桌"）
    // 10-18 §5 C/E：消费者按失败原因分档——永久失败（NoDirectory/CreateFailed/AddThrew）丢弃并日志，瞬时失败（NoInstance）重新入队
    private static readonly System.Collections.Generic.Queue<PendingService> _pendingServiceItems = new System.Collections.Generic.Queue<PendingService>();
    internal static void OnUpdateServiceItemRefill()
    {
        try
        {
            if (_pendingServiceItems.Count == 0) return;
            var em = Il2Cpp.EmporiumEntry.Instance;
            if (em == null || em.frontInvinvElement == null) return; // 柜台未就绪 → 下帧重试
            int batch = _pendingServiceItems.Count; // 只消费本次入队的数量，防无限循环
            for (int i = 0; i < batch && _pendingServiceItems.Count > 0; i++)
            {
                var p = _pendingServiceItems.Dequeue();
                try
                {
                    // 10-18：许可/充电器补发直接进后库（不依赖柜台就绪，柜台没摆也能到货）
                    if (p.ToBackInv)
                    {
                        var bk = WageAPI.WageItemFactory.Create(p.ItemId);
                        if (bk != null)
                        {
                            try
                            {
                                bk.DisableTag("not_purchased", true);
                                bk.DisableTag("TAG_NOT_PURCHASED", true);
                                bk.EnableTag("IS_OWNED_TAG", true);
                            }
                            catch { }
                            if (WageAPI.WageItemGrant.GrantToPlayerBackInv(bk, includeOwnedTransfer: true))
                            {
                                if (!string.IsNullOrEmpty(p.EntryId))
                                {
                                    if (B2ServiceStore.GetLevel(p.EntryId) < 1) B2ServiceStore.SetLevel(p.EntryId, 1);
                                    B2ServiceStore.SetDelivered(p.EntryId);
                                }
                                Core.LogMsg("[B2R] 服务补发（延迟重试成功）→ 后库: " + p.ItemId + " entry=" + p.EntryId);
                                continue;
                            }
                        }
                        _pendingServiceItems.Enqueue(p); // 后库也进不去 → 下轮再试，绝不丢
                        Core.LogMsg("[B2R] 服务补发后库失败，保留重试: " + p.ItemId);
                        continue;
                    }
                    var reason = MerchantHelper.AddFailReason.None;
                    var placed = MerchantHelper.AddItemToCounterEx(p.ItemId, out reason, 0, true); // 同款 isOwend=true（玩家拥有直接拿走）
                    if (placed != null && IsOnCounter(placed))
                    {
                        // P1-8：delivered/SetLevel 写条目 id（面板读 e.Id）；补发态已锁则 SetLevel 无副作用
                        if (!string.IsNullOrEmpty(p.EntryId))
                        {
                            if (B2ServiceStore.GetLevel(p.EntryId) < 1) B2ServiceStore.SetLevel(p.EntryId, 1);
                            B2ServiceStore.SetDelivered(p.EntryId);
                        }
                        Core.LogMsg("[B2R] 服务发放（延迟重试成功）→ 柜台: " + p.ItemId + " entry=" + p.EntryId);
                        continue;
                    }
                    if (reason == MerchantHelper.AddFailReason.NoInstance)
                    {
                        _pendingServiceItems.Enqueue(p); // 柜台又未就绪 → 下帧再试
                        Core.LogMsg("[B2R] 服务发放延迟重试（柜台未就绪，再排）: " + p.ItemId);
                    }
                    else
                    {
                        // §5.3（第五轮 20261018）：永久失败不再丢弃——物品退回玩家后库（钱已扣，货必须到手），并置 delivered 防重复扣费。
                        //   不采用退款方案：PendingService 未携带价格，补发态 GetCost 返回 0 退不出正确金额；退物品等价且无金额歧义。
                        try
                        {
                            var back = WageAPI.WageItemFactory.Create(p.ItemId);
                            if (back != null && WageAPI.WageItemGrant.GrantToPlayerBackInv(back, includeOwnedTransfer: true))
                            {
                                if (!string.IsNullOrEmpty(p.EntryId))
                                {
                                    if (B2ServiceStore.GetLevel(p.EntryId) < 1) B2ServiceStore.SetLevel(p.EntryId, 1);
                                    B2ServiceStore.SetDelivered(p.EntryId);
                                }
                                Core.LogMsg("[B2R] 服务发放永久失败（" + reason + "）→ 已退回后库: " + p.ItemId + " entry=" + p.EntryId);
                            }
                            else
                            {
                                // 后库也进不去 → 保留在队列末尾，下轮继续尝试（宁可滞留不可蒸发）
                                _pendingServiceItems.Enqueue(p);
                                Core.LogMsg("[B2R] 服务发放永久失败且后库退回失败，保留重试: " + p.ItemId + " reason=" + reason);
                            }
                        }
                        catch (Exception ex2)
                        {
                            _pendingServiceItems.Enqueue(p);
                            Core.LogMsg("[B2R] 服务发放兜底异常，保留重试: " + p.ItemId + " " + ex2.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Core.LogMsg("[B2R] 服务发放延迟重试失败 " + p.ItemId + ": " + ex.Message);
                    _pendingServiceItems.Enqueue(p);
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OnUpdateServiceItemRefill 异常: " + ex.Message); }
    }

    private static void OnUnlockService(WildEntry e)
    {
        try
        {
            if (e.ServiceId == "protector")
            {
                if (!ServiceUnlockable()) { Notify(ServiceRequirementText()); return; }
                if (!GrantServiceItem(e.Id, GuMachineSystem.PROTECTOR_ID))
                {
                    Notify(LangHelper.T("上柜台失败，稍后自动补发（无需再付款）", "Counter placement failed; will auto-redeliver (no extra charge)"));
                    return;
                }
                Notify(LangHelper.T("已获得 蛙哥保护器核心（放入AI生成器舱即稳定版）", "Got Wage's Protector Core (place in AI generator bay for stable mode)"));
                return;
            }
            if (e.ServiceId == "protector_chance")
            {
                if (!ServiceUnlockable()) { Notify(ServiceRequirementText()); return; }
                int lv = WageSaveStore.GetInt(WageBrother.CLIENT_ID, WageBrother.PROTECTOR_SPAWN_KEY, 0);
                if (lv >= 6) { Notify(LangHelper.T("保护器出现概率已达上限 90%", "Protector spawn chance already maxed (90%)")); return; }
                lv++;
                WageSaveStore.SetInt(WageBrother.CLIENT_ID, WageBrother.PROTECTOR_SPAWN_KEY, lv);
                WageSaveStore.Flush();
                Notify(LangHelper.T("保护器随机出现概率提升至 " + (30 + lv * 10) + "%", "Protector random spawn chance up to " + (30 + lv * 10) + "%"));
                Core.LogMsg("[B2R] 保护器概率提升: Lv" + lv + " → " + (30 + lv * 10) + "%");
                return;
            }
            // 10-18 玩家拍板：养蛊机可重复购买（每台 30000，多台独立炼蛊）——照 protector 模式：每次购买发一台，
            //   不写锁（B2ServiceStore.GetLevel 恒 0 → ServiceCost 恒收费、redeliver 判定恒 false），FindGuMachines
            //   按实例逐台 tick（DayLoop.cs:28-59 seen 去重），多台天然支持。
            if (e.ServiceId == "gu_machine")
            {
                if (!ServiceUnlockable()) { Notify(ServiceRequirementText()); return; }
                if (!GrantServiceItem(e.Id, GuMachineSystem.GU_MACHINE_ID))
                {
                    Notify(LangHelper.T("上柜台失败，稍后自动补发（无需再付款）", "Counter placement failed; will auto-redeliver (no extra charge)"));
                    return;
                }
                Notify(LangHelper.T("已获得 蛙哥养蛊机（可重复购买，多台独立炼蛊）", "Got Wage's Swarm Forge (repeatable, each forge works independently)"));
                Core.LogMsg("[B2R] 养蛊机发放: " + e.Id);
                return;
            }
            // 10-18 §5 B/D：先发后锁 + 存量补发。
            //   补发态 = 已解锁（GetLevel>=1）但未到货（delivered_<id>==0）→ 走发放分支且不扣费（GetCost 对该 id 返回 0）；
            //   正常购买态 = 未解锁 → 先发放成功（GrantServiceItem 返回 true）再写锁 SetLevel；失败不写锁（可重试/补发）。
            bool redeliver = B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id);
            if (!redeliver && B2ServiceStore.GetLevel(e.Id) >= 1) { Notify(LangHelper.T("该服务已解锁", "Service already unlocked")); return; }
            // P2-11（审阅 20261018）：补发态跳过解锁门槛——已扣费未到货的老档，好感/等级回落后补发入口仍可用（唯一恢复入口）
            if (!redeliver && !ServiceUnlockable()) { Notify(ServiceRequirementText()); return; }
            if (e.ServiceId == "ai_generator") { if (GrantServiceItem(e.Id, GuMachineSystem.AI_GENERATOR_ID)) MarkServiceDelivered(e); else NotifyServiceFail(); return; }
            else if (e.ServiceId == "permit1") { if (GrantServiceItem(e.Id, WageBrokerPermitHelper.PERMIT_1_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥的许可（一级），已放入后库", "Got Wage's Permit (Tier 1), placed in back storage")); } else NotifyServiceFail(); return; }
            else if (e.ServiceId == "permit2") { if (GrantServiceItem(e.Id, WageBrokerPermitHelper.PERMIT_2_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥的许可（二级），已放入后库", "Got Wage's Permit (Tier 2), placed in back storage")); } else NotifyServiceFail(); return; }
            else if (e.ServiceId == "permit3") { if (GrantServiceItem(e.Id, WageBrokerPermitHelper.PERMIT_3_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥的许可（三级），已放入后库", "Got Wage's Permit (Tier 3), placed in back storage")); } else NotifyServiceFail(); return; }
            else if (e.ServiceId == "charger1") { if (GrantServiceItem(e.Id, WageBrokerChargerHelper.CHARGER_1_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥充电器（一级），已放入后库", "Got Wage's Charger (Tier 1), placed in back storage")); } else NotifyServiceFail(); return; }
            else if (e.ServiceId == "charger2") { if (GrantServiceItem(e.Id, WageBrokerChargerHelper.CHARGER_2_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥充电器（二级），已放入后库", "Got Wage's Charger (Tier 2), placed in back storage")); } else NotifyServiceFail(); return; }
            else if (e.ServiceId == "charger3") { if (GrantServiceItem(e.Id, WageBrokerChargerHelper.CHARGER_3_ID, true)) { MarkServiceDelivered(e); Notify(LangHelper.T("已获得 蛙哥充电器（三级），已放入后库", "Got Wage's Charger (Tier 3), placed in back storage")); } else NotifyServiceFail(); return; }
            // 10-18 门路：无实体物品，直接写等级（B2ServiceStore.owned_蛙娘门路）；已购判定用 WageGirlConn.Level()
            else if (e.ServiceId == "conn1") { if (WageGirlConn.Level() >= 1) { Notify(LangHelper.T("门路已开通", "Connections already unlocked")); return; } B2ServiceStore.SetLevel(WageGirlConn.PerkId, 1); B2ServiceStore.SetDelivered(e.Id); Notify(LangHelper.T("蛙娘门路 Lv1 已开通：假货按原价收", "Wage Girl Connections Lv1 unlocked: fakes at full price")); Core.LogMsg("[B2R] 门路 Lv1 开通"); return; }
            else if (e.ServiceId == "conn2") { if (WageGirlConn.Level() >= 2) { Notify(LangHelper.T("门路已开通", "Connections already unlocked")); return; } B2ServiceStore.SetLevel(WageGirlConn.PerkId, 2); B2ServiceStore.SetDelivered(e.Id); Notify(LangHelper.T("蛙娘门路 Lv2 已开通：违禁品也按原价收", "Wage Girl Connections Lv2 unlocked: contraband at full price")); Core.LogMsg("[B2R] 门路 Lv2 开通"); return; }
            else if (e.ServiceId == "conn3") { if (WageGirlConn.Level() >= 3) { Notify(LangHelper.T("门路已开通", "Connections already unlocked")); return; } B2ServiceStore.SetLevel(WageGirlConn.PerkId, 3); B2ServiceStore.SetDelivered(e.Id); Notify(LangHelper.T("蛙娘门路 Lv3 已开通：直售免罪", "Wage Girl Connections Lv3 unlocked: direct sales leave no evidence")); Core.LogMsg("[B2R] 门路 Lv3 开通"); return; }
            else { Notify(LangHelper.T("服务已解锁", "Service unlocked")); return; }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OnUnlockService 异常: " + ex.Message); }
    }

    // 10-18 §5 B/D：发放成功后写锁（补发态已锁则不再写）+ delivered 标记
    private static void MarkServiceDelivered(WildEntry e)
    {
        if (B2ServiceStore.GetLevel(e.Id) < 1) B2ServiceStore.SetLevel(e.Id, 1); // 先发后锁：仅正常购买态写锁
        B2ServiceStore.SetDelivered(e.Id); // 到货标记（老档补发后置 1，按钮恢复已解锁态）
        Core.LogMsg("[B2R] 服务解锁: " + e.Id);
    }

    // 10-18 §5 B：发放失败提示（先发后锁：不写锁 → 按钮保留可点，物品已入重试队列自动补发）
    private static void NotifyServiceFail()
        => Notify(LangHelper.T("上柜台失败，稍后自动补发（无需再付款）", "Counter placement failed; will auto-redeliver (no extra charge)"));

    // 10-17 重回第一天（wanderer_day1）：买完立刻生效、一次性（拆包AI Q4 全清落地清单，Memos nChyYHKex6U2sejaf2ScLs）
    //   字段全 public 直接可写：dayCounter=0 拨回第1天；gridInv.ExpelAll 清柜台（第三方货自动隔离）；
    //   客户排期/声望/犯罪证据清零；好物池随机刷新（复用 AddRandomItemsToCounter，force 跳过 HardMode 门）
    private static void ApplyWandererDayOne()
    {
        try
        {
            var ss = StoreStation.Instance;
            if (ss != null) ss.dayCounter = 0;

            var ps = PlayerStore.Instance;
            if (ps != null)
            {
                try { if (ps.gridInv != null) ps.gridInv.ExpelAll(); } catch (Exception ex) { Core.LogMsg("[B2R] 清柜台异常: " + ex.Message); }
                try { if (ps.futurStoreClientIdQueue != null) ps.futurStoreClientIdQueue.Clear(); } catch (Exception ex) { Core.LogMsg("[B2R] 清客户排期ID异常: " + ex.Message); }
                try { if (ps.futurStoreClientDayQueue != null) ps.futurStoreClientDayQueue.Clear(); } catch (Exception ex) { Core.LogMsg("[B2R] 清客户排期天异常: " + ex.Message); }
                try
                {
                    if (ps.storeReputations != null)
                    {
                        foreach (var rep in ps.storeReputations)
                        {
                            if (rep == null) continue;
                            try { int cur = rep.GetReputation(); if (cur != 0) rep.ModReputation(-cur); } catch { }
                        }
                    }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] 声望重置异常: " + ex.Message); }
                try { if (ps.soldItemsTracker != null) ps.soldItemsTracker.Clear(); } catch (Exception ex) { Core.LogMsg("[B2R] 清销售记录异常: " + ex.Message); }
                try { if (ps.wantedStatus != null) ps.wantedStatus.Clear(); } catch (Exception ex) { Core.LogMsg("[B2R] 清通缉状态异常: " + ex.Message); }
                try { ps.wrongfulReportCount = 0; } catch (Exception ex) { Core.LogMsg("[B2R] 清零诬告计数异常: " + ex.Message); }
                try { ps.soldStolenOrContrabandThisRun = false; } catch (Exception ex) { Core.LogMsg("[B2R] 清零违禁销售标记异常: " + ex.Message); }
            }

            // 好物池随机刷新 4 件（force 跳过 HardMode 门——用户拍板普通版也可触发）
            WagePowerPerk.AddRandomItemsToCounter(null, true, 4);

            Core.LogMsg("[B2R] 重回第一天执行：dayCounter=0，柜台清空，排期/声望/犯罪证据清零，好物池刷新×4");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] ApplyWandererDayOne 异常: " + ex.Message); }
    }

    public static void PostfixGetLocalizedTitle(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsService) { __result = ServiceTitle(e); return; } // 服务条目
            if (e.IsCannibal) { __result = LangHelper.T("吞噬季", "Cannibalism Season"); return; }
            var perk = FindPerk(e.PerkId);
            __result = perk != null ? perk.DisplayName : e.PerkId;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] Title 异常: " + ex.Message); }
    }

    public static void PostfixGetLocalizedSubtitle(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsService) { __result = ServiceSubtitle(e); return; } // 服务条目
            if (e.IsCannibal) { __result = LangHelper.T("机器模组/电池互食盛宴", "Machines devour modules & batteries"); return; }
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            int c = perk.Cost;
            string type = c < 0 ? LangHelper.T("负面特性", "Negative")
                       : (c == 0 ? LangHelper.T("零价特性", "Free") : LangHelper.T("正面特性", "Positive"));
            if (perk.Cost < 0)
            {
                if (B2ServiceStore.IsPurified(e.PerkId)) type += LangHelper.T(" · 已净化", " · purified");
                else if (B2ServiceStore.IsEliminated(e.PerkId)) type += LangHelper.T(" · 已消除", " · eliminated");
            }
            else
            {
                int lv = B2ServiceStore.GetLevel(e.PerkId);
                if (lv >= 1) type += LangHelper.T(" · Lv" + lv, " · Lv" + lv);
            }
            __result = type;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] Subtitle 异常: " + ex.Message); }
    }

    public static void PostfixGetLocalizedDescription(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsService) { __result = ServiceDescription(e); return; } // 服务条目
            if (e.IsCannibal)
            {
                if (B2ServiceStore.IsCannibalOwned)
                {
                    int mode = B2ServiceStore.CannibalMode;
                    string m = mode == 0
                        ? LangHelper.T("单次模式 · 第" + B2ServiceStore.CannibalDay + "天触发", "Once · Day " + B2ServiceStore.CannibalDay)
                        : LangHelper.T("周期模式 · 每" + B2ServiceStore.CannibalInterval + "天触发", "Every " + B2ServiceStore.CannibalInterval + " days");
                    __result = LangHelper.T("已开启。" + m + "。再次点击可切换模式。", "ON. " + m + ". Click again to switch mode.");
                }
                else
                {
                    __result = LangHelper.T("机器模组与电池互食的盛宴季。开启后按设定日期/周期触发（默认每 10 天一次）。",
                        "Machines feast on modules & batteries. Triggers on schedule (every 10 days default).");
                }
                return;
            }
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            __result = perk.Description;
            int lv = B2ServiceStore.GetLevel(e.PerkId);
            if (perk.Cost >= 0 && lv >= 1)
            {
                string up = UpgradeEffectsText(e.PerkId, lv);
                if (!string.IsNullOrEmpty(up))
                    __result += LangHelper.T("\n\n" + up, "");
                else
                    __result += LangHelper.T("\n\n当前等级：Lv" + lv + (lv >= 3 ? "（已满级）" : ""), "");
            }
            else if (perk.Cost < 0 && B2ServiceStore.IsPurified(e.PerkId))
            {
                __result += LangHelper.T("\n\n（已净化：保留加成面，负面效果已移除）", "");
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] Description 异常: " + ex.Message); }
    }

    /// <summary>升级效果文案（10-15：与效果接线一一对应，未接线特性不显示）。</summary>
    private static string UpgradeEffectsText(string perkId, int lv)
    {
        string key = perkId;
        if (key == WagePowerPerk.PerkId)      return lv >= 3 ? LangHelper.T("Lv3：蛙哥到店客户预算 ×5", "Lv3: WageBrother client budget x5") : LangHelper.T("Lv2：解锁蛙哥服务面板", "Lv2: unlocks the service panel");
        if (key == WageGirlPerk.PerkId)       return lv >= 3 ? LangHelper.T("Lv3：议价+60、销赃+10%、洗白消耗减半", "Lv3: bargain +60, fence +10%, wash cost halved") : LangHelper.T("Lv2：议价+55、销赃+5%", "Lv2: bargain +55, fence +5%");
        if (key == DrJacksonFriendPerk.PerkId) return lv >= 3 ? LangHelper.T("Lv3：博士友情价 -15%", "Lv3: Jackson friend price -15%") : LangHelper.T("Lv2：博士友情价 -12%", "Lv2: Jackson friend price -12%");
        if (key == DestinyDicePerk.PerkId)    return lv >= 3 ? LangHelper.T("Lv3：命运骰触发阈值 300", "Lv3: dice trigger threshold 300") : LangHelper.T("Lv2：命运骰触发阈值 350", "Lv2: dice trigger threshold 350");
        if (key == LuckScoutPerk.PerkId)      return lv >= 3 ? LangHelper.T("Lv3：稀有物掉落率上限 +10%", "Lv3: rare drop cap +10%") : LangHelper.T("Lv2：稀有物掉落率上限 +5%", "Lv2: rare drop cap +5%");
        if (key == SmilingFacePerk.PerkId)    return lv >= 3 ? LangHelper.T("Lv3：卖出加价提升至 40%", "Lv3: sell markup 40%") : LangHelper.T("Lv2：卖出加价提升至 30%", "Lv2: sell markup 30%");
        if (key == WineLoverPerk.PerkId)      return lv >= 3 ? LangHelper.T("Lv3：酒类卖出加价提升至 50%", "Lv3: alcohol markup 50%") : LangHelper.T("Lv2：酒类卖出加价提升至 35%", "Lv2: alcohol markup 35%");
        if (key == RiskTakerPerk.PerkId)      return lv >= 3 ? LangHelper.T("Lv3：违禁品加价提升至 40%", "Lv3: contraband markup 40%") : LangHelper.T("Lv2：违禁品加价提升至 30%", "Lv2: contraband markup 30%");
        if (key == WaterMerchantPerk.PerkId || key == AlcoholMerchantPerk.PerkId || key == RetiredGunsmithPerk.PerkId)
            return lv >= 3 ? LangHelper.T("Lv3：专属商人折扣 -15%", "Lv3: merchant discount -15%") : LangHelper.T("Lv2：专属商人折扣 -10%", "Lv2: merchant discount -10%");
        if (key == WandererPerk.PerkId)       return LangHelper.T("Lv2/Lv3：每次升级再赠 1 工具 + 1 日用品", "Lv2/Lv3: +1 tool & +1 household per level");
        if (key == WildeEvidencePerk.PerkId)  return lv >= 3 ? LangHelper.T("Lv3：线人减证据 -12、犯罪消除 -100", "Lv3: fixer reduces evidence 12, crime 100") : LangHelper.T("Lv2：线人减证据 -8、犯罪消除 -75", "Lv2: fixer reduces evidence 8, crime 75");
        return "";
    }

    public static void PostfixGetLocalizedAlreadyBought(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsCannibal) return; // 吞噬季已购态由 Description 表达（按钮保持可点 = 切换模式）
            if (e.IsService)
            {
                // 10-18 童瑶反馈根修（审阅 §4 改动3）：服务条目 PerkId==null 原先裸退 → 原版拼 "network_"+id+"_already_bought_in"
                //   无本地化条目 → 卡片底部裸键名（NETWORK_B2_SVC_..._ALREADY_BOUGHT_IN 截图）。补服务分支文案：
                //   补发态/可重复/已解锁，一次性未购 return（原版无已购文案=空）。
                bool redeliver = B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id);
                if (redeliver) { __result = LangHelper.T("补发中 · 点击领取", "Redelivering · click to claim"); return; }
                if (e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine")
                { __result = LangHelper.T("可重复购买", "Repeatable"); return; }
                if (B2ServiceStore.GetLevel(e.Id) >= 1) { __result = LangHelper.T("已解锁", "Unlocked"); return; }
                return;
            }
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            if (perk.Cost < 0)
            {
                if (B2ServiceStore.IsPurified(e.PerkId)) __result = LangHelper.T("已净化", "purified");
                else if (B2ServiceStore.IsEliminated(e.PerkId)) __result = LangHelper.T("已消除", "eliminated");
                return;
            }
            int lv = B2ServiceStore.GetLevel(e.PerkId);
            if (lv >= 3) __result = LangHelper.T("已满级", "MAX LEVEL");
            else if (lv >= 1) __result = LangHelper.T("已拥有（可升级）", "owned (upgradable)");
            else if (IsSelected(e.PerkId)) __result = LangHelper.T("开局已选（可升级）", "selected (upgradable)");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] AlreadyBought 异常: " + ex.Message); }
    }

    private static bool IsSelected(string perkId)
    {
        try { return StartingPerk.IsPerkActive(perkId); } catch { return false; }
    }

    // IsUnlocked：满级/已消除 → true（按钮锁定，显示 AlreadyBought）；其余 false（可点购买/升级/消除）
    public static void PostfixIsUnlocked(NetworkUpgrade __instance, ref bool __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsCannibal) { __result = false; return; } // 吞噬季永远可点（购买/切换模式）
            if (e.IsService)
            {
                // 10-18 童瑶反馈根修（审阅《审阅_童瑶反馈_服务重复购买锁定》§4 改动1）：原版 Unlock() 无条件 state=2
                //   （NetworkUpgrade.txt:3617）、IsUnlocked()=state==2（:3592）、UI 门控 interactable=IsReady()&&!IsUnlocked()
                //   （WildUIManager.txt:203-249）——本方法原先无 IsService 分支 → 服务买一次后 state 恒 2 → 按钮永久锁死
                //   （玩家"保护器·随机出现率"买一次后买不了）。补发态必须可点（已扣费未到货的恢复入口）。
                bool redeliver = B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id);
                if (redeliver) { __result = false; return; }
                bool repeatable = e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine"; // 10-18 玩家拍板：养蛊机可重复
                if (repeatable) { __result = false; return; } // 可重复：覆盖原版 state==2，恒可点
                return;                                       // 一次性：保持原版语义（未购不锁、已购锁=想要）
            }
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            if (perk.Cost < 0)
            {
                bool gone = B2ServiceStore.IsEliminated(e.PerkId) || B2ServiceStore.IsPurified(e.PerkId);
                // 10-19 审阅落地（假消除）：仅"完全消除"以上锁须以列表为准——标志位说消了但列表还在
                //   （老档假消除/迁移前）→ 不上锁，让玩家可重试；净化=特性仍在列表属正常，不做列表校验
                if (gone && B2ServiceStore.IsEliminated(e.PerkId))
                {
                    try { gone = !StartingPerk.IsPerkActive(e.PerkId); } catch { }
                }
                __result = gone;
                return;
            }
            __result = B2ServiceStore.GetLevel(e.PerkId) >= 3;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] IsUnlocked 异常: " + ex.Message); }
    }

    // IsReady：可点前置（未满级/未消除；招贼体质被偷未赎回锁）
    // FIX5：改 Prefix——原版 IsReady 内部先调 IsHavePrerequisite()，prerequisite=null 的 b2_ 条目直接 NRE（日志 22:07:00 实锤），
    //   Postfix 在原版崩后不执行 → 按钮态判定中断 → 解锁按钮保持灰。Prefix 跳过原版，b2_ 直接给结果。
    public static bool PrefixIsReady(NetworkUpgrade __instance, ref bool __result)
    {
        try
        {
            if (!IsB2(__instance)) return true; // 非 b2_：走原版
            var e = FindEntry(__instance.id);
            if (e == null) return true;
            if (e.IsCannibal) { __result = true; return false; } // 吞噬季永远可点（购买/切换模式）
            // FIX15（10-16 玩家"有些服务钱够了买了才告知条件不足"）：服务条目的解锁条件（蛙哥Lv2+好感）必须
            //   在购买前拦截——原 OnUnlockService 检查在原生扣钱之后（原生购买先支付再触发 UnlockAction）→
            //   条件不足时钱已扣。PrefixIsReady=购买按钮可点判定（购买前），此处直接按 ServiceUnlockable 给结果。
            if (e.IsService)
            {
                // 10-18 §5 D：存量补发——已解锁但未到货（delivered_<id>==0）→ 可点补发（GetCost 对该 id 返回 0，不扣费）
                // P2-11（审阅 20261018）：补发态跳过解锁门槛判定（在 ServiceUnlockable 之前）——已扣费未到货的老档入口不失效
                bool redeliver = B2ServiceStore.GetLevel(e.Id) >= 1 && !B2ServiceStore.IsDelivered(e.Id);
                if (redeliver) { __result = true; return false; }
                if (!ServiceUnlockable()) { __result = false; return false; } // 条件不足→按钮灰（提示看描述）
                // 一次性服务：已解锁+已到货（IsUnlocked 锁）；可重复（保护器/概率/养蛊机）→ 可点
                __result = e.ServiceId == "protector" || e.ServiceId == "protector_chance" || e.ServiceId == "gu_machine" || B2ServiceStore.GetLevel(e.Id) < 1;
                return false;
            }
            var perk = FindPerk(e.PerkId);
            if (perk == null) { __result = true; return false; }
            if (perk.Cost < 0)
            {
                if (e.PerkId == ThiefMagnetPerk.PerkId && ThiefMagnetSystem.StolenCount() > 0)
                    __result = false; // 招贼体质：被偷未赎回 → 锁（提示走消业障）
                else if (WageBrother.NonRemovablePerks.Contains(e.PerkId))
                    __result = false; // 10-19 审阅落地：精神错乱等系统特性面板链不给消（对齐名片链 WageBrother.cs:837）
                else
                    __result = !B2ServiceStore.IsEliminated(e.PerkId) && !B2ServiceStore.IsPurified(e.PerkId);
                return false;
            }
            __result = B2ServiceStore.GetLevel(e.PerkId) < 3;
            return false;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[B2R] PrefixIsReady 异常: " + ex.Message);
            return true;
        }
    }

    // FIX5：原版 IsHavePrerequisite() 对 prerequisite=null 直接 NRE（日志 22:07:00 实锤：NullReferenceException at IsHavePrerequisite）
    //   → b2_ 条目无前置要求，Prefix 直接返回 true 跳过原版（根治 NRE + OnElementClicked 按钮态判定中断）
    public static bool PrefixIsHavePrerequisite(NetworkUpgrade __instance, ref bool __result)
    {
        try
        {
            if (!IsB2(__instance)) return true; // 非 b2_：走原版
            __result = true; // b2_ 无前置
            return false;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[B2R] PrefixIsHavePrerequisite 异常: " + ex.Message);
            return true;
        }
    }

    // ================= ③ 购买 / 升级 / 消除执行（原生支付已完成，本侧只做状态+附赠+刷新） =================
    private static void OnUnlock(WildEntry e)
    {
        try
        {
            if (e.IsService) { OnUnlockService(e); RefreshDetail(e.Id); return; } // 服务条目（机器/流浪者）
            if (e.IsCannibal)
            {
                if (!B2ServiceStore.IsCannibalOwned)
                {
                    B2ServiceStore.SetCannibalOwned(true);
                    B2ServiceStore.SetCannibalMode(1); // 默认周期（对齐原版语义）
                    B2ServiceStore.SetCannibalInterval(10);
                    B2ServiceStore.SetCannibalDay(0);
                    Notify(LangHelper.T("吞噬季已开启！默认每 10 天一次，再次点击可切换模式", "Cannibalism Season ON! Every 10 days. Click again to switch."));
                    Core.LogMsg("[B2R] 已购买吞噬季（3000）");
                }
                else
                {
                    int m = B2ServiceStore.CannibalMode == 0 ? 1 : 0;
                    B2ServiceStore.SetCannibalMode(m);
                    Notify(LangHelper.T("吞噬季模式 → " + (m == 0 ? "单次（第" + B2ServiceStore.CannibalDay + "天）" : "周期（每" + B2ServiceStore.CannibalInterval + "天）"),
                                     "Cannibalism mode → " + (m == 0 ? "Once" : "Periodic")));
                    Core.LogMsg("[B2R] 吞噬季模式切换 → " + (m == 0 ? "单次" : "周期"));
                }
                RefreshDetail(e.Id);
                return;
            }

            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            int lv = B2ServiceStore.GetLevel(e.PerkId);

            if (perk.Cost < 0) // 负面：消除 / 净化
            {
                if (B2ServiceStore.IsEliminated(e.PerkId) || B2ServiceStore.IsPurified(e.PerkId)) return;
                if (e.PerkId == InfamousPerk.PerkId)
                {
                    // 声名狼藉特殊：分势力恢复窗口（全正才真正消除，走既有逻辑；本条目免费开窗）
                    try { WageBrother.ShowRepFactionWindow(); } catch (Exception ex) { Core.LogMsg("[B2R] 声名狼藉开窗异常: " + ex.Message); }
                    return;
                }
                if (e.PerkId == ThiefMagnetPerk.PerkId && ThiefMagnetSystem.StolenCount() > 0)
                {
                    Notify(LangHelper.T("被偷物品还没赎完，招贼体质暂时消不掉——先点「消业障」里的赎回", "Redeem stolen items first."));
                    return;
                }
                // 10-15 净化购买：可净化特性弹选择窗（消除=全移除 / 净化=保留加成只删 debuff）
                if (B2ServiceStore.IsPurifiable(e.PerkId)) { ShowPurifyWindow(e, perk); return; }
                // 10-19 审阅落地（假消除根修）：消除必须两步——①删原版列表 StartingPerk.RemovePerk（角色面板读的这份）
                //   ②写标志位 owned_<id>=-1（Core.PerkActive 统一入口）。原实现只做②→提示已消但角色面板仍在、钱照扣。
                try { StartingPerk.RemovePerk(e.PerkId); }
                catch (Exception ex) { Core.LogMsg("[B2R] RemovePerk 异常 " + e.PerkId + ": " + ex.Message); }
                B2ServiceStore.Eliminate(e.PerkId);
                // 10-19 审阅落地：提示改"后置校验驱动"——真删了才报成功；没删（RemovePerk 半失败/列表异常）→ 回滚标志位+退款，不固化终态
                bool reallyGone = false;
                try { reallyGone = !StartingPerk.IsPerkActive(e.PerkId); } catch { }
                if (reallyGone)
                {
                    Notify(LangHelper.T("已消除负面特性：" + perk.DisplayName, "Negative perk removed: " + perk.DisplayName));
                    Core.LogMsg("[B2R] 已消除 " + e.PerkId + "（列表已删+标志位已写）");
                }
                else
                {
                    // 回滚：标志位撤销 + 退还原生已扣的钱（原生支付在 UnlockAction 之前完成）
                    B2ServiceStore.SetLevel(e.PerkId, 0);
                    int refund = WageBrother.PriceForCost(perk.Cost);
                    try { var ps = PlayerStore.Instance; if (ps != null) ps.playerCash += refund; } catch { }
                    Notify(LangHelper.T("消除失败，已退还 " + refund + " 信用点", "Removal failed, refunded " + refund + " cr"));
                    Core.LogError("[B2R] 消除失败未生效: " + e.PerkId + "，已退款 " + refund);
                }
                RefreshDetail(e.Id);
                return;
            }

            // 正面 / 零价：购买 / 升级
            if (lv == 0)
            {
                B2ServiceStore.SetLevel(e.PerkId, 1);
                Notify(LangHelper.T("已获得特性：" + perk.DisplayName, "Perk acquired: " + perk.DisplayName));
                Core.LogMsg("[B2R] 已购买 " + e.PerkId);
                GrantGift(e.PerkId); // 附赠开局物品（幂等防双份）
            }
            else if (lv == 1)
            {
                B2ServiceStore.SetLevel(e.PerkId, 2);
                Notify(LangHelper.T(perk.DisplayName + " 升级到 Lv2", perk.DisplayName + " upgraded to Lv2"));
                Core.LogMsg("[B2R] 已升级 " + e.PerkId + " → Lv2");
                ReapplyCurrentClientBudget(); // FIX17b：升级即重算当前客户预算（好感倍率立即按新等级）
                if (e.PerkId == WandererPerk.PerkId) { WandererPerk.GiveUpgradeItems(); Notify(LangHelper.T("流浪者再赠 1 工具 + 1 日用品", "Wanderer gives 1 tool + 1 household item")); }
            }
            else if (lv == 2)
            {
                B2ServiceStore.SetLevel(e.PerkId, 3);
                Notify(LangHelper.T(perk.DisplayName + " 升级到 Lv3（满级）", perk.DisplayName + " upgraded to Lv3 (MAX)"));
                Core.LogMsg("[B2R] 已升级 " + e.PerkId + " → Lv3");
                ReapplyCurrentClientBudget(); // FIX17b：蛙哥牛逼 Lv3 → 当前客户好感倍率 ×4→×5 立即生效
                if (e.PerkId == WandererPerk.PerkId)
                {
                    // 10-17 用户拍板：重回第一天迁流浪者 Lv3（满级触发，对齐原服务价 20000；Lv3 不再赠物）
                    ApplyWandererDayOne();
                    Notify(LangHelper.T("流浪者 Lv3：重回第一天！时间拨回第1天，商店已清空并用好物池刷新", "Wanderer Lv3: Back to Day 1! Time reset, store restocked"));
                }
            }
            RefreshDetail(e.Id);
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OnUnlock 异常: " + ex.Message); }
    }

    // ============ 净化购买（10-15：保留正面/中性加成，只移除负面 debuff） ============
    // 净化价 = 消除价 × 2（童叟无欺 4000 / 招贼体质 10000）
    private static int PurifyPrice(WildEntry e, CustomStartingPerk perk)
    {
        try
        {
            int basePrice = e.PerkId == ThiefMagnetPerk.PerkId ? 5000 : WageBrother.PriceForCost(perk.Cost);
            return basePrice * 2;
        }
        catch { return 4000; }
    }

    private static void ShowPurifyWindow(WildEntry e, CustomStartingPerk perk)
    {
        try
        {
            if (PlayerStore.Instance == null) return;
            var mgr = CustomUIManager.Instance; if (mgr == null) return;
            if (mgr.IsOpen("b2_purify_window")) mgr.CloseWindow("b2_purify_window");
            var w = mgr.CreateWindow("b2_purify_window", LangHelper.T("净化 · 保留加成", "Purify · Keep the bonus"), "overlay");
            if (w == null) return;
            w.SetSize(380, 300).SetPosition(Vector2.zero);
            w.BeginColumn(4f);
            w.AddLabel(LangHelper.T("蛙哥：这招能去邪气，也能只去邪气留本事。", "Wage Brother: I can remove the whole perk, or just the bad part."), "b2_purify_hint");
            var ps = PlayerStore.Instance;
            w.AddLabel(LangHelper.T("当前现金：" + ps.playerCash, "Cash: " + ps.playerCash), "b2_purify_cash");
            string bonus = perk.Id == ThiefMagnetPerk.PerkId
                ? LangHelper.T("保留：黑市买家每日上门（额外销售渠道）", "Keep: black-market buyers visit daily (extra sales)")
                : LangHelper.T("保留：声誉获取 +25%（客户更信任你）", "Keep: reputation gain +25% (customers trust you)");
            string debuff = perk.Id == ThiefMagnetPerk.PerkId
                ? LangHelper.T("移除：治安检查 +20% 与夜间丢物", "Remove: inspection +20% and nightly theft")
                : LangHelper.T("移除：卖出收益 -25%", "Remove: sale income -25%");
            w.AddLabel(bonus, "b2_purify_bonus");
            w.AddLabel(debuff, "b2_purify_debuff");
            int elimPrice = e.PerkId == ThiefMagnetPerk.PerkId ? 5000 : WageBrother.PriceForCost(perk.Cost);
            int purifyPrice = PurifyPrice(e, perk);
            var elimAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => {
                try
                {
                    if (ps.playerCash < elimPrice) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
                    ps.playerCash -= elimPrice;
                    StartingPerk.RemovePerk(perk.Id);
                    B2ServiceStore.Eliminate(perk.Id);
                    StoreUIManager.Instance.Notify(LangHelper.T("蛙哥收了" + elimPrice + "块，" + perk.DisplayName + "整个消了", "Removed " + perk.DisplayName + " for " + elimPrice));
                    Core.LogMsg("[B2R] 净化窗消除 " + perk.Id + "，扣 " + elimPrice);
                    RefreshDetail(e.Id);
                    try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("b2_purify_window"); } catch { }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] 净化窗消除异常: " + ex.Message); }
            }));
            var purifyAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => {
                try
                {
                    if (ps.playerCash < purifyPrice) { StoreUIManager.Instance.Notify(LangHelper.T("钱不够", "Not enough credits")); return; }
                    ps.playerCash -= purifyPrice;
                    B2ServiceStore.Purify(perk.Id);
                    StoreUIManager.Instance.Notify(LangHelper.T("蛙哥收了" + purifyPrice + "块，" + perk.DisplayName + "净化完成——加成还在，邪气去了", "Purified " + perk.DisplayName + " for " + purifyPrice + " - bonus kept, debuff gone"));
                    Core.LogMsg("[B2R] 净化 " + perk.Id + "，扣 " + purifyPrice);
                    RefreshDetail(e.Id);
                    try { if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow("b2_purify_window"); } catch { }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] 净化异常: " + ex.Message); }
            }));
            w.AddButton(LangHelper.T("彻底消除 · " + elimPrice + " 信用点", "Remove entirely · " + elimPrice + " cr"), elimAct, "b2_purify_elim");
            w.AddButton(LangHelper.T("净化 · " + purifyPrice + " 信用点", "Purify · " + purifyPrice + " cr"), purifyAct, "b2_purify_do");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] ShowPurifyWindow 异常: " + ex.Message); }
    }

    // FIX17b：升级后重算当前在场客户预算（蛙哥牛逼 Lv3 ×4→×5 立即生效；无客户/未覆盖则静默跳过）
    private static void ReapplyCurrentClientBudget()
    {
        try
        {
            var ps = PlayerStore.Instance;
            var client = ps?.currentClientInstance?.storeClient;
            if (client == null) return;
            WageGirlSystem.ReapplyBudgetFor(client);
        }
        catch (System.Exception ex) { Core.LogMsg("[B2R] 升级重算预算异常: " + ex.Message); }
    }

    // 附赠开局物品：只有真实开局发物的特性才附赠（盘点 10-14 实锤）
    // 捡漏直觉 = 拾荒者工具箱 + 加强探测器 + 虚空珠（TryGiveKit 幂等：_kitGiven + HasAnyVoidBeadStorage 防双份）
    // 命运之骰 = 命运骰（GiveIfActive 幂等：_diceGiven 防双份）
    // 其余 19 条 OnNewGame 无实际发物 → 购买仅激活/升级（附赠表空）
    private static void GrantGift(string perkId)
    {
        try
        {
            if (perkId == LuckScoutPerk.PerkId)
            {
                LuckScoutPerk.TryGiveKit();
                Core.LogMsg("[B2R] 捡漏直觉附赠套装触发（工具箱+探测器+虚空珠，防重由 TryGiveKit 内部保证）");
            }
            else if (perkId == DestinyDicePerk.PerkId)
            {
                DestinyDicePerk.GiveIfActive();
                Core.LogMsg("[B2R] 命运之骰附赠触发（命运骰，防重由 GiveIfActive 内部保证）");
            }
            else if (perkId == WandererPerk.PerkId)
            {
                WandererPerk.GiveRandomItems();
                Core.LogMsg("[B2R] 流浪者附赠触发（工具1+日用品1+随机4；开局清空/现金随机的 debuff 只在开局发生，B2 购买=纯加成面）");
            }
            else if (perkId == WageGirlPerk.PerkId)
            {
                WageGirlSystem.GiveEntityNow();
                Core.LogMsg("[B2R] 蛙娘附赠触发（立即发实体，防重由 TryGiveToBackpack 内部 ExistsInScene 保证）");
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 附赠异常 " + perkId + ": " + ex.Message); }
    }

    private static void RefreshDetail(string id)
    {
        try
        {
            var wui = WildUIManager.Instance;
            if (wui != null) wui.OnElementClicked(id); // 刷新右侧详情（仿原版 OnUnlockClicked 尾部调用）
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 刷新详情异常: " + ex.Message); }
    }

    private static void Notify(string msg)
    {
        try { StoreUIManager.Instance.Notify(msg); } catch (Exception ex) { Core.LogMsg("[B2R] Notify异常: " + ex.Message); }
    }

    // ================= ④ 动态克隆元素（网络面板静态 31 槽，注入不自动渲染） =================
    // FIX3（2026-10-14）：networkElements List 实测为空（拆包 C5 .ctor new 空 List 实锤，prefab 拖拽仅是推断）
    //   → 原版 31 条 = panel 子树下的 prefab 静态元素，不在 List 里；分流/克隆必须 GetComponentsInChildren 扫子树
    //   → OpenUI 返回时元素子树未填充，延迟 0.5s 再执行（等 prefab 反序列化/填充完成）
    public static void PostfixOpenUI(WildUIManager __instance)
    {
        try { MelonCoroutines.Start(ApplyWageModeDelayed(__instance)); }
        catch (Exception ex) { Core.LogMsg("[B2R] OpenUI 启动延迟分流异常: " + ex.Message); }
    }

    private static System.Collections.IEnumerator ApplyWageModeDelayed(WildUIManager ui)
    {
        yield return new WaitForSeconds(0.5f); // 等 panel 子树元素就绪（OpenUI 返回时 List/子树为空）
        try
        {
            EnsureElements(ui);
            ApplyWageMode(ui);
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OpenUI 延迟分流异常: " + ex.Message); }
    }

    private static int CountB2Elements(WildUIManager ui)
    {
        try
        {
            if (ui == null || ui.panel == null) return 0;
            var all = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
            if (all == null || all.Length == 0) return 0;
            int n = 0;
            foreach (var el in all)
                if (el != null && el.id != null && el.id.StartsWith(PREFIX)) n++;
            return n;
        }
        catch { return 0; }
    }

    // FIX18（2026-10-17 P0）：面板重建自愈——读档/回主菜单会卸载 store 场景，WildUI 面板 prefab 重建，
    //   克隆的 b2 元素随旧面板销毁，但 _cloned 静态标记仍 true → EnsureElements 短路 → 蛙哥模式 b2=0 空壳
    //   + 顶部标题/分组标签缓存指向已销毁对象（Latest.log 实锤：03:17:57 读档后 03:18:10 分流 total=68→35 b2=33→0）。
    //   每次打开先校验子树 b2 计数，不完整即判定面板重建 → 清残留 + 重置全部缓存引用 → 重新克隆。
    private static void ResetPanelCaches()
    {
        _cloned = false;
        _headerTmp = null;
        _headerSearched = false;
        _groupLabels = null;
        _favorTmp = null;
        _containerGeoRecorded = false;
        _geoContainer = null;
        _geoAnchorPersisted = false; // FIX20：读档重建后重新记录新容器基准锚点
        // FIX11「恢复清单不 Clear」仅在同一面板实例内有意义；实例重建后旧 Transform 全失效，必须清空
        _emptyContainerOrig.Clear();
        _b2Containers.Clear();
    }

    private static void EnsureElements(WildUIManager ui)
    {
        if (ui == null || ui.panel == null) return;
        if (_cloned)
        {
            int b2 = CountB2Elements(ui);
            if (b2 >= Entries.Length) return; // 克隆完整（面板未重建）
            Core.LogMsg($"[B2R] 面板重建检测: 子树 b2={b2}/{Entries.Length} → 重置克隆状态并重新克隆");
            // 清残留 b2 元素（防部分残留重复克隆；DestroyImmediate 在协程中安全）
            var stale = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
            foreach (var el in stale)
            {
                if (el == null || el.id == null || !el.id.StartsWith(PREFIX)) continue;
                try { UnityEngine.Object.DestroyImmediate(el.gameObject); } catch { }
            }
            ResetPanelCaches();
        }
        var all = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
        if (all == null || all.Length == 0) { Core.LogMsg("[B2R] 未找到任何 NetworkElement（子树无模板，跳过克隆）"); return; }
        var tpl = all[0];
        if (tpl == null || tpl.gameObject == null) return;
        Transform parent = tpl.gameObject.transform.parent;
        if (parent == null) return;

        // FIX4：克隆到模板容器后手动网格定位（4 列）——原版容器无自动布局接管运行时子物体（实测 22 条全叠在模板槽位）
        //   禁用容器 LayoutGroup（若有），避免布局组件覆盖手动坐标
        try
        {
            var lg = parent.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (lg != null) lg.enabled = false;
        }
        catch { }
        RectTransform tplRect = tpl.gameObject.GetComponent<RectTransform>();
        float cellW = tplRect != null && tplRect.rect.width > 1 ? tplRect.rect.width : 120f;
        float cellH = tplRect != null && tplRect.rect.height > 1 ? tplRect.rect.height : 120f;
        // FIX7 诊断：父容器/可视区尺寸（判断第二行起不可见=Mask 裁切 or 坐标出界）
        try
        {
            var pRect = parent.GetComponent<RectTransform>();
            var rootRect = ui.panel.gameObject.GetComponent<RectTransform>();
            Core.LogMsg($"[B2R] 容器 parent={parent.name} rect=({(pRect != null ? pRect.rect.width : -1):F0}x{(pRect != null ? pRect.rect.height : -1):F0}) pivot=({(pRect != null ? pRect.pivot.x : -1):F1},{(pRect != null ? pRect.pivot.y : -1):F1}) anchored=({(pRect != null ? pRect.anchoredPosition.x : -1):F0},{(pRect != null ? pRect.anchoredPosition.y : -1):F0}) sibling={parent.GetSiblingIndex()}");
            Core.LogMsg($"[B2R] 面板根 rect=({(rootRect != null ? rootRect.rect.width : -1):F0}x{(rootRect != null ? rootRect.rect.height : -1):F0}) 模板 local=({(tplRect != null ? tplRect.localPosition.x : -1):F0},{(tplRect != null ? tplRect.localPosition.y : -1):F0}) rect=({(tplRect != null ? tplRect.rect.width : -1):F0}x{(tplRect != null ? tplRect.rect.height : -1):F0}) sibling={tpl.gameObject.transform.GetSiblingIndex()}");
            var canvas = ui.panel.gameObject.GetComponentInParent<Canvas>();
            Core.LogMsg($"[B2R] 层级: 模板路径={GetTransformPath(tpl.gameObject.transform)} canvasSorting={(canvas != null ? canvas.sortingOrder : -999)} panel下Canvas数={ui.panel.gameObject.GetComponentsInChildren<Canvas>(true).Length}");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 容器诊断异常: " + ex.Message); }
        // FIX12：卡片间距 14→30（玩家"服务卡不要挤在一起"）——容器扩高逻辑会自动跟随 needH 增长
        const float spacing = 30f;
        // FIX14（10-16 玩家"图标横向排满面板之后再往下排列"）：列数=面板可视宽自适应（原固定 4 列→面板 1250 只放 4 列浪费）
        //   1250 宽 / (cell 120+spacing 30) ≈ 8 列 → 图标铺满横向再换行；最少 4 列（窄面板兜底）
        int cols = 4;
        try
        {
            var pRect0 = parent.GetComponent<RectTransform>();
            if (pRect0 != null && pRect0.rect.width > 1)
            {
                int fit = (int)((pRect0.rect.width - 20f) / (cellW + spacing));
                if (fit > cols) cols = fit;
            }
        }
        catch { }
        int idx = 0;
        foreach (var e in Entries)
        {
            try
            {
                var go = UnityEngine.Object.Instantiate(tpl.gameObject, parent);
                if (go == null) continue;
                go.name = "b2_" + e.Id;
                var el = go.GetComponent<NetworkElement>();
                if (el == null) continue;
                el.id = e.Id; // public 字段（dump.cs:43053）
                // 重绑按钮：移除克隆自模板的旧监听（绑定原版 id），绑定我们的 id → OnElementClicked
                if (el.button != null)
                {
                    el.button.onClick.RemoveAllListeners();
                    string capturedId = e.Id;
                    el.button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(
                        (System.Action)(() => WildUIManager.Instance.OnElementClicked(capturedId))));
                }
                // FIX4：手动 4 列网格（相对模板槽位偏移）
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    int row = idx / cols, col = idx % cols;
                    float baseX = tplRect != null ? tplRect.localPosition.x : 0f;
                    float baseY = tplRect != null ? tplRect.localPosition.y : 0f;
                    rt.localPosition = new Vector3(baseX + col * (cellW + spacing), baseY - row * (cellH + spacing), 0f);
                    // FIX7 诊断：打印坐标定位"第二行起不可见"根因（y 方向/容器裁切/可视区）
                    if (idx < 8 || idx == Entries.Length - 1)
                        Core.LogMsg($"[B2R] 网格 idx={idx} id={e.Id} row={row} col={col} pos=({rt.localPosition.x:F1},{rt.localPosition.y:F1}) 模板=({baseX:F1},{baseY:F1}) cell=({cellW:F0},{cellH:F0}) sibling={go.transform.GetSiblingIndex()}");
                }
                // FIX6c：icon 显示尺寸诊断（仿原版图标格式=制作规格依据；拆包 Q3 资源层缺失→运行时读 sizeDelta 最快）
                try
                {
                    if (el.icon != null)
                    {
                        var iconRt = el.icon.GetComponent<RectTransform>();
                        if (iconRt != null && (idx < 4 || idx == Entries.Length - 1))
                            Core.LogMsg($"[B2R] ICON 尺寸 idx={idx} id={e.Id} iconSize=({iconRt.rect.width:F1}x{iconRt.rect.height:F1}) iconLocal=({iconRt.localPosition.x:F1},{iconRt.localPosition.y:F1})");
                    }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] ICON 尺寸诊断异常 " + e.Id + ": " + ex.Message); }
                // FIX6c + 10-15 蛙娘拆分：perk 卡按 PerkId 映射图标（索引无关）；吞噬季/服务条目走 Icon(idx) 兜底
                // FIX14（10-16 玩家"很多图标没有生产"）：服务条目（PerkId=null）原走 Icon(idx) 超出 22 数组→null→空白；
                //   改按 ServiceId 映射（B2Icons.ServiceIcon）；PerkIcon 未命中也兜底 Icon(idx)，防空白
                try
                {
                    if (el.icon != null)
                    {
                        Sprite sp = null;
                        if (e.PerkId != null) sp = B2Icons.PerkIcon(e.PerkId);
                        else if (e.ServiceId != null) sp = B2Icons.ServiceIcon(e.ServiceId);
                        else sp = B2Icons.Icon(idx);
                        if (sp == null && e.PerkId != null) sp = B2Icons.Icon(idx); // perk 映射缺失兜底（不空白）
                        if (sp != null) el.icon.sprite = sp;
                    }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] 图标绑定异常 " + e.Id + ": " + ex.Message); }
                go.SetActive(true);
                idx++;
            }
            catch (Exception ex) { Core.LogMsg("[B2R] 克隆元素失败 " + e.Id + ": " + ex.Message); }
        }
        _cloned = true;
        Core.LogMsg("[B2R] 已克隆 " + Entries.Length + " 个元素到面板子树（模板=子树首个 NetworkElement，4 列网格布局）");
        // FIX19（2026-10-17 P0 补齐）：容器几何抽独立方法——每次蛙哥打开都幂等应用。
        //   原实现内联在克隆尾部，二次打开 _cloned 短路 → 扩展/置顶不再执行 → 「被挡住」复现
        //   （Latest.log 实锤：03:15:53 FIX11 还原 180 后，03:15:57 蛙哥打开无 FIX8 扩展日志）。
        EnsureContainerGeometry(ui);
    }

    // FIX19：容器高度扩展 + 层级置顶（原 FIX8 逻辑抽独立方法，根治"第二行起不可见"/"被挡住"）。
    //   幂等：needH > 当前高才扩展（已扩展不重复）；置顶每次执行（蛙哥模式防装饰/横条覆盖）。
    //   调用点：EnsureElements 克隆后（首次布局）+ ApplyWageMode 蛙哥分支（每次打开自愈，
    //     覆盖「先开王尔德正常 → 再开蛙哥被挡」顺序相关回归）。
    private static void EnsureContainerGeometry(WildUIManager ui)
    {
        try
        {
            if (ui == null || ui.panel == null) return;
            var all = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
            if (all == null || all.Length == 0) return;
            // FIX38（2026-10-17 FIX37 实测残留）：all[0] 为原版卡时（GetComponentsInChildren 层级顺序）→ 误扩展原版容器
            //   （日志实锤 Panel1 (1)/(2) anchored=192 残留=被 FIX8 误动）。只扩展明确的 b2 卡父容器（id 以 PREFIX 开头）。
            Transform parent = null;
            NetworkElement b2el = null;
            foreach (var el0 in all)
            {
                if (el0 == null || el0.gameObject == null || el0.transform == null || el0.transform.parent == null) continue;
                if (el0.id != null && el0.id.StartsWith(PREFIX))
                {
                    parent = el0.transform.parent;
                    b2el = el0;
                    break;
                }
            }
            if (parent == null) { Core.LogMsg("[B2R] FIX38 未找到 b2 卡父容器（面板结构不符预期，跳过容器扩展）"); return; }
            RectTransform tplRect = b2el != null ? b2el.gameObject.GetComponent<RectTransform>() : null;
            float cellW = tplRect != null && tplRect.rect.width > 1 ? tplRect.rect.width : 120f;
            float cellH = tplRect != null && tplRect.rect.height > 1 ? tplRect.rect.height : 120f;
            const float spacing = 30f; // FIX12：卡片间距 14→30（容器扩高逻辑自动跟随 needH 增长）
            int cols = 4; // FIX14：列数=面板可视宽自适应（最少 4 列兜底），与克隆网格同算法
            try
            {
                var pRect0 = parent.GetComponent<RectTransform>();
                if (pRect0 != null && pRect0.rect.width > 1)
                {
                    int fit = (int)((pRect0.rect.width - 20f) / (cellW + spacing));
                    if (fit > cols) cols = fit;
                }
            }
            catch { }
            int rows = (Entries.Length + cols - 1) / cols;
            float needH = rows * cellH + (rows - 1) * spacing + 40f;
            var pRect2 = parent.GetComponent<RectTransform>();
            if (pRect2 != null && needH > pRect2.rect.height + 0.5f)
            {
                float oldH = pRect2.rect.height;
                Vector2 sd = pRect2.sizeDelta;
                // FIX11：记录原始几何（CloseUI/王尔德时还原，防污染原版面板）
                if (!_containerGeoRecorded)
                {
                    _containerGeoRecorded = true;
                    _geoContainer = pRect2;
                    _containerGeoOrig = sd;
                    _containerGeoAnchor = pRect2.anchoredPosition;
                }
                // FIX37（2026-10-17 Q5 拆包实锤终局）：原版零几何写（WildUIManager 全文件 anchoredPosition/sizeDelta 0 命中）——
                //   漂移链=FIX20 读当前值当基准（读到的是已漂移值）→FIX11 还原到漂移位置。根治：扩展 anchored 永远写固定常量
                //   (1000, 367-Δ/2)（王尔德实测 x=1000/y=367 系=原版 prefab 静态值），绝不读回当前值当基准。
                pRect2.sizeDelta = new Vector2(sd.x, needH);
                // FIX10：pivot=(0.5,0.5) 时 sizeDelta 扩高会让容器中心不动→顶部上顶 Δ/2，超出面板上框架
                //   （玩家实测：'不要超过上边的框架，往下延展'）→ 补偿：anchoredPosition 下移 Δ/2，
                //   顶部保持原位不动、仅向下延展
                float delta = needH - oldH;
                pRect2.anchoredPosition = new Vector2(1000f, 367f - delta * 0.5f);
                Core.LogMsg($"[B2R] FIX8 容器高度扩展: {oldH:F0} → {needH:F0}（rows={rows} cellH={cellH:F0} spacing={spacing:F0}）下移补偿={delta * 0.5f:F0} 固定基准=(1000,367)");
            }
            if (_wageMode) parent.SetAsLastSibling(); // 蛙哥模式容器渲染置顶（防装饰/横条覆盖）
            // Mask/ScrollRect 诊断（父链）——若扩展后仍不可见，据此定位裁切源
            var mask = parent.GetComponentInParent<UnityEngine.UI.RectMask2D>();
            var scroll = parent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            Core.LogMsg($"[B2R] FIX8 裁切源诊断: RectMask2D={(mask != null ? mask.name : "无")} ScrollRect={(scroll != null ? scroll.name : "无")} 容器rect后={(pRect2 != null ? pRect2.rect.height : -1):F0} anchored={(pRect2 != null ? pRect2.anchoredPosition : UnityEngine.Vector2.zero)}");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] FIX8 容器扩展异常: " + ex.Message); }
    }

    // ================= ⑤ 蛙哥电话 1005 =================
    // 注册：电话簿直接可见（state=4 + dialedBefore=true，胡安解锁态蓝本）
    public static void PostfixInitPhoneClientDict(Il2CppSystem.Collections.Generic.Dictionary<long, StorePhoneClient> __result)
    {
        try { EnsurePhoneRegistered(__result); }
        catch (Exception ex) { Core.LogMsg("[B2R] 电话注册异常: " + ex.Message); }
    }

    // 10-16 闪退修复：原挂 StorePhoneClient.InitPhoneClientDict（返回泛型 Dictionary）触发 Il2CppInterop 栈溢出；
    //   拆包实锤 InitPhoneClientDict 全库仅 1 调用=StartNewGame（PlayerStore.txt:2080）→ 改挂 StartNewGame(非泛型) 同点注册。
    //   读档不重建 PhoneClientDict（ES3 随档恢复）→ 旧档仍走 EnsurePhoneRegistered() 无参兜底（OnStoreGameLoaded，Patches.Lifecycle.cs:541）。
    public static void PostfixStartNewGame_EnsurePhone()
    {
        try { EnsurePhoneRegistered(); }
        catch (Exception ex) { Core.LogMsg("[B2R] StartNewGame 电话注册异常: " + ex.Message); }
    }

    // 幂等注册（ContainsKey 防重）：新档走 StartNewGame Postfix；旧档走 OnStoreGameLoaded 兜底
    // 拆包实锤（DB-PHONE-01 C2）：InitPhoneClientDict 全库仅 1 调用=StartNewGame（PlayerStore.txt:2080），
    //   读档不重建 PhoneClientDict（整对象 ES3 随档恢复）→ 旧档缺蛙哥 1005，必须读档后补注册
    internal static void EnsurePhoneRegistered(Il2CppSystem.Collections.Generic.Dictionary<long, StorePhoneClient> dict)
    {
        if (dict == null) return;
        if (!dict.ContainsKey(WAGE_PHONE_NUMBER))
        {
            var pc = new StorePhoneClient();
            pc.phoneClientType = StorePhoneClient.PhoneClientType.Supplier;
            pc.phoneState = (StorePhoneClient.PhoneState)4; // 解锁态（电话簿可见）
            pc.displayName = LangHelper.T("蛙娘", "Wage Brother"); // 2026-10-14 显示名纠正：蛙哥→蛙娘
            pc.locID = "";
            pc.dialogFuncId = WAGE_DIALOG_ID; // B2-R2：走蛙哥对话链（GetCallDialog 按 dialogFuncId 查 DialogFuncs）
            pc.cooldownDuration = 0;
            pc.currentCooldown = 0;
            pc.dialedBefore = true; // 直接可拨（无需先到店）
            dict.Add(WAGE_PHONE_NUMBER, pc);
            Core.LogMsg("[B2R] 蛙哥电话 1005 已注册（电话簿直接可见）");
        }
        // FIX17b：对话工厂注册移出「电话已存在」分支——10-15 实测日志「蛙哥对话未注册，回退直开面板」：
        //   旧档电话号已注册 → 直接 return → RegisterWageBrotherDialog 从未执行 → 拨号无对话文字。幂等 ContainsKey 防重。
        RegisterWageBrotherDialog();
    }

    // 读档兜底：OnStoreGameLoaded 时从已加载的 PhoneClientDict 实例上补注册（B2-R-H1）
    internal static void EnsurePhoneRegistered()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null) return;
            EnsurePhoneRegistered(ps.PhoneClientDict);
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 读档电话兜底异常: " + ex.Message); }
    }

    // 10-19 审阅落地（假消除·第四层）：老档一次性迁移——标志位 owned_<id>=-1 但原版列表仍在 → 补删。
    // 幂等：列表已删的会被 IsPerkActive 过滤；重复读档不重复删。只修 Cost<0 且 IsEliminated 且列表仍在 三条件同时成立。
    internal static void RepairFakeEliminations()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps == null || ps.startingPerks == null) return;
            int fixedCount = 0;
            foreach (var perk in CustomStartingPerks.All)
            {
                if (perk.Cost >= 0) continue;
                if (!B2ServiceStore.IsEliminated(perk.Id)) continue; // 只修"标记已消"的
                if (!StartingPerk.IsPerkActive(perk.Id)) continue;    // 列表已无 → 本来就对
                try { StartingPerk.RemovePerk(perk.Id); fixedCount++; } catch { }
            }
            if (fixedCount > 0) Core.LogMsg("[B2R] 假消除修复：补删 " + fixedCount + " 个负面特性（老档）");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] RepairFakeEliminations 异常: " + ex.Message); }
    }

    // 拨号拦截：1005 走原版对话链（dialogFuncId="WageBrother" → DialogFuncs）；仅当对话工厂注册失败时回退直开面板
    public static bool PrefixStartPhoneDialog(long currentNumber)
    {
        // 10-19 探针：无论放行/回退都打（原实现仅回退/异常才打——放行路径对 1005 静默，来电"无响应"无法从日志定位）
        Core.LogMsg("[B2R] Probe StartPhoneDialog: " + currentNumber);
        try
        {
            if (currentNumber != WAGE_PHONE_NUMBER) return true; // 放行其他号码（含王尔德 8376）
            var dict = Il2Cpp.PhoneClientList.DialogFuncs;
            Core.LogMsg("[B2R] Probe 1005 dict=" + (dict == null ? "NULL" : "count=" + dict.Count)
                + " hasWageBrother=" + (dict != null && dict.ContainsKey(WAGE_DIALOG_ID)));
            if (dict != null && dict.ContainsKey(WAGE_DIALOG_ID)) return true; // 对话就绪 → 放行原版 GetCallDialog
            // 兜底：对话未注册（异常/为空）→ 直开面板（B2-R 老行为）
            Core.LogMsg("[B2R] 蛙哥对话未注册，回退直开面板（1005）");
            var wui = WildUIManager.Instance;
            if (wui == null) { Core.LogMsg("[B2R] WildUIManager 为空，无法打开面板"); return false; }
            try { PhoneUIManager.Instance.StopCall(); } catch { }
            _wageMode = true;
            wui.OpenUI(false);
            Core.LogMsg("[B2R] 蛙哥电话 1005 拨通（兜底）→ 打开原生查看服务面板");
            return false; // 拦原版对话
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 拨号拦截异常: " + ex.Message); return true; }
    }

    // ================= ⑥ 蛙哥电话对话链 + 面板蛙哥化（B2-R2） =================
    // 拆包蓝本：王尔德 PhoneWild（PhoneDialogList.txt:965-1433）——主对话 SetText(说话人,台词) +
    //   AddChoice×2（图标 DirectoryMaster.Item("$",1)）；选项 Action=end-action：
    //   「浏览服务」b__0=OpenUI+CloseUI+OnPhoneDialogEnd(0)（WildUIManager.txt:400-433）／「挂断」b__1=OnPhoneDialogEnd(1)
    // DialogFuncs 分发（DB-PHONE-01 P1）：GetCallDialog 按 dialogFuncId 查 DialogFuncs 静态字典（PhoneClientList .cctor 建）
    private const string WAGE_DIALOG_ID = "WageBrother";
    private static bool _wageMode = false; // 蛙哥面板分流标记（防污染王尔德路径）

    // 注册对话工厂（幂等 ContainsKey）；EnsurePhoneRegistered 里已调
    internal static void RegisterWageBrotherDialog()
    {
        try
        {
            var dict = Il2Cpp.PhoneClientList.DialogFuncs;
            // 10-19 探针：注册路径可见性（此前"已注册/已存在/为空"日志全缺，无法判断链路走到哪）
            Core.LogMsg("[B2R] Probe RegisterWageBrotherDialog: dict=" + (dict == null ? "NULL" : "count=" + dict.Count)
                + " hasWageBrother=" + (dict != null && dict.ContainsKey(WAGE_DIALOG_ID)));
            if (dict == null) { Core.LogMsg("[B2R] DialogFuncs 为空，对话注册跳过"); return; }
            if (dict.ContainsKey(WAGE_DIALOG_ID)) return; // 幂等
            var sysFunc = new System.Func<Il2Cpp.StorePhoneClient, Il2Cpp.Dialogue>(BuildWageBrotherDialogue);
            dict.Add(WAGE_DIALOG_ID, DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.StorePhoneClient, Il2Cpp.Dialogue>>((System.Delegate)sysFunc));
            Core.LogMsg("[B2R] DialogFuncs[\"WageBrother\"] 已注册（蛙哥电话对话链）");
            // 10-19 探针：注册后复核（Add 后原版消费侧是否可见）
            Core.LogMsg("[B2R] Probe 注册后复核: hasWageBrother=" + dict.ContainsKey(WAGE_DIALOG_ID));
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 对话注册异常: " + ex.Message); }
    }

    // 构建蛙哥对话：主台词 + 2 选项（查看服务/再见）
    // B2-R2-D 实锤修正：AddChoice 的 name 参数电话 UI 不显示——选项文本=choiceNextDialogue.plainText（DisplayChoice :1908-1917）；
    //   王尔德选项=choiceAction+choiceNextDialogue 双绑（SelectChoice 两条同步执行 :2168-2404）→ 每个选项必须绑 PLAYER 台词对话
    private static Il2Cpp.Dialogue BuildWageBrotherDialogue(Il2Cpp.StorePhoneClient client)
    {
        try
        {
            var main = new Il2Cpp.Dialogue();
            main.SetText(
                LangHelper.T("蛙哥", "Wage Brother"),
                LangHelper.T("这里是蛙哥。需要查看服务、购买特性，还是调整吞噬季？",
                             "This is Wage Brother. Need services, perks, or cannibalism settings?"));
            var icon = Il2Cpp.DirectoryMaster.Item("$", true); // 王尔德同款钱币图标（Item(string,bool) 实锤签名）
            // 选项A：查看服务 → 点击即直开面板（王尔德 b__0 蓝本：OpenUI+CloseUI+OnPhoneDialogEnd，零中间对话）
            //   B2-R2-D 实锤：AddChoice 的 Item3=action 点击同步执行；选项文本=choiceNextDialogue(playDlgA).plainText
            //   2026-10-14 简化：去掉 PLAYER台词→蛙哥台词3→endAction 两步推进（玩家容易卡在中间导致面板不开→误判失败）
            var playDlgA = new Il2Cpp.Dialogue().SetText("PLAYER", LangHelper.T("查看服务", "View services"));
            var openAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
            {
                try
                {
                    _wageMode = true;
                    var wui = WildUIManager.Instance;
                    if (wui != null) wui.OpenUI(false);
                    try { PhoneUIManager.Instance.CloseUI(); } catch { }
                    try { PhoneUIManager.Instance.OnPhoneDialogEnd(false); } catch { }
                    Core.LogMsg("[B2R] 查看服务 → 直接打开蛙哥人脉网络");
                }
                catch (Exception ex) { Core.LogMsg("[B2R] 打开面板异常: " + ex.Message); }
            }));
            main.AddChoice(icon, "", "", playDlgA, openAct, null);
            // 10-17 吞噬季电话控制：已购 → 按状态显示「开始吞噬季/终止吞噬季」（随时开始、随时终止，动态单选项）
            if (B2ServiceStore.IsCannibalOwned)
            {
                bool manualActive = B2ServiceStore.CannibalManualStartDay > 0;
                if (manualActive)
                {
                    var playDlgStop = new Il2Cpp.Dialogue().SetText("PLAYER", LangHelper.T("终止吞噬季", "Stop cannibalism season"));
                    var stopAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
                    {
                        try
                        {
                            B2ServiceStore.SetCannibalManualStartDay(0);
                            Notify(LangHelper.T("吞噬季已终止（回归原调度）", "Cannibalism season stopped (back to schedule)"));
                            Core.LogMsg("[B2R] 电话终止吞噬季（手动窗口清零）");
                        }
                        catch (Exception ex) { Core.LogMsg("[B2R] 电话终止吞噬季异常: " + ex.Message); }
                    }));
                    main.AddChoice(icon, "", "", playDlgStop, stopAct, null);
                }
                else
                {
                    var playDlgStart = new Il2Cpp.Dialogue().SetText("PLAYER", LangHelper.T("开始吞噬季", "Start cannibalism season"));
                    var startAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
                    {
                        try
                        {
                            int dayNow = DeterministicSchedule.CurrentDay;
                            // 防重复：原模式今天是否已在自动窗口（自动窗口内不重复手动开启）
                            bool inAutoWindow = false;
                            if (B2ServiceStore.CannibalMode == 0)
                            {
                                int sd = B2ServiceStore.CannibalDay;
                                if (sd > 0 && dayNow >= sd && dayNow - sd <= 2) inAutoWindow = true;
                            }
                            else
                            {
                                int iv = B2ServiceStore.CannibalInterval > 0 ? B2ServiceStore.CannibalInterval : 10;
                                if (dayNow % iv <= 2) inAutoWindow = true;
                            }
                            if (inAutoWindow)
                            {
                                Notify(LangHelper.T("今天已在吞噬季中，无需重复开启", "Cannibalism season is already active today"));
                                return;
                            }
                            B2ServiceStore.SetCannibalManualStartDay(dayNow);
                            ModCannibalism.OnDayStartPostfix(); // 立即执行一次互食（num=0，开始提示+吞噬）
                            Notify(LangHelper.T("吞噬季已手动开始（持续 3 天）", "Cannibalism season started manually (3 days)"));
                            Core.LogMsg("[B2R] 电话开始吞噬季 day=" + dayNow);
                        }
                        catch (Exception ex) { Core.LogMsg("[B2R] 电话开始吞噬季异常: " + ex.Message); }
                    }));
                    main.AddChoice(icon, "", "", playDlgStart, startAct, null);
                }
            }
            // 选项B：再见 → PLAYER 台词「再见」+ 挂断（王尔德 b__1 同款：OnPhoneDialogEnd(1)）
            var playDlgB = new Il2Cpp.Dialogue().SetText("PLAYER", LangHelper.T("再见", "Goodbye"));
            var hangAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
            {
                try { PhoneUIManager.Instance.OnPhoneDialogEnd(true); } catch { }
            }));
            main.AddChoice(icon, "", "", playDlgB, hangAct, null);
            return main;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 对话构建异常: " + ex.Message); }
        return new Il2Cpp.Dialogue(); // 异常兜底：空对话（原版 BugDialog 兜底链仍生效）
    }

    // 蛙哥化面板分流：OpenUI Postfix 应用——蛙哥=标题「蛙哥人脉网络」+只显示 b2_ 22 条；
    // 王尔德路径=原样（原版31显示 + b2_22隐藏，防混排污染）
    // FIX3：不再遍历 networkElements List（实测空），改扫 panel 子树全部 NetworkElement（原版 31 + 克隆 22）
    private static void ApplyWageMode(WildUIManager ui)
    {
        if (ui == null || ui.panel == null) { Core.LogMsg("[B2R] 分流跳过（panel 为空）"); return; }
        var all = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
        if (all == null || all.Length == 0) { Core.LogMsg("[B2R] 分流跳过（子树无 NetworkElement）"); return; }
        int b2Count = 0, total = all.Length;
        foreach (var el in all)
        {
            if (el == null || el.gameObject == null) continue;
            bool isB2 = el.id != null && el.id.StartsWith(PREFIX);
            if (isB2) b2Count++;
            el.gameObject.SetActive(_wageMode ? isB2 : !isB2);
        }
        Core.LogMsg($"[B2R] 分流: _wageMode={_wageMode} total={total} b2={b2Count}（蛙哥=仅b2可见 / 王尔德=仅原版可见）");
        ApplyWageLabels(ui, _wageMode); // FIX4：蛙哥=隐藏原版分组标签（推荐改「蛙哥服务」）；王尔德=恢复
        HideEmptyB2Containers(ui, _wageMode); // FIX9：蛙哥=隐藏原版其他分类容器（空白横条区），只留一个放置位；王尔德=恢复
        if (_wageMode)
        {
            if (ui.title != null)
                ui.title.text = LangHelper.T("蛙哥人脉网络", "Wage Brother Network"); // 右侧详情槽（打开瞬间）
            EnsureContainerGeometry(ui); // FIX19：每次蛙哥打开应用容器几何+置顶（防二次打开"被挡住"）
            ApplyWageHeader(ui); // 顶部页面标题（prefab 静态 TMP，运行时遍历替换——拆包 B2-R2-H3 C1）
            // FIX21 诊断（2026-10-17 实测空壳排查）：0x150 字典 b2 条目数 + 面板 b2 元素 active/图标状态
            try
            {
                int dictB2 = 0;
                try
                {
                    var ps = PlayerStore.Instance;
                    if (ps != null && ps.networkUpgrade != null)
                        foreach (var kv in ps.networkUpgrade) { if (kv.Key != null && kv.Key.StartsWith("b2_")) dictB2++; }
                }
                catch { }
                int elB2 = 0, elActiveB2 = 0; string firstState = "无";
                foreach (var el in all)
                {
                    if (el == null || el.gameObject == null || el.id == null || !el.id.StartsWith(PREFIX)) continue;
                    elB2++;
                    if (el.gameObject.activeSelf) elActiveB2++;
                    if (elB2 == 1)
                    {
                        bool iconOk = false;
                        try { iconOk = el.icon != null && el.icon.gameObject != null && el.icon.gameObject.activeSelf; } catch { }
                        firstState = $"id={el.id} active={el.gameObject.activeSelf} iconActive={iconOk}";
                    }
                }
                Core.LogMsg($"[B2R] FIX21 卡片诊断: 0x150b2条目={dictB2}/{Entries.Length} 面板b2元素={elB2} active={elActiveB2} 首卡[{firstState}]");
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX21 卡片诊断异常: " + ex.Message); }
            // FIX25 诊断（2026-10-17 实测第二轮）：FIX22/23/24 后容器 anchored=192 正常、FIX24 复核无漂移，
            //   但蛙哥卡片仍画面不可见 → 运行时定位首张 b2 卡的容器/卡 worldPosition + activeInHierarchy + Canvas 层级
            try
            {
                NetworkElement firstB2 = null;
                foreach (var el in all)
                {
                    if (el == null || el.gameObject == null || el.id == null || !el.id.StartsWith(PREFIX)) continue;
                    firstB2 = el; break;
                }
                if (firstB2 != null && firstB2.gameObject != null)
                {
                    var parent = firstB2.gameObject.transform.parent;
                    var pRect = parent != null ? parent.GetComponent<RectTransform>() : null;
                    var neRect = firstB2.GetComponent<RectTransform>();
                    if (pRect != null && neRect != null)
                    {
                        var cv = firstB2.gameObject.GetComponentInParent<Canvas>();
                        Core.LogMsg($"[B2R] FIX25 定位: 容器 anchored=({pRect.anchoredPosition.x:F0},{pRect.anchoredPosition.y:F0}) worldPos=({pRect.position.x:F1},{pRect.position.y:F1}) size=({pRect.sizeDelta.x:F0},{pRect.sizeDelta.y:F0}) | 首卡[{firstB2.id}] local=({neRect.localPosition.x:F0},{neRect.localPosition.y:F0}) worldPos=({neRect.position.x:F1},{neRect.position.y:F1}) activeInHierarchy={firstB2.gameObject.activeInHierarchy} canvasSorting={(cv != null ? cv.sortingOrder : -999)}");
                    }
                    else
                    {
                        Core.LogMsg($"[B2R] FIX25 定位: 首卡 [{firstB2.id}] parent/pRect 缺失（parent={firstB2.gameObject.transform.parent?.name ?? "null"}）");
                    }
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX25 定位异常: " + ex.Message); }
            // FIX26 诊断（2026-10-17 实测第二轮追查）：FIX25 显示首卡 worldPos=(712,864) local=(-560,165)（网格设 -10，+175 疑点）
            //   → 补 Canvas 类型/缩放 + 卡片锚点 + 原版卡片对照，并登记 0.5s 延迟复核（UpdateElement 每帧重写卡片位置）
            try
            {
                var firstB2 = (NetworkElement)null;
                foreach (var el in all)
                {
                    if (el == null || el.gameObject == null || el.id == null || !el.id.StartsWith(PREFIX)) continue;
                    firstB2 = el; break;
                }
                if (firstB2 != null && firstB2.gameObject != null)
                {
                    var cv = firstB2.gameObject.GetComponentInParent<Canvas>();
                    var cvRt = cv != null ? cv.GetComponent<RectTransform>() : null;
                    Core.LogMsg($"[B2R] FIX26 Canvas: renderMode={(cv != null ? cv.renderMode : -1)} scaleFactor={(cv != null ? cv.scaleFactor : 0f):F2} rectScale=({(cvRt != null ? cvRt.localScale.x : 0f):F3},{(cvRt != null ? cvRt.localScale.y : 0f):F3}) sorting={(cv != null ? cv.sortingOrder : -999)} Screen=({UnityEngine.Screen.width}x{UnityEngine.Screen.height})");
                    var neRt2 = firstB2.GetComponent<RectTransform>();
                    if (neRt2 != null)
                        Core.LogMsg($"[B2R] FIX26 首卡 anchor=({neRt2.anchorMin.x:F2},{neRt2.anchorMin.y:F2})-({neRt2.anchorMax.x:F2},{neRt2.anchorMax.y:F2}) pivot=({neRt2.pivot.x:F2},{neRt2.pivot.y:F2}) localScale=({neRt2.localScale.x:F2},{neRt2.localScale.y:F2})");
                    // 登记延迟复核（FIX26b 在 PostfixUpdate 执行）
                    _cardCheckRect = neRt2;
                    _cardCheckTime = Time.time + 0.5f;
                }
                // 原版对照：第一张非 b2 卡（蛙哥模式被隐藏但 GetComponentsInChildren(true) 可取）
                foreach (var el in all)
                {
                    if (el == null || el.gameObject == null || el.id == null || el.id.StartsWith(PREFIX)) continue;
                    var or = el.GetComponent<RectTransform>();
                    if (or != null)
                    {
                        Core.LogMsg($"[B2R] FIX26 原版对照卡[{el.id}] local=({or.localPosition.x:F0},{or.localPosition.y:F0}) worldPos=({or.position.x:F1},{or.position.y:F1}) active={el.gameObject.activeSelf}");
                        break;
                    }
                    break;
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX26 对照异常: " + ex.Message); }
            // FIX28 诊断（2026-10-17 实测第三轮）：FIX27 实锤蛙哥卡 worldPos=(712,864) 屏幕内（1920x1080）却不可见
            //   → 渲染遮挡/层级嫌疑 → 打印容器 sibling（SetAsLastSibling 置顶是否生效）+ 兄弟元素拓扑 + 面板子 Canvas
            try
            {
                var firstB2 = (NetworkElement)null;
                foreach (var el in all)
                {
                    if (el == null || el.gameObject == null || el.id == null || !el.id.StartsWith(PREFIX)) continue;
                    firstB2 = el; break;
                }
                if (firstB2 != null && firstB2.gameObject != null)
                {
                    var parent = firstB2.gameObject.transform.parent;
                    if (parent != null)
                    {
                        Core.LogMsg($"[B2R] FIX28 容器 sibling={parent.GetSiblingIndex()} name={parent.name}");
                        if (parent.parent != null)
                        {
                            var sibs = new System.Collections.Generic.List<string>();
                            int cnt = parent.parent.childCount;
                            for (int i = 0; i < cnt; i++)
                            {
                                var c = parent.parent.GetChild(i);
                                if (c == null) continue;
                                sibs.Add($"{c.name}[{i}]{(c == parent ? "←容器" : "")}");
                            }
                            Core.LogMsg($"[B2R] FIX28 父[{parent.parent.name}] 兄弟顺序: {string.Join(" ", sibs)}");
                            // FIX31：蛙哥模式隐藏挡路兄弟容器（Panel1 系列空容器渲染在容器上方盖住 b2 卡——
                            //   FIX9 只隐藏"含 NetworkElement 的容器"，漏掉无子元素的空容器；蛙哥只用 Panel1 (3) 放卡，其余 Panel1 系列隐藏防遮挡）
                            if (_wageMode)
                            {
                                int h2 = 0;
                                for (int i = 0; i < parent.parent.childCount; i++)
                                {
                                    var c = parent.parent.GetChild(i);
                                    if (c == null || c == parent) continue;
                                    string cn = c.name;
                                    if (cn != null && cn.StartsWith("Panel1") && c.gameObject != null && c.gameObject.activeSelf)
                                    {
                                        if (!_hiddenPanelObjs.Contains(c.gameObject)) _hiddenPanelObjs.Add(c.gameObject);
                                        c.gameObject.SetActive(false);
                                        h2++;
                                    }
                                }
                                if (h2 > 0) Core.LogMsg($"[B2R] FIX31 隐藏挡路 Panel1 兄弟容器: {h2} 个（{string.Join(" ", sibs.FindAll(s => s.Contains("Panel1") && !s.Contains("←")))}）");
                            }
                        }
                        // FIX29：登记容器几何时间追踪（PostfixUpdate 每 0.1s 采样，定位扩展后被还原的时间点）
                        _geoTraceRect = parent.GetComponent<RectTransform>();
                        _geoTraceStart = Time.time;
                        _geoTraceBucket = -1;
                        // FIX30：蛙哥容器守护（PostfixUpdate 每帧检查，被原版逻辑还原立即重新扩展+置顶——首次打开不还原、再次打开必还原的差异由原版淡入状态决定）
                        if (_wageMode)
                        {
                            _wageGeoGuardRect = parent.GetComponent<RectTransform>();
                            _wageGeoGuardStart = Time.time;
                            // needH：若登记时容器已异常（<300 即被还原），兜底用业务目标 530；否则用当前扩展高度
                            _wageGeoGuardNeedH = (_wageGeoGuardRect != null && _wageGeoGuardRect.sizeDelta.y > 300f) ? _wageGeoGuardRect.sizeDelta.y : 530f;
                            _wageGeoGuardRestoreCount = 0;
                        }
                    }
                }
                var subCv = ui.panel.gameObject.GetComponentsInChildren<Canvas>(true);
                foreach (var sc in subCv)
                {
                    if (sc == null || sc.gameObject == null) continue;
                    Core.LogMsg($"[B2R] FIX28 子Canvas[{sc.gameObject.name}] sorting={sc.sortingOrder} active={sc.gameObject.activeSelf}");
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX28 遮挡诊断异常: " + ex.Message); }
        }
        else
        {
            // FIX22（2026-10-17 P0 实测）：王尔德打开强制回正容器几何（锚点拉回固定常量）。
            //   FIX37（Q5 实锤终局）：原版零几何写（WildUIManager 全文件 0 命中）——容器 prefab 静态，王尔德各分类 y 不同
            //   （367/-231/-388=各分类布局），**不可**把 all0[0].parent 一律拉 367（all0[0] 随分类切换指向不同容器=误伤）。
            //   只清 FIX8 扩展态残留（y≈192=367-Δ/2 特征值）→ 367；其余容器保持 prefab 静态。
            _wageGeoGuardStart = -1f; _wageGeoGuardRect = null; // FIX32：蛙哥→王尔德切换立即终止守护（防与还原打架）
            RestoreContainerGeometry(); // FIX11 幂等还原（固定常量 (1000,367)/180）
            RestoreHiddenPanelObjs(); // FIX31：恢复蛙哥模式隐藏的挡路兄弟容器（王尔德需要各分类容器显示原版卡）
            try
            {
                var all0 = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
                if (all0 != null && all0.Length > 0 && all0[0] != null && all0[0].gameObject != null)
                {
                    var parent = all0[0].gameObject.transform.parent;
                    var pRect = parent != null ? parent.GetComponent<RectTransform>() : null;
                    if (pRect != null)
                    {
                        Vector2 cur = pRect.anchoredPosition;
                        bool changed = false;
                        // 扩展态特征值 y≈192（FIX8 下移补偿 367-175）→ 拉回 367/180；其他 y 值=prefab 静态不动
                        if (Math.Abs(cur.y - 192f) < 8f)
                        {
                            pRect.anchoredPosition = new Vector2(cur.x, 367f);
                            changed = true;
                        }
                        if (Math.Abs(pRect.sizeDelta.y - 530f) < 10f)
                        {
                            pRect.sizeDelta = new Vector2(pRect.sizeDelta.x, 180f);
                            changed = true;
                        }
                        if (changed) Core.LogMsg($"[B2R] FIX22 王尔德容器清扩展残留: 锚点 {cur.x:F0},{cur.y:F0} → (x,367) size→180（FIX37 固定常量）");
                        // FIX33：登记王尔德容器守护（PostfixUpdate 每帧检查，原版漂移立即拉回 367/180）
                        _wildGeoGuardRect = pRect;
                        _wildGeoGuardStart = Time.time;
                        _wildGeoGuardRestoreCount = 0;
                        // FIX35（2026-10-17 FIX34 实锤）：王尔德打开时 b2 容器残留蛙哥扩展态 192/530 未还原
                        //   FIX11/FIX22 还原 all0[0].parent（王尔德模式=原版首卡容器）≠ b2 容器 → 530 高空容器盖住分类=整体错位
                        //   显式遍历 b2 卡父容器强制还原 367/180（不依赖 FIX11 recorded 状态）
                        try
                        {
                            foreach (var el in ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true))
                            {
                                if (el == null || el.gameObject == null || el.transform == null || el.transform.parent == null) continue;
                                if (el.id == null || !el.id.StartsWith(PREFIX)) continue;
                                var brt = el.transform.parent.GetComponent<RectTransform>();
                                if (brt != null)
                                {
                                    // FIX37（Q5 实锤终局）：写固定常量——王尔德 prefab 静态 (1000,367)/1250×180
                                    //   （日志实测王尔德打开各容器 x=1000 系正常；530/192 全是 mod 残留污染）
                                    bool bchanged = false;
                                    if (Math.Abs(brt.sizeDelta.y - 180f) > 0.5f || Math.Abs(brt.sizeDelta.x - 1250f) > 0.5f) { brt.sizeDelta = new Vector2(1250f, 180f); bchanged = true; }
                                    if (Math.Abs(brt.anchoredPosition.y - 367f) > 0.5f || Math.Abs(brt.anchoredPosition.x - 1000f) > 0.5f) { brt.anchoredPosition = new Vector2(1000f, 367f); bchanged = true; }
                                    if (bchanged) Core.LogMsg($"[B2R] FIX35 王尔德还原b2容器[{brt.name}] → size=({brt.sizeDelta.x:F0},{brt.sizeDelta.y:F0}) anchored=({brt.anchoredPosition.x:F0},{brt.anchoredPosition.y:F0})（FIX37 固定常量）");
                                }
                                break; // 只需第一个 b2 卡的父容器
                            }
                        }
                        catch (Exception ex) { Core.LogMsg("[B2R] FIX35 还原b2容器异常: " + ex.Message); }
                    }
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX22 王尔德容器回正异常: " + ex.Message); }
            // FIX39 诊断（2026-10-17 FIX38 实测残留）：王尔德"恩惠区空/线人区多卡"——打印每张显示中原版卡↔容器映射，
            //   一次实测确定恩惠/线人卡在哪个容器、容器 anchored 是否错位
            try
            {
                if (ui != null && ui.panel != null && ui.panel.gameObject != null)
                {
                    var nels = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
                    foreach (var nel in nels)
                    {
                        if (nel == null || nel.gameObject == null || !nel.gameObject.activeInHierarchy) continue;
                        if (nel.id == null || nel.id.StartsWith(PREFIX)) continue; // 只打显示中原版卡
                        var nrt = nel.gameObject.GetComponent<RectTransform>();
                        var prt = nel.transform.parent != null ? nel.transform.parent.GetComponent<RectTransform>() : null;
                        if (nrt != null && prt != null)
                            Core.LogMsg($"[B2R] FIX39 王尔德原版卡[{nel.id}] 父={prt.name} 卡local=({nrt.localPosition.x:F0},{nrt.localPosition.y:F0}) 容器anchored=({prt.anchoredPosition.x:F0},{prt.anchoredPosition.y:F0}) sibling={prt.GetSiblingIndex()}");
                    }
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX39 卡映射诊断异常: " + ex.Message); }
            // FIX27 诊断（2026-10-17 实测第二轮）：王尔德对照——打印 Screen 尺寸 + 原版首卡/容器 worldPos，
            //   与蛙哥模式 FIX25/26 数据对比，判定蛙哥卡不可见是"位置出屏"还是"遮挡/渲染"
            try
            {
                Core.LogMsg($"[B2R] FIX27 王尔德对照: Screen=({UnityEngine.Screen.width}x{UnityEngine.Screen.height})");
                var all0 = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
                if (all0 != null && all0.Length > 0 && all0[0] != null && all0[0].gameObject != null)
                {
                    var or = all0[0].GetComponent<RectTransform>();
                    var pr = all0[0].gameObject.transform.parent != null ? all0[0].gameObject.transform.parent.GetComponent<RectTransform>() : null;
                    if (or != null) Core.LogMsg($"[B2R] FIX27 王尔德原版首卡[{all0[0].id}] local=({or.localPosition.x:F0},{or.localPosition.y:F0}) worldPos=({or.position.x:F1},{or.position.y:F1}) active={all0[0].gameObject.activeSelf}");
                    if (pr != null) Core.LogMsg($"[B2R] FIX27 王尔德容器 anchored=({pr.anchoredPosition.x:F0},{pr.anchoredPosition.y:F0}) worldPos=({pr.position.x:F1},{pr.position.y:F1}) size=({pr.sizeDelta.x:F0},{pr.sizeDelta.y:F0})");
                }
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX27 王尔德对照异常: " + ex.Message); }
            // FIX34 诊断（2026-10-17 实测）：王尔德污染="卡片/板块整体错位下移"（截图 fc1b103a 推荐卡 y=180/供货商 630 vs 正常 45/585）
            //   容器 Panel1(3) anchored=367 正常（FIX27 已证）→ 下移源在面板根 or 各板块容器 → 打印定位
            try
            {
                if (ui.panel != null && ui.panel.gameObject != null)
                {
                    var rootRt = ui.panel.gameObject.GetComponent<RectTransform>();
                    if (rootRt != null)
                    {
                        var rp = rootRt.parent;
                        Core.LogMsg($"[B2R] FIX34 面板根 local=({rootRt.localPosition.x:F0},{rootRt.localPosition.y:F0}) anchored=({rootRt.anchoredPosition.x:F0},{rootRt.anchoredPosition.y:F0}) parent={(rp != null ? rp.name : "无")} pivot=({rootRt.pivot.x:F1},{rootRt.pivot.y:F1})");
                        var allRts = ui.panel.gameObject.GetComponentsInChildren<RectTransform>(true);
                        foreach (var pr in allRts)
                        {
                            if (pr == null || pr.gameObject == null) continue;
                            string pn = pr.name;
                            if (pn != null && pn.StartsWith("Panel1"))
                            {
                                var ppr = pr.parent != null ? pr.parent.GetComponent<RectTransform>() : null;
                                Core.LogMsg($"[B2R] FIX34 容器[{pn}] anchored=({pr.anchoredPosition.x:F0},{pr.anchoredPosition.y:F0}) local=({pr.localPosition.x:F0},{pr.localPosition.y:F0}) size=({pr.sizeDelta.x:F0},{pr.sizeDelta.y:F0}) sibling={pr.GetSiblingIndex()} 父local=({(ppr != null ? ppr.localPosition.x : -1):F0},{(ppr != null ? ppr.localPosition.y : -1):F0})");
                                // FIX36：记录各分类容器基准锚点（仅王尔德首次记录——"越开越少"=各分类容器每次被原版漂移出屏，FIX33 只守护推荐容器）
                                if (!_panelAnchorRecorded)
                                {
                                    _panelAnchorPersist[pn] = pr.anchoredPosition;
                                }
                            }
                        }
                    }
                }
                if (_panelAnchorPersist.Count > 0) _panelAnchorRecorded = true; // FIX36：已记录（下次只守护不覆盖）
            }
            catch (Exception ex) { Core.LogMsg("[B2R] FIX34 面板根诊断异常: " + ex.Message); }
        }
        Core.LogMsg("[B2R] 面板模式: " + (_wageMode ? "蛙哥（22条）" : "王尔德（原样）"));
    }

    // FIX4：原版分组标签（推荐/恩惠/线人/供货商/服务/枪械匠）= prefab 静态 TMP（截图实锤仍在显示），
    //   不在 NetworkElement 里→分流管不到。蛙哥模式：第一个（推荐）文本改「蛙哥服务」，其余 5 个隐藏；
    //   王尔德模式：恢复原文/原 active（防污染）。首开建立引用+原文快照，后续复用；引用失效则重搜。
    private sealed class GroupLabelState
    {
        internal Il2CppTMPro.TextMeshProUGUI Tmp;
        internal string OrigText;
        internal bool OrigActive;
    }
    private static GroupLabelState[] _groupLabels = null;
    private static void ApplyWageLabels(WildUIManager ui, bool wageMode)
    {
        try
        {
            if (ui == null || ui.panel == null) return;
            if (_groupLabels == null)
            {
                var comps = ui.panel.gameObject.GetComponentsInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
                var list = new System.Collections.Generic.List<GroupLabelState>();
                foreach (var tmp in comps)
                {
                    if (tmp == null) continue;
                    string t = (tmp.text ?? "").Trim();
                    if (t == "推荐" || t == "恩惠" || t == "线人" || t == "供货商" || t == "服务" || t == "枪械匠"
                        || t == "Recommendations" || t == "Favors" || t == "Informants" || t == "Suppliers" || t == "Services" || t == "Gunsmiths")
                    {
                        list.Add(new GroupLabelState { Tmp = tmp, OrigText = t, OrigActive = tmp.gameObject.activeSelf });
                    }
                }
                _groupLabels = list.ToArray();
                Core.LogMsg("[B2R] 分组标签定位: " + _groupLabels.Length + " 个（推荐/恩惠/线人/供货商/服务/枪械匠）");
            }
            bool first = true;
            foreach (var s in _groupLabels)
            {
                if (s == null || s.Tmp == null) continue;
                bool alive;
                try { alive = s.Tmp.gameObject != null; } catch { alive = false; }
                if (!alive) { _groupLabels = null; return; } // FIX18：面板重建 → 清缓存，下次 OpenUI 重搜定位分组标签
                if (wageMode)
                {
                    if (first)
                    {
                        s.Tmp.text = LangHelper.T("蛙哥服务", "Wage Brother Services");
                        first = false;
                    }
                    else s.Tmp.gameObject.SetActive(false);
                }
                else
                {
                    s.Tmp.text = s.OrigText;
                    s.Tmp.gameObject.SetActive(s.OrigActive);
                }
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 分组标签处理异常: " + ex.Message); }
    }

    // FIX9：原版面板=多分类放置区（推荐/恩惠/线人/供货商/服务/枪械匠各一个容器/横条）。
    //   b2 22 张全克隆进第一个分类容器（模板父级=Panel1(3)）；其余分类容器=纯原版元素（蛙哥模式已隐藏卡片，
    //   但容器背景/空白横条仍显示 → 玩家诉求「王尔德不同的分类有不同的放置位置，我只想有一个放置位置」）。
    //   蛙哥模式：隐藏不含 b2 卡片的容器（空横条）；王尔德模式：恢复原 active（防污染）。
    private static readonly System.Collections.Generic.HashSet<UnityEngine.Transform> _b2Containers = new System.Collections.Generic.HashSet<UnityEngine.Transform>();
    private static readonly System.Collections.Generic.Dictionary<UnityEngine.Transform, bool> _emptyContainerOrig = new System.Collections.Generic.Dictionary<UnityEngine.Transform, bool>();
    // FIX11：容器级隐藏默认关闭（FIX9 隐藏 6 容器后出现"进蛙哥服务自动关闭"+王尔德面板被污染）。
    //   玩家要的"只保留一个放置位置"下一轮改用安全方式（定位分类标签横条 Image，不碰容器）。置 true 需先实测稳定。
    private const bool _b2HideContainers = false;
    // FIX31：恢复蛙哥模式隐藏的挡路兄弟容器（王尔德打开/面板关闭时调用——王尔德需要各分类容器显示原版卡）
    private static void RestoreHiddenPanelObjs()
    {
        try
        {
            foreach (var go in _hiddenPanelObjs)
            {
                if (go != null) go.SetActive(true);
            }
            _hiddenPanelObjs.Clear();
        }
        catch (Exception ex) { Core.LogMsg("[B2R] FIX31 恢复容器异常: " + ex.Message); }
    }

    private static void HideEmptyB2Containers(WildUIManager ui, bool wageMode)
    {
        try
        {
            if (!_b2HideContainers) return; // FIX11：容器级隐藏回退
            if (ui == null || ui.panel == null) return;
            var all = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
            if (all == null || all.Length == 0) return;
            var containsB2 = new System.Collections.Generic.HashSet<UnityEngine.Transform>();
            var containers = new System.Collections.Generic.HashSet<UnityEngine.Transform>();
            foreach (var el in all)
            {
                if (el == null || el.transform == null || el.transform.parent == null) continue;
                var p = el.transform.parent;
                containers.Add(p);
                if (el.id != null && el.id.StartsWith(PREFIX)) containsB2.Add(p);
            }
            if (wageMode)
            {
                int hidden = 0;
                foreach (var c in containers)
                {
                    if (containsB2.Contains(c)) continue; // 保留含 b2 的容器=唯一放置区
                    if (!_emptyContainerOrig.ContainsKey(c)) _emptyContainerOrig[c] = c.gameObject.activeSelf;
                    if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); hidden++; }
                }
                Core.LogMsg($"[B2R] FIX9 隐藏空分类容器: 容器总数={containers.Count} 含b2={containsB2.Count} 隐藏={hidden}");
            }
            else
            {
                RestoreHiddenContainers(); // FIX11：幂等恢复（不 Clear——清单常驻，王尔德打开时也能恢复上次被隐藏的容器）
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] FIX9 容器隐藏异常: " + ex.Message); }
    }

    // 顶部页面标题：无代码挂点（拆包 C1 实锤——WildUIManager 方法表全无 Find，OpenUI/CloseUI 不重置该 TMP）
    // 10-15 好感可视化：顶部横幅文本 = 标题 + 好感状态（门槛 SVC_AFFECTION_GATE=40）
    private static string BuildAffectionBanner()
    {
        string head = LangHelper.T("蛙哥人脉网络", "Wage Brother Network");
        try
        {
            int gate = SVC_AFFECTION_GATE;
            int cur = (int)WageGirlSystem.GetAffectionExact();
            if (cur >= gate) return head + "｜" + LangHelper.T("好感 " + cur + "/" + gate + " 已达标", "Affection " + cur + "/" + gate + " OK");
            return head + "｜" + LangHelper.T("好感 " + cur + "/" + gate + " 还差 " + (gate - cur), "Affection " + cur + "/" + gate + " (+" + (gate - cur) + ")");
        }
        catch { return head; }
    }
    private static UnityEngine.Color BannerColor()
    {
        try { return (int)WageGirlSystem.GetAffectionExact() >= SVC_AFFECTION_GATE ? new UnityEngine.Color(0.3f, 0.72f, 0.38f) : new UnityEngine.Color(0.85f, 0.28f, 0.28f); }
        catch { return UnityEngine.Color.white; }
    }

    // 运行时遍历 panel 子树找最靠上的非详情/非元素 TMP → SetText；改一次存活，缓存引用
    private static Il2CppTMPro.TextMeshProUGUI _headerTmp = null;
    private static bool _headerSearched = false;
    private static void ApplyWageHeader(WildUIManager ui)
    {
        try
        {
            if (_headerSearched && _headerTmp == null) return; // 已搜过没找到，不再搜
            if (_headerTmp != null)
            {
                // FIX18：面板重建后缓存引用指向已销毁 TMP（托管包装非 null 但 native 已释放）→
                //   gameObject 判空（PostfixUpdate 的 _favorTmp 同款判空在该 interop 环境实测有效：
                //   读档后恩惠行仍被正确替换=已重搜）→ 失效则重置缓存，落到下方重搜分支
                bool alive;
                try { alive = _headerTmp.gameObject != null; } catch { alive = false; }
                if (alive)
                {
                    _headerTmp.text = BuildAffectionBanner();
                    _headerTmp.color = BannerColor();
                    return;
                }
                _headerTmp = null;
                _headerSearched = false;
            }
            if (ui.panel == null) return;
            var comps = ui.panel.gameObject.GetComponentsInChildren<Component>(true); // Patches.World.cs:143 同款
            Il2CppTMPro.TextMeshProUGUI fallback = null;
            foreach (var c in comps)
            {
                if (c == null) continue;
                var tmp = c.TryCast<Il2CppTMPro.TextMeshProUGUI>();
                if (tmp == null) continue;
                // 排除右侧详情 6 字段（D6 实锤：title/subtitle/description/cooldown/cost/alreadyMessageTMP）
                if (ReferenceEquals(tmp, ui.title) || ReferenceEquals(tmp, ui.subtitle) || ReferenceEquals(tmp, ui.description)
                    || ReferenceEquals(tmp, ui.cooldown) || ReferenceEquals(tmp, ui.cost) || ReferenceEquals(tmp, ui.alreadyMessageTMP)) continue;
                // 排除网络元素内部的 TMP（22 克隆 + 原版 31 各自详情）
                bool inElement = false;
                var elements = ui.panel.gameObject.GetComponentsInChildren<NetworkElement>(true);
                foreach (var el in elements)
                {
                    if (el == null || el.gameObject == null) continue;
                    if (tmp.gameObject.transform.IsChildOf(el.gameObject.transform)) { inElement = true; break; }
                }
                if (inElement) continue;
                // FIX3：优先文本匹配原版顶部标题（「王尔德的人脉网络」/ 含 "Network"）——y 最大法会选到隐藏/无关 TMP
                string t = tmp.text ?? "";
                if (t.Contains("人脉网络") || t.Contains("Network"))
                {
                    _headerTmp = tmp;
                    break;
                }
                // 兜底：y 最大（屏幕位置最靠上）
                if (fallback == null || tmp.transform.position.y > fallback.transform.position.y)
                    fallback = tmp;
            }
            if (_headerTmp == null) _headerTmp = fallback;
            _headerSearched = true;
            if (_headerTmp != null)
            {
                _headerTmp.text = BuildAffectionBanner();
                _headerTmp.color = BannerColor();
                Core.LogMsg("[B2R] 顶部标题已替换为「" + (_headerTmp.text ?? "") + "」");
            }
            else
            {
                _headerSearched = false; // 未找到→下次 OpenUI 重搜（panel 初始化时序差异）
                Core.LogMsg("[B2R] 未找到顶部标题 TMP（面板结构不符预期，仅右侧 title 槽生效）");
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 顶部标题替换异常: " + ex.Message); }
    }

    public static void PostfixCloseUI()
    {
        _wageGeoGuardStart = -1f; _wageGeoGuardRect = null; // FIX32：立即终止蛙哥容器守护（防与还原打架，容器残留 530/192 污染王尔德）
        _wildGeoGuardStart = -1f; _wildGeoGuardRect = null; // FIX33：立即终止王尔德容器守护
        _wageMode = false; // 关闭面板复位分流标记（防下次王尔德路径被污染）
        RestoreContainerGeometry(); // FIX11：还原 FIX8/FIX10 容器几何（扩高+下移）——王尔德/关闭时必还原，否则污染原版布局
        RestoreHiddenContainers();  // FIX11：恢复 FIX9 隐藏的容器（清单常驻不 Clear，王尔德打开时幂等恢复）
        RestoreHiddenPanelObjs();   // FIX31：恢复蛙哥模式隐藏的挡路兄弟容器（防下次王尔德缺容器）
    }

    // ================= FIX11：容器几何/显隐状态还原（根治"干扰原版"） =================
    // FIX8 扩容器高度 + FIX10 下移补偿都是运行时几何改动，FIX9 容器隐藏也是运行时显隐改动——
    // 原实现王尔德模式/CloseUI 不还原 → 王尔德面板被污染（截图 0b460ea3：左侧详情空白、卡片错位、枪械匠截断）
    private static bool _containerGeoRecorded = false;
    private static UnityEngine.Vector2 _containerGeoOrig = UnityEngine.Vector2.zero;  // 原始 sizeDelta
    private static UnityEngine.Vector2 _containerGeoAnchor = UnityEngine.Vector2.zero; // 原始 anchoredPosition
    private static UnityEngine.RectTransform _geoContainer = null;
    private static bool _geoAnchorPersisted = false; // FIX20：容器基准锚点已持久记录（跨开关循环，读档重建时重置）
    private static UnityEngine.Vector2 _geoAnchorPersist = UnityEngine.Vector2.zero; // FIX20：首次容器基准锚点（防 CloseUI 动画漂移累积）
    private static UnityEngine.RectTransform _restoreCheckRect = null; // FIX24：还原后复核对象
    private static float _restoreCheckTime = -1f; // FIX24：复核时间点（Time.time + 0.5s）
    private static UnityEngine.RectTransform _cardCheckRect = null; // FIX26b：卡片延迟复核对象
    private static float _cardCheckTime = -1f; // FIX26b：卡片复核时间点
    private static UnityEngine.RectTransform _geoTraceRect = null; // FIX29：容器几何时间追踪对象
    private static float _geoTraceStart = -1f; // FIX29：追踪起点
    private static int _geoTraceBucket = -1; // FIX29：上次采样桶
    private static UnityEngine.RectTransform _wageGeoGuardRect = null; // FIX30：蛙哥容器守护对象
    private static float _wageGeoGuardStart = -1f; // FIX30：守护起点
    private static float _wageGeoGuardNeedH = 0f; // FIX30：守护目标高度
    private static float _wageGeoGuardBaseAnchor = 367f; // FIX30：守护基准锚点
    private static int _wageGeoGuardRestoreCount = 0; // FIX30：守护期间还原次数
    private static readonly System.Collections.Generic.List<UnityEngine.GameObject> _hiddenPanelObjs = new System.Collections.Generic.List<UnityEngine.GameObject>(); // FIX31：蛙哥模式隐藏的挡路兄弟容器
    private static UnityEngine.RectTransform _wildGeoGuardRect = null; // FIX33：王尔德容器守护对象
    private static float _wildGeoGuardStart = -1f; // FIX33：守护起点
    private static int _wildGeoGuardRestoreCount = 0; // FIX33：守护期间拉回次数
    // FIX40（2026-10-17 FIX39 日志实锤）：王尔德各分类容器 prefab 静态布局表（原版真值）
    //   Panel1 (3)=367 推荐 | Panel1 (1)=217 恩惠 | Panel1 (2)=67 线人
    //   Panel1=-83 供货商 | Panel1 (4)=-231 服务 | Panel1 (5)=-388 枪械匠
    //   注意：FIX36 旧逻辑把残留 192 一律拉 367 是错的（只有推荐容器基准 367；恩惠容器 217 被拉到 367=恩惠区消失）
    private static bool TryGetPanelPrefabY(string name, out float y)
    {
        switch (name)
        {
            case "Panel1 (3)": y = 367f; return true;  // 推荐
            case "Panel1 (1)": y = 217f; return true;  // 恩惠
            case "Panel1 (2)": y = 67f; return true;   // 线人
            case "Panel1": y = -83f; return true;      // 供货商
            case "Panel1 (4)": y = -231f; return true; // 服务
            case "Panel1 (5)": y = -388f; return true; // 枪械匠
            default: y = float.NaN; return false;
        }
    }
    private static readonly System.Collections.Generic.Dictionary<string, UnityEngine.Vector2> _panelAnchorPersist = new System.Collections.Generic.Dictionary<string, UnityEngine.Vector2>(); // FIX36：各 Panel1 分类容器基准锚点（王尔德首次打开记录）
    private static bool _panelAnchorRecorded = false; // FIX36：基准锚点已记录
    private static int _panelGuardRestoreCount = 0; // FIX36：全容器守护拉回次数
    private static void RestoreContainerGeometry()
    {
        try
        {
            if (_geoContainer != null && _geoContainer.gameObject != null && _containerGeoRecorded)
            {
                // FIX37（Q5 实锤终局）：还原永远写固定常量（王尔德 prefab 静态 1250×180、anchored=(1000,367)），
                //   绝不读 _containerGeoOrig/_geoAnchorPersist（漂移后写回的是漂移值——漂移链根因）。
                _geoContainer.sizeDelta = new UnityEngine.Vector2(1250f, 180f);
                _geoContainer.anchoredPosition = new UnityEngine.Vector2(1000f, 367f);
                Core.LogMsg("[B2R] FIX11 容器几何还原: sizeDelta=(1250,180) anchored=(1000,367)（FIX37 固定常量，不读漂移记录值）");
                // FIX24：登记还原后复核（0.5s 后 PostfixUpdate 检查是否被父级 Layout 重排/动画移走并强制拉回基准）
                _restoreCheckRect = _geoContainer;
                _restoreCheckTime = Time.time + 0.5f;
            }
            _containerGeoRecorded = false;
            _geoContainer = null;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 容器几何还原异常: " + ex.Message); }
    }
    private static void RestoreHiddenContainers()
    {
        try
        {
            int restored = 0;
            foreach (var kv in _emptyContainerOrig)
            {
                if (kv.Key != null && kv.Key.gameObject != null && !kv.Key.gameObject.activeSelf && kv.Value)
                { kv.Key.gameObject.SetActive(true); restored++; }
            }
            if (restored > 0) Core.LogMsg("[B2R] FIX11 恢复隐藏容器: " + restored);
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 容器恢复异常: " + ex.Message); }
    }

    // ================= FIX6：恩惠行蛙哥化 + tooltip 取证 =================
    // 拆包实锤（B2-R2-H3-FIX5 拆包）：恩惠行=WildUIManager.Update 每帧 SetText（GetLocalizedUI("network_ui_wild_favor")+info sprite，
    //   声望[0x14] 数值同帧刷新，:392-410）→ 运行时改名瞬间被刷回 → 必须 Patch Update 的 Postfix 每帧替换。
    //   仅 _wageMode=true（蛙哥面板打开期间）替换"王尔德"→"蛙哥"；王尔德模式直接 return（零开销、不污染）。
    //   TMP 定位：文本匹配缓存（"王尔德的恩惠"前缀，与 ApplyWageLabels 同模式；英文兜底 "Wilde"）。
    private static Il2CppTMPro.TextMeshProUGUI _favorTmp = null;
    public static void PostfixUpdate(WildUIManager __instance)
    {
        try
        {
            // FIX24：容器还原后延迟复核（0.5s）——若被父级 Layout 重排/动画移走，强制拉回持久基准，断漂移循环
            //   触发链：FIX11 还原 530→180 高度变化 → 父级 Layout 重排把容器移到非基准位置（下次打开时已是漂移值）
            if (_restoreCheckTime > 0f && Time.time >= _restoreCheckTime)
            {
                _restoreCheckTime = -1f;
                var rr = _restoreCheckRect;
                _restoreCheckRect = null;
                if (rr != null && rr.gameObject != null)
                {
                    Vector2 curAnchor = rr.anchoredPosition;
                    // FIX37（Q5 实锤终局）：复核基准=固定常量 (1000,367)/180（原版 prefab 静态值），不读 _geoAnchorPersist（漂移后写回的是漂移值）
                    if (Math.Abs(curAnchor.y - 367f) > 0.5f || Math.Abs(curAnchor.x - 1000f) > 0.5f || Math.Abs(rr.sizeDelta.y - 180f) > 0.5f)
                    {
                        rr.anchoredPosition = new Vector2(1000f, 367f);
                        rr.sizeDelta = new Vector2(1250f, 180f);
                        Core.LogMsg($"[B2R] FIX24 还原后复核: 容器被移动 ({curAnchor.x:F0},{curAnchor.y:F0}) → 强制回固定常量 (1000,367)/180");
                    }
                    else
                    {
                        Core.LogMsg($"[B2R] FIX24 还原后复核: 容器稳定 anchored=({curAnchor.x:F0},{curAnchor.y:F0}) 无漂移");
                    }
                }
            }
            // FIX29：容器几何时间追踪（每 0.1s 采样至 1s，定位"扩展后被还原"发生的时间点——判断 mod 同步 or 原版延迟逻辑）
            if (_geoTraceStart > 0f)
            {
                float t = Time.time - _geoTraceStart;
                int bucket = (int)(t * 10f);
                if (bucket != _geoTraceBucket)
                {
                    _geoTraceBucket = bucket;
                    if (_geoTraceRect != null && _geoTraceRect.gameObject != null)
                        Core.LogMsg($"[B2R] FIX29 追踪 t={t:F1}s: sizeDelta=({_geoTraceRect.sizeDelta.x:F0},{_geoTraceRect.sizeDelta.y:F0}) anchored=({_geoTraceRect.anchoredPosition.x:F0},{_geoTraceRect.anchoredPosition.y:F0})");
                }
                if (bucket > 10) { _geoTraceStart = -1f; _geoTraceRect = null; }
            }
            // FIX30/FIX32：蛙哥容器守护（蛙哥模式期间持续，被原版逻辑还原高度/撤销下移补偿立即重新扩展+置顶）
            //   FIX32：①同时守护 anchored（原版会撤销下移补偿 192→367，卡片贴屏幕底 1039）②守护持续到蛙哥模式结束
            //   （原 2s 窗口在关闭后还活着 → 与 FIX11 还原打架 → 关闭后容器残留 530/192 → 王尔德被污染）
            if (_wageGeoGuardStart > 0f && _wageGeoGuardRect != null && _wageGeoGuardRect.gameObject != null)
            {
                if (_wageMode)
                {
                    float wantAnchor = _wageGeoGuardBaseAnchor - 175f;
                    if (_wageGeoGuardRect.sizeDelta.y < _wageGeoGuardNeedH - 1f
                        || Math.Abs(_wageGeoGuardRect.anchoredPosition.y - wantAnchor) > 1f)
                    {
                        _wageGeoGuardRestoreCount++;
                        _wageGeoGuardRect.sizeDelta = new Vector2(_wageGeoGuardRect.sizeDelta.x, _wageGeoGuardNeedH);
                        _wageGeoGuardRect.anchoredPosition = new Vector2(_wageGeoGuardRect.anchoredPosition.x, wantAnchor);
                        _wageGeoGuardRect.SetAsLastSibling();
                        Core.LogMsg($"[B2R] FIX32 容器被改动→强制拉回 #{_wageGeoGuardRestoreCount} size=({_wageGeoGuardRect.sizeDelta.x:F0},{_wageGeoGuardRect.sizeDelta.y:F0}) anchored=({_wageGeoGuardRect.anchoredPosition.x:F0},{_wageGeoGuardRect.anchoredPosition.y:F0})");
                    }
                }
                else
                {
                    // 蛙哥模式结束（切王尔德/关闭）→ 终止守护，交还原逻辑接管
                    Core.LogMsg($"[B2R] FIX32 守护终止（模式切换）期间共拉回 {_wageGeoGuardRestoreCount} 次");
                    _wageGeoGuardStart = -1f;
                    _wageGeoGuardRect = null;
                }
            }
            // FIX33：王尔德容器守护（王尔德模式期间持续，原版逻辑会持续漂移容器→面板内容下移错位=污染）
            //   FIX22 只在打开瞬间拉回一次，之后原版再漂移无人管 → 截图实锤推荐卡 y=45→180、供货商 585→630 整体下移
            if (_wildGeoGuardStart > 0f && _wildGeoGuardRect != null && _wildGeoGuardRect.gameObject != null)
            {
                if (!_wageMode)
                {
                    // FIX37（Q5 实锤终局）：守护基准=固定常量 (1000,367)/180（王尔德 prefab 静态值），不读当前值
                    if (Math.Abs(_wildGeoGuardRect.anchoredPosition.y - 367f) > 1f
                        || Math.Abs(_wildGeoGuardRect.anchoredPosition.x - 1000f) > 1f
                        || Math.Abs(_wildGeoGuardRect.sizeDelta.y - 180f) > 1f
                        || Math.Abs(_wildGeoGuardRect.sizeDelta.x - 1250f) > 1f)
                    {
                        _wildGeoGuardRestoreCount++;
                        _wildGeoGuardRect.anchoredPosition = new Vector2(1000f, 367f);
                        _wildGeoGuardRect.sizeDelta = new Vector2(1250f, 180f);
                        Core.LogMsg($"[B2R] FIX33 王尔德容器漂移→拉回 #{_wildGeoGuardRestoreCount} anchored=({_wildGeoGuardRect.anchoredPosition.x:F0},{_wildGeoGuardRect.anchoredPosition.y:F0}) size=({_wildGeoGuardRect.sizeDelta.x:F0},{_wildGeoGuardRect.sizeDelta.y:F0})");
                    }
                }
                else
                {
                    _wildGeoGuardStart = -1f;
                    _wildGeoGuardRect = null;
                }
            }
            // FIX36：全分类容器高度守护（王尔德模式每帧，清残留扩展态——Q5 实锤原版零几何写，各分类 y=prefab 静态不动，
            //   FIX40：改用 prefab 布局表还原（FIX39 日志实锤真值：推荐367/恩惠217/线人67/供货商-83/服务-231/枪械匠-388），
            //   不再一律拉 367——旧逻辑把恩惠容器 217 误拉到 367=恩惠区消失）
            if (!_wageMode && __instance != null && __instance.panel != null && __instance.panel.gameObject != null)
            {
                try
                {
                    var allRts = __instance.panel.gameObject.GetComponentsInChildren<RectTransform>(true);
                    foreach (var pr in allRts)
                    {
                        if (pr == null || pr.gameObject == null) continue;
                        string pn = pr.name;
                        if (pn == null || !pn.StartsWith("Panel1")) continue;
                        float wantY;
                        if (!TryGetPanelPrefabY(pn, out wantY)) continue; // 不在表内（如 Panel1 名称变体）不动
                        bool bchanged = false;
                        if (Math.Abs(pr.sizeDelta.y - 180f) > 1f) { pr.sizeDelta = new Vector2(pr.sizeDelta.x, 180f); bchanged = true; }
                        if (Math.Abs(pr.anchoredPosition.y - wantY) > 1f) { pr.anchoredPosition = new Vector2(pr.anchoredPosition.x, wantY); bchanged = true; }
                        if (bchanged)
                        {
                            _panelGuardRestoreCount++;
                            Core.LogMsg($"[B2R] FIX36 分类容器[{pn}]→prefab基准 y={wantY:F0} #{_panelGuardRestoreCount} anchored=({pr.anchoredPosition.x:F0},{pr.anchoredPosition.y:F0}) size=({pr.sizeDelta.x:F0},{pr.sizeDelta.y:F0})");
                        }
                    }
                }
                catch (Exception ex) { Core.LogMsg("[B2R] FIX36 全容器守护异常: " + ex.Message); }
            }
            // FIX26b：卡片延迟复核（0.5s）——确认卡片是否被每帧 UpdateElement/布局逻辑移动位置
            if (_cardCheckTime > 0f && Time.time >= _cardCheckTime)
            {
                _cardCheckTime = -1f;
                var cr = _cardCheckRect;
                _cardCheckRect = null;
                if (cr != null && cr.gameObject != null)
                    Core.LogMsg($"[B2R] FIX26b 卡片延迟复核: worldPos=({cr.position.x:F1},{cr.position.y:F1}) local=({cr.localPosition.x:F0},{cr.localPosition.y:F0}) activeInHierarchy={cr.gameObject.activeInHierarchy}");
            }
            // FIX16：服务发放延迟重试（帧轮询，柜台未就绪时补上桌）——不依赖 _wageMode（王尔德路径也补）
            OnUpdateServiceItemRefill();
            if (!_wageMode) return; // 王尔德路径零开销
            if (__instance == null || __instance.panel == null) return;
            if (_favorTmp == null || _favorTmp.gameObject == null)
            {
                _favorTmp = null;
                var comps = __instance.panel.gameObject.GetComponentsInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
                foreach (var tmp in comps)
                {
                    if (tmp == null) continue;
                    string t = (tmp.text ?? "");
                    if (t.StartsWith("王尔德的恩惠") || t.StartsWith("Wilde's Favor") || t.Contains("王尔德的恩惠"))
                    {
                        _favorTmp = tmp;
                        break;
                    }
                }
                if (_favorTmp == null) return; // 面板未就绪/结构不符，下帧再找
            }
            string cur = _favorTmp.text;
            if (!string.IsNullOrEmpty(cur) && cur.Contains("王尔德"))
                _favorTmp.text = cur.Replace("王尔德", "蛙哥");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 恩惠行替换异常: " + ex.Message); }
    }

    // FIX6：tooltip 取证——拆包未定 CustomUITooltipTrigger[0x18] 存的是本地化 key 还是纯文本（决定 Patch 点）。
    //   Patch CustomUIManager.ShowTooltip 打印传入文本，玩家 hover 恩惠叹号时日志给出 [0x18] 内容 → 下轮定改法。
    //   签名按拆包调用链（ShowTooltip(文本, this)）猜 string+CustomUITooltipTrigger；挂不上 ManualPatcher 记失败，不炸。
    public static void PostfixShowTooltip(string text)
    {
        try { Core.LogMsg("[B2R] ShowTooltip 文本: [" + (text ?? "<null>") + "]"); }
        catch (Exception ex) { Core.LogMsg("[B2R] ShowTooltip 日志异常: " + ex.Message); }
    }

    // FIX13：tooltip 蛙哥化（FIX6b 拆包实锤：恩惠 tooltip 文本必经 CustomUIManager.ShowTooltip 唯一重载（实例方法，text 参数））
    //   玩家实测（14773b89）"叹号内容还是王尔德"→ Prefix ref text 替换（挂点A=改 text 参数，全局唯一入口）
    //   只改恩惠 tooltip：_wageMode && 文本含"王尔德"→ 蛙哥文案；不影响其他 CustomUI tooltip（不含"王尔德"的放行）
    public static bool PrefixShowTooltip(ref string text, CustomUITooltipTrigger owner)
    {
        try
        {
            if (_wageMode && text != null && text.Contains("王尔德"))
            {
                text = "提升在下层区的声望，或使用通过蛙哥解锁的夜间服务，都会增加蛙哥的恩惠。该恩惠会优先用于蛙哥的升级。";
                Core.LogMsg("[B2R] tooltip 已蛙哥化");
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] tooltip 替换异常: " + ex.Message); }
        return true;
    }

    // ================= ⑦ 来电 name 行本地化兜底（B2-R2-H2） =================
    // 拆包实锤（B2-R2-H2-D）：通话 name 行=GetLocalizedDisplayName()=GetLocalizedClientName(locID) 查 ClientName 共享表；
    //   蛙哥 locID="" → 表内无 "" 条目 → Unity 回显 "in ClientName" 错误。电话簿+通话 name 行都走此方法 → 修一处全覆盖。
    // 方案 B（最稳）：空 locID → fallback displayName（蛙哥 displayName="蛙哥" 已就绪，零新增数据；其余客户 locID 非空 → 放行原版）
    private static string GetTransformPath(Transform t)
    {
        try
        {
            var parts = new System.Collections.Generic.List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
        catch { return "?"; }
    }
    public static bool PrefixGetLocalizedDisplayName(StorePhoneClient __instance, ref string __result)
    {
        try
        {
            if (__instance == null) return true;
            if (string.IsNullOrEmpty(__instance.locID))
            {
                __result = string.IsNullOrEmpty(__instance.displayName) ? "" : __instance.displayName;
                return false;
            }
        }
        catch (Exception ex) { Core.LogMsg("[B2R] GetLocalizedDisplayName 拦截异常: " + ex.Message); }
        return true;
    }
}
