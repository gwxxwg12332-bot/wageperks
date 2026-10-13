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

    private sealed class WildEntry
    {
        internal string Id;
        internal string PerkId;   // null = 吞噬季特殊条目
        internal bool IsCannibal => PerkId == null;
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
                u.isRepeatable = e.IsCannibal;   // 吞噬季：已购后再次点击 = 切换模式
                u.cooldownCurrent = 0;
                u.cooldownDuration = 0;
                u.prerequisite = null;
                u.lockInDemo = false;
                u.state = 0;
                __result.Add(e.Id, u);
            }
            RegisterUnlockActions();
            Core.LogMsg("[B2R] networkUpgrade(0x150) 注入 " + Entries.Length + " 条（21 特性 + 吞噬季）");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] 注入异常: " + ex.Message); }
    }

    // 读档兜底：ES3 反序列化 0x150 可能重建字典，缺 b2_ 条目时补（幂等）
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
                u.isRepeatable = e.IsCannibal;
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
        if (e.IsCannibal)
        {
            return B2ServiceStore.IsCannibalOwned ? 0 : CANNIBAL_PRICE; // 已购再点 = 切换模式（免费）
        }
        var perk = FindPerk(e.PerkId);
        if (perk == null) return 0;
        int lv = B2ServiceStore.GetLevel(e.PerkId);
        if (perk.Cost < 0) // 负面：已消除免费占位，未消除显示消除价
        {
            if (B2ServiceStore.IsEliminated(e.PerkId)) return 0;
            if (e.PerkId == InfamousPerk.PerkId) return 0; // 声名狼藉特殊：分势力恢复（免费开窗）
            if (e.PerkId == ThiefMagnetPerk.PerkId) return 5000; // 招贼体质固定消除价（B2 定价沿用）
            return WageBrother.PriceForCost(perk.Cost);
        }
        if (lv >= 3) return 0; // 满级
        if (lv == 0) return BuyPrice(perk);
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

    public static void PostfixGetLocalizedTitle(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
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
            if (e.IsCannibal) { __result = LangHelper.T("机器模组/电池互食盛宴", "Machines devour modules & batteries"); return; }
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            int c = perk.Cost;
            string type = c < 0 ? LangHelper.T("负面特性", "Negative")
                       : (c == 0 ? LangHelper.T("零价特性", "Free") : LangHelper.T("正面特性", "Positive"));
            if (perk.Cost < 0)
            {
                if (B2ServiceStore.IsEliminated(e.PerkId)) type += LangHelper.T(" · 已消除", " · eliminated");
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
                __result += LangHelper.T("\n\n当前等级：Lv" + lv + (lv >= 3 ? "（已满级）" : "（升级增强效果开发中）"), "");
        }
        catch (Exception ex) { Core.LogMsg("[B2R] Description 异常: " + ex.Message); }
    }

    public static void PostfixGetLocalizedAlreadyBought(NetworkUpgrade __instance, ref string __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsCannibal) return; // 吞噬季已购态由 Description 表达（按钮保持可点 = 切换模式）
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            if (perk.Cost < 0)
            {
                if (B2ServiceStore.IsEliminated(e.PerkId)) __result = LangHelper.T("已消除", "eliminated");
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
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            if (perk.Cost < 0) { __result = B2ServiceStore.IsEliminated(e.PerkId); return; }
            __result = B2ServiceStore.GetLevel(e.PerkId) >= 3;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] IsUnlocked 异常: " + ex.Message); }
    }

    // IsReady：可点前置（未满级/未消除；招贼体质被偷未赎回锁）
    public static void PostfixIsReady(NetworkUpgrade __instance, ref bool __result)
    {
        try
        {
            if (!IsB2(__instance)) return;
            var e = FindEntry(__instance.id);
            if (e == null) return;
            if (e.IsCannibal) return; // 原版默认 true 即可
            var perk = FindPerk(e.PerkId);
            if (perk == null) return;
            if (perk.Cost < 0)
            {
                if (e.PerkId == ThiefMagnetPerk.PerkId && ThiefMagnetSystem.StolenCount() > 0)
                    __result = false; // 招贼体质：被偷未赎回 → 锁（提示走消业障）
                else
                    __result = !B2ServiceStore.IsEliminated(e.PerkId);
                return;
            }
            __result = B2ServiceStore.GetLevel(e.PerkId) < 3;
        }
        catch (Exception ex) { Core.LogMsg("[B2R] IsReady 异常: " + ex.Message); }
    }

    // ================= ③ 购买 / 升级 / 消除执行（原生支付已完成，本侧只做状态+附赠+刷新） =================
    private static void OnUnlock(WildEntry e)
    {
        try
        {
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

            if (perk.Cost < 0) // 负面：消除
            {
                if (B2ServiceStore.IsEliminated(e.PerkId)) return;
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
                B2ServiceStore.Eliminate(e.PerkId);
                Notify(LangHelper.T("已消除负面特性：" + perk.DisplayName, "Negative perk removed: " + perk.DisplayName));
                Core.LogMsg("[B2R] 已消除 " + e.PerkId);
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
            }
            else if (lv == 2)
            {
                B2ServiceStore.SetLevel(e.PerkId, 3);
                Notify(LangHelper.T(perk.DisplayName + " 升级到 Lv3（满级）", perk.DisplayName + " upgraded to Lv3 (MAX)"));
                Core.LogMsg("[B2R] 已升级 " + e.PerkId + " → Lv3");
            }
            RefreshDetail(e.Id);
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OnUnlock 异常: " + ex.Message); }
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
    public static void PostfixOpenUI(WildUIManager __instance)
    {
        try
        {
            EnsureElements(__instance);
            ApplyWageMode(__instance); // B2-R2：蛙哥/王尔德分流（标题+元素显隐）
        }
        catch (Exception ex) { Core.LogMsg("[B2R] OpenUI 克隆异常: " + ex.Message); }
    }

    private static void EnsureElements(WildUIManager ui)
    {
        if (ui == null || ui.networkElements == null) return;
        if (_cloned) return;
        if (ui.networkElements.Count == 0) return;
        var tpl = ui.networkElements[0];
        if (tpl == null || tpl.gameObject == null) return;
        Transform parent = tpl.gameObject.transform.parent;
        if (parent == null) return;

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
                go.SetActive(true);
                ui.networkElements.Add(el);
            }
            catch (Exception ex) { Core.LogMsg("[B2R] 克隆元素失败 " + e.Id + ": " + ex.Message); }
        }
        _cloned = true;
        Core.LogMsg("[B2R] 已克隆 " + Entries.Length + " 个元素到 networkElements");
    }

    // ================= ⑤ 蛙哥电话 1005 =================
    // 注册：电话簿直接可见（state=4 + dialedBefore=true，胡安解锁态蓝本）
    public static void PostfixInitPhoneClientDict(Il2CppSystem.Collections.Generic.Dictionary<long, StorePhoneClient> __result)
    {
        try { EnsurePhoneRegistered(__result); }
        catch (Exception ex) { Core.LogMsg("[B2R] 电话注册异常: " + ex.Message); }
    }

    // 幂等注册（ContainsKey 防重）：新档走 InitPhoneClientDict Postfix；旧档走 OnStoreGameLoaded 兜底
    // 拆包实锤（DB-PHONE-01 C2）：InitPhoneClientDict 全库仅 1 调用=StartNewGame（PlayerStore.txt:2080），
    //   读档不重建 PhoneClientDict（整对象 ES3 随档恢复）→ 旧档缺蛙哥 1005，必须读档后补注册
    internal static void EnsurePhoneRegistered(Il2CppSystem.Collections.Generic.Dictionary<long, StorePhoneClient> dict)
    {
        if (dict == null) return;
        if (dict.ContainsKey(WAGE_PHONE_NUMBER)) return; // 幂等
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
        RegisterWageBrotherDialog(); // B2-R2：同步注册对话工厂
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

    // 拨号拦截：1005 走原版对话链（dialogFuncId="WageBrother" → DialogFuncs）；仅当对话工厂注册失败时回退直开面板
    public static bool PrefixStartPhoneDialog(long currentNumber)
    {
        try
        {
            if (currentNumber != WAGE_PHONE_NUMBER) return true; // 放行其他号码（含王尔德 8376）
            var dict = Il2Cpp.PhoneClientList.DialogFuncs;
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
            if (dict == null) { Core.LogMsg("[B2R] DialogFuncs 为空，对话注册跳过"); return; }
            if (dict.ContainsKey(WAGE_DIALOG_ID)) return; // 幂等
            var sysFunc = new System.Func<Il2Cpp.StorePhoneClient, Il2Cpp.Dialogue>(BuildWageBrotherDialogue);
            dict.Add(WAGE_DIALOG_ID, DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.StorePhoneClient, Il2Cpp.Dialogue>>((System.Delegate)sysFunc));
            Core.LogMsg("[B2R] DialogFuncs[\"WageBrother\"] 已注册（蛙哥电话对话链）");
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
    private static void ApplyWageMode(WildUIManager ui)
    {
        if (ui == null || ui.networkElements == null) return;
        int b2Count = 0, total = ui.networkElements.Count;
        foreach (var el in ui.networkElements)
        {
            if (el == null || el.gameObject == null) continue;
            bool isB2 = el.id != null && el.id.StartsWith(PREFIX);
            if (isB2) b2Count++;
            el.gameObject.SetActive(_wageMode ? isB2 : !isB2);
        }
        Core.LogMsg($"[B2R] 分流: _wageMode={_wageMode} total={total} b2={b2Count}（蛙哥=仅b2可见 / 王尔德=仅原版可见）");
        if (_wageMode)
        {
            if (ui.title != null)
                ui.title.text = LangHelper.T("蛙哥人脉网络", "Wage Brother Network"); // 右侧详情槽（打开瞬间）
            ApplyWageHeader(ui); // 顶部页面标题（prefab 静态 TMP，运行时遍历替换——拆包 B2-R2-H3 C1）
        }
        Core.LogMsg("[B2R] 面板模式: " + (_wageMode ? "蛙哥（22条）" : "王尔德（原样）"));
    }

    // 顶部页面标题：无代码挂点（拆包 C1 实锤——WildUIManager 方法表全无 Find，OpenUI/CloseUI 不重置该 TMP）
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
                _headerTmp.text = LangHelper.T("蛙哥人脉网络", "Wage Brother Network");
                return;
            }
            if (ui.panel == null) return;
            var comps = ui.panel.gameObject.GetComponentsInChildren<Component>(true); // Patches.World.cs:143 同款
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
                foreach (var el in ui.networkElements)
                {
                    if (el == null || el.gameObject == null) continue;
                    if (tmp.gameObject.transform.IsChildOf(el.gameObject.transform)) { inElement = true; break; }
                }
                if (inElement) continue;
                // 顶部标题 = 剩余 TMP 中屏幕位置最靠上（y 最大）——顶部标题在面板顶，信用点/恩惠标签在其下方
                if (_headerTmp == null || tmp.transform.position.y > _headerTmp.transform.position.y)
                    _headerTmp = tmp;
            }
            _headerSearched = true;
            if (_headerTmp != null)
            {
                _headerTmp.text = LangHelper.T("蛙哥人脉网络", "Wage Brother Network");
                Core.LogMsg("[B2R] 顶部标题已替换为「蛙哥人脉网络」");
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
        _wageMode = false; // 关闭面板复位分流标记（防下次王尔德路径被污染）
    }

    // ================= ⑦ 来电 name 行本地化兜底（B2-R2-H2） =================
    // 拆包实锤（B2-R2-H2-D）：通话 name 行=GetLocalizedDisplayName()=GetLocalizedClientName(locID) 查 ClientName 共享表；
    //   蛙哥 locID="" → 表内无 "" 条目 → Unity 回显 "in ClientName" 错误。电话簿+通话 name 行都走此方法 → 修一处全覆盖。
    // 方案 B（最稳）：空 locID → fallback displayName（蛙哥 displayName="蛙哥" 已就绪，零新增数据；其余客户 locID 非空 → 放行原版）
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
