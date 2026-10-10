using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace WageBlackGold;
/// <summary>黑金典藏箱挂点注册（WS 模式：手工 AccessTools + Harmony.Patch，逐条 try/catch）。</summary>
internal static class PatchRegistry
{
    internal static void ApplyAll()
    {
        Core.LogMsg("[黑金典藏箱] 挂点注册开始（5 组）");
        var harmony = new HarmonyLib.Harmony("com.wageblackgold");

        // 1. RenderHandler.LoadFromAtlas Prefix——自定义 atlas 拦截（黑金箱换皮）
        try
        {
            var orig = AccessTools.Method(typeof(RenderHandler), "LoadFromAtlas");
            var pre = AccessTools.Method(typeof(BlackGoldBox), "PrefixLoadFromAtlas");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[黑金典藏箱] Patch OK: RenderHandler.LoadFromAtlas");
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: LoadFromAtlas " + ex.Message); }

        // 2. EmporiumEntry.ShowAfterhourInv Postfix——博士夜晚商店上架（价格 2000）
        try
        {
            var orig = FindMethodByName(typeof(EmporiumEntry), "ShowAfterhourInv");
            if (orig == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: EmporiumEntry.ShowAfterhourInv 按名未找到"); }
            else
            {
                var post = AccessTools.Method(typeof(BlackGoldBoxStyle), "PostfixShowAfterhourInv");
                harmony.Patch(orig, postfix: new HarmonyMethod(post));
                Core.LogMsg("[黑金典藏箱] Patch OK: EmporiumEntry.ShowAfterhourInv");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: ShowAfterhourInv " + ex.Message); }

        // 2b. StoreClientList.PlaceInventorInventory Postfix——博士到访上货入口（10-08 对齐鲁滨逊先例触发链 Patches.Npc.Client.cs:279）
        try
        {
            var origP = FindMethodByName(typeof(Il2Cpp.StoreClientList), "PlaceInventorInventory");
            if (origP == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: StoreClientList.PlaceInventorInventory 按名未找到"); }
            else
            {
                var postP = AccessTools.Method(typeof(BlackGoldBoxStyle), "PostfixPlaceInventorInventory", new System.Type[] { typeof(bool) });
                harmony.Patch(origP, postfix: new HarmonyMethod(postP));
                Core.LogMsg("[黑金典藏箱] Patch OK: StoreClientList.PlaceInventorInventory");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: PlaceInventorInventory " + ex.Message); }

        // 3. 原版菜单"切换"按钮复用（拆包 AI 10-08 返工实锤：TriggerButton ISIL :3956=Call GameItem.ToggleSlotItem；
        // 按钮显示条件=CanToggleSlotItem）——黑金箱 Patch CanToggleSlotItem（显示按钮）+ ToggleSlotItem（切样式），
        // 其他物品原样放行。Shift+双击自研链已退役（10-08 用户拍板：参考原版箱子左键切换）。
        try
        {
            var origC = FindMethodByName(typeof(GameItem), "CanToggleSlotItem");
            if (origC == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: GameItem.CanToggleSlotItem 按名未找到"); }
            else
            {
                var preC = AccessTools.Method(typeof(BlackGoldBoxStyle), "PrefixCanToggleSlotItem");
                harmony.Patch(origC, prefix: new HarmonyMethod(preC));
                Core.LogMsg("[黑金典藏箱] Patch OK: GameItem.CanToggleSlotItem（菜单显示切换按钮）");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: CanToggleSlotItem " + ex.Message); }

        // 3b. ToggleSlotItem Prefix——黑金箱→NextStyle（切样式），其他物品放行原版
        try
        {
            var origT = FindMethodByName(typeof(GameItem), "ToggleSlotItem");
            if (origT == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: GameItem.ToggleSlotItem 按名未找到"); }
            else
            {
                var preT = AccessTools.Method(typeof(BlackGoldBoxStyle), "PrefixToggleSlotItem");
                harmony.Patch(origT, prefix: new HarmonyMethod(preT));
                Core.LogMsg("[黑金典藏箱] Patch OK: GameItem.ToggleSlotItem（黑金切换样式）");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: ToggleSlotItem " + ex.Message); }

        // 3c. MayToggleSlotItem Prefix——拆包 AI v4 实锤：切换点击条件 canToggle=[0x203]=MayToggleSlotItem && CanToggleSlotItem
        // （ItemContextHandler:2137-2149）——黑金只 Patch Can（Can=true）但 May=false（无槽）→ [0x203]=0 → 按钮可见但点不了。
        // 补 Patch MayToggleSlotItem→true → May&&Can 双 true → [0x203]=1 → 点击触发（10-08 修"点不了"）。
        try
        {
            var origM = FindMethodByName(typeof(GameItem), "MayToggleSlotItem");
            if (origM == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: GameItem.MayToggleSlotItem 按名未找到"); }
            else
            {
                var preM = AccessTools.Method(typeof(BlackGoldBoxStyle), "PrefixMayToggleSlotItem");
                harmony.Patch(origM, prefix: new HarmonyMethod(preM));
                Core.LogMsg("[黑金典藏箱] Patch OK: GameItem.MayToggleSlotItem（黑金切换可点）");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: MayToggleSlotItem " + ex.Message); }

        // 4. GameItem.SetContentWindow Postfix——读档重建内容窗口时恢复样式（400 格内容原生 DecodeNodes）
        try
        {
            var orig = FindMethodByName(typeof(GameItem), "SetContentWindow");
            if (orig == null) { Core.LogMsg("[黑金典藏箱] Patch FAIL: GameItem.SetContentWindow 按名未找到"); }
            else
            {
                var post = AccessTools.Method(typeof(BlackGoldBoxStyle), "PostfixSetContentWindow");
                harmony.Patch(orig, postfix: new HarmonyMethod(post));
                Core.LogMsg("[黑金典藏箱] Patch OK: GameItem.SetContentWindow");
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: SetContentWindow " + ex.Message); }

        // 5. 物品目录 InitDirectory Postfix——黑金箱注册（读档原生恢复窗口；挂 3 个目录子类=CustomStorageContainer 先例）
        foreach (var t in new System.Type[] { typeof(ContainerItemDirectory), typeof(AmenitiesItemDirectory), typeof(ModItemDirectory) })
        {
            try
            {
                var orig = AccessTools.Method(t, "InitDirectory");
                var post = AccessTools.Method(typeof(BlackGoldBox), "PostfixInitDirectory");
                harmony.Patch(orig, postfix: new HarmonyMethod(post));
                Core.LogMsg("[黑金典藏箱] Patch OK: " + t.Name + ".InitDirectory");
            }
            catch (System.Exception ex) { Core.LogMsg("[黑金典藏箱] Patch FAIL: " + t.Name + ".InitDirectory " + ex.Message); }
        }
    }

    /// <summary>按方法名遍历查找（Il2Cpp 互操作兜底——ManualPatcher.TryPatchByName 同款：裸 AccessTools.Method 带参/私有方法可能失败）。</summary>
    private static System.Reflection.MethodInfo FindMethodByName(System.Type type, string name)
    {
        try
        {
            foreach (var mi in AccessTools.GetDeclaredMethods(type))
            {
                if (mi != null && mi.Name == name) return mi;
            }
        }
        catch { }
        return null;
    }
}
