using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace JacksonPerks;

// ===== 第三方兼容守卫（09-26 设计稿《拾荒计数守卫+兼容共存升级》P0/P1）=====
// 拆包实锤（CompatDiag 作者 Z.Qiao）：第三方钓鱼 mod TackleShop.FishingPerkRuntime 的
// BeforeCanScavenge（挂原生 CanScavenge 的 Prefix）会临时清零 PlayerStore.scavengingAttempts
// （dump.cs:31874 public int scavengingAttempts）→ 我方 CanScavenge 快照失真 → 拾荒计数闸死。
// 守卫模式：对"第三方方法"再挂 Prefix/Postfix——Prefix 快照原生次数，Postfix 拉回，
// 阻断清零的长期影响（无条件生效：清零破坏的是原生状态，影响所有拾荒者）。
internal sealed partial class LuckScoutPerk
{
    private static int _savedAttempts = -1;

    /// <summary>安装第三方兼容守卫（PatchRegistry.ApplyAll 末尾调用；AppDomain 探测，未装则跳过）</summary>
    internal static void InstallCompatGuards()
    {
        InstallAttemptGuard();
        InstallKitchenGuard();
    }

    // ===== P0-改动1：scavengingAttempts 快照-恢复守卫 =====
    private static void InstallAttemptGuard()
    {
        try
        {
            var type = FindLoadedType("TackleShop.FishingPerkRuntime");
            if (type == null) { Core.LogMsg("[拾荒守卫] 未找到 TackleShop.FishingPerkRuntime，跳过（未装第三方钓鱼mod）"); return; }
            var method = type.GetMethod("BeforeCanScavenge", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null) { Core.LogMsg("[拾荒守卫] BeforeCanScavenge 方法不存在，跳过"); return; }
            var harmony = new HarmonyLib.Harmony("WagesPerks.CompatGuard.Attempt");
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(LuckScoutPerk), nameof(PrefixCompatGuard)),
                postfix: new HarmonyMethod(typeof(LuckScoutPerk), nameof(PostfixCompatGuard)));
            Core.LogMsg("[拾荒守卫] 已安装 scavengingAttempts 快照-恢复守卫（TackleShop.FishingPerkRuntime.BeforeCanScavenge）");
        }
        catch (System.Exception ex) { Core.LogMsg("[拾荒守卫] 安装失败: " + ex.Message); }
    }

    private static void PrefixCompatGuard()
    {
        try { _savedAttempts = PlayerStore.Instance != null ? PlayerStore.Instance.scavengingAttempts : -1; }
        catch { _savedAttempts = -1; }
    }

    private static void PostfixCompatGuard()
    {
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null && _savedAttempts >= 0) ps.scavengingAttempts = _savedAttempts; // 第三方清零后拉回
        }
        catch { }
    }

    // ===== P1-改动5 挂点A：第三方厨房估价空 identifier 守卫 =====
    // 学 CompatDiag SkipKitchenPricingForNullIdentifier：identifier 为空则跳过估价，防空字典键异常
    private static void InstallKitchenGuard()
    {
        try
        {
            var type = FindLoadedType("XIAOWOKitchenBusiness");
            if (type == null) { Core.LogMsg("[拾荒守卫] 未找到 XIAOWOKitchenBusiness，跳过"); return; }
            var method = type.GetMethod("KitchenBeverageCurrentValuePatch", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (method == null) { Core.LogMsg("[拾荒守卫] KitchenBeverageCurrentValuePatch 不存在，跳过"); return; }
            var harmony = new HarmonyLib.Harmony("WagesPerks.CompatGuard.Kitchen");
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(LuckScoutPerk), nameof(PrefixNullIdSkip)));
            Core.LogMsg("[拾荒守卫] 已安装空 identifier 估价守卫（XIAOWOKitchenBusiness.KitchenBeverageCurrentValuePatch）");
        }
        catch (System.Exception ex) { Core.LogMsg("[拾荒守卫] 厨房守卫安装失败: " + ex.Message); }
    }

    /// <summary>空 identifier 估价守卫：identifier 为 null/空 → 直接短路返回 0（防空字典键）；非空 → 放行</summary>
    private static bool PrefixNullIdSkip(object __instance, ref long __result)
    {
        try
        {
            var gi = __instance as GameItem;
            if (gi != null && string.IsNullOrEmpty(gi.identifier)) { __result = 0; return false; }
        }
        catch { }
        return true;
    }

    /// <summary>AppDomain 扫描按类型全名（宽松 Contains 匹配）找第三方类型；Il2Cpp 程序集遍历异常逐个吞</summary>
    private static System.Type FindLoadedType(string typeFullName)
    {
        try
        {
            var asms = System.AppDomain.CurrentDomain.GetAssemblies();
            if (asms == null) return null;
            foreach (var a in asms)
            {
                if (a == null) continue;
                try
                {
                    var t = a.GetType(typeFullName);
                    if (t != null) return t;
                }
                catch { }
                try
                {
                    foreach (var t2 in a.GetTypes())
                    {
                        if (t2 == null) continue;
                        string n = "";
                        try { n = t2.FullName ?? ""; } catch { continue; }
                        if (n.IndexOf(typeFullName, StringComparison.OrdinalIgnoreCase) >= 0) return t2;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
