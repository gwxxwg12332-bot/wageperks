using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    // ===== 物品归属判定 =====
    private static bool _ownedInited = false;
    private static System.Reflection.MethodInfo _isItemOwned = null;
    // 物品是否在玩家背包或柜台（指针比较，柜台陈列/待售物品也判定为玩家可控）
    private static bool IsInBackpackOrCounter(GameItem item)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || item == null) return false;
            IntPtr targetPtr = item.Pointer;
            if (targetPtr == IntPtr.Zero) return false;
            foreach (GameInventory inv in new[] { (GameInventory)em.backInvinvElement, (GameInventory)em.frontInvinvElement })
            {
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var ci = inv.childItems[i];
                    if (ci != null && ci.Pointer == targetPtr) return true;
                }
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return false;
    }

    private static bool IsItemOwned(GameItem item)
    {
        if (item == null) return false;
        if (!_ownedInited)
        {
            _ownedInited = true;
            try
            {
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetName().Name != "Assembly-CSharp") continue;
                    var type = assembly.GetType("GeneralHelper");
                    if (type == null)
                    {
                        var types = assembly.GetTypes();
                        foreach (var t2 in types) { if (t2.Name == "GeneralHelper") { type = t2; break; } }
                    }
                    if (type == null) break;
                    var methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    foreach (var mi in methods)
                    {
                        if (mi.Name == "IsItemOwned" && mi.GetParameters().Length == 1)
                        { _isItemOwned = mi; break; }
                    }
                    break;
                }

            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] IsItemOwned 查找异常 " + ex.Message); }
        }
        if (_isItemOwned == null) return true;
        try { return (bool)_isItemOwned.Invoke(null, new object[] { item }); }
        catch { return true; }
    }

    // ===== 09-13 用户拍板：枪械改装全局关闭（恢复"王尔德枪匠未解锁"状态）+ 定制单删除 =====
    // A. Postfix GunHelper.InitGun：拆包 09-13 [L1] moddable=true → SetGameItemType("MODDABLE")，模组系统按 type 判定可装——
    // 全局移除 MODDABLE type → 所有枪不可加零件（不显示可加零件）
    public static void PostfixInitGun(Il2Cpp.GameItem __0)
    {
        try
        {
            if (__0 == null) return;
            var types = __0.GetGameItemType();
            if (types != null && types.Contains("MODDABLE"))
                __0.RemoveGameItemType("MODDABLE");
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
    }

    // C. Prefix StoreClientListGun 订单生成：拦截定制单（拆包 09-13 [L1]：原生有 null 防护——客户端照常来店但无定制要求）
    public static bool PrefixBlockGunOrder() { return false; }

    // B. Prefix DirectoryMaster.Item：枪械模组 id 重定向无害物品（拆包 09-13 [L1]：全游戏物品创建统一入口，商店/奖励/全量随机都走它；
    // 模组物品无按 id 点名生成，只可能经全量池随机进入游戏 → 此处拦截全覆盖；重定向而非 null 防崩）
    private static System.Collections.Generic.HashSet<string> _gunModIds = null;
    private static readonly string[] _gunModIdFallback = {
        "rds_view", "rds_view2", "rds_makeshift_view", "silencer_view", "silencer2_view",
        "barrel_view", "compensator_view", "grip_view", "stock_view"
    };
    private static bool IsGunModIdentifier(string id)
    {
        try
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_gunModIds == null)
            {
                _gunModIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var s in _gunModIdFallback) _gunModIds.Add(s);
                // 运行时枚举 GunModDirectory 注册表补全（失败兜底硬编码）
                try
                {
                    var ids = Il2Cpp.DirectoryMaster.GetIdentifierList<object>("GunModDirectory");
                    if (ids != null) { foreach (var s in ids) { if (!string.IsNullOrEmpty(s)) _gunModIds.Add(s); } }
                }
                catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
            }
            return _gunModIds.Contains(id);
        }
        catch { return false; }
    }

    public static bool PrefixDirectoryMasterItem(ref string identifier, bool isOwned)
    {
        try
        {
            if (identifier != null && IsGunModIdentifier(identifier))
                identifier = "scrap_metal"; // 重定向无害废金属（防崩；模组物品不再生成到任何池）
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.ContainerUpgrade] 异常: " + ex.Message); }
        return true;
    }

}
