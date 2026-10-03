using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System.Linq;

namespace WageSurvival;
internal static class PatchRegistry
{
    internal static void ApplyAll()
    {
        // 双mod分工：鲁滨逊职业(startType=14)→WagePerks负责，WageSurvival让路
        // 其他职业→WageSurvival全局生效
        bool isRobinsonRun = false;
        try
        {
            var ps = PlayerStore.Instance;
            if (ps != null) isRobinsonRun = (int)ps.startType == 14;
        }
        catch (System.Exception ex) { Core.LogMsg($"[WageSurvival] 检测职业异常: {ex.Message}"); }
        Core.LogMsg($"[WageSurvival] 当前职业: {(isRobinsonRun ? "鲁滨逊(让路给WagePerks)" : "其他(全局生效)")}");

        var harmony = new HarmonyLib.Harmony("com.wagesurvival");

        // 双击吃喝（始终挂——WagePerks让路给WageSurvival）
        try
        {
            var orig = AccessTools.Method(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixDoubleClickAction");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: ItemMouseDoubleClickHandler.DoubleClickAction");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: DoubleClickAction " + ex.Message);
        }

        // Z键快捷键（始终挂——不管WagePerks加不加载）
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.InputActionManager), "Update");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixFrameUpdate");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: InputActionManager.Update");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: InputActionManager.Update " + ex.Message);
        }

        // 存档读写（始终挂——不管WagePerks加不加载）
        try
        {
            var origLoad = AccessTools.Method(typeof(PlayerStore), "LoadGame");
            var postLoad = AccessTools.Method(typeof(SurvivalFood), "PostfixLoadGame");
            harmony.Patch(origLoad, postfix: new HarmonyMethod(postLoad));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.LoadGame");

            var origSave = AccessTools.Method(typeof(PlayerStore), "SaveGame");
            var postSave = AccessTools.Method(typeof(SurvivalFood), "PostfixSaveGame");
            harmony.Patch(origSave, postfix: new HarmonyMethod(postSave));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.SaveGame");

            // 10-03 补：打烊落盘（踩WagePerks老坑——SaveGame只在进游戏时调一次）
            var origEndDay = AccessTools.Method(typeof(PlayerStore), "EndDay");
            var postEndDay = AccessTools.Method(typeof(SurvivalFood), "PostfixEndDay");
            harmony.Patch(origEndDay, postfix: new HarmonyMethod(postEndDay));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.EndDay → Flush");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: LoadGame/SaveGame " + ex.Message);
        }

        // 鲁滨逊职业→让路给WagePerks（避免效果叠加）
        if (isRobinsonRun)
        {
            Core.LogMsg("[WageSurvival] 鲁滨逊职业，生存系统patch让路（WagePerks负责）");
            return;
        }

        // 每天衰减（StoreEventManager.OnDayStart Postfix）
        try
        {
            var orig = AccessTools.Method(typeof(StoreEventManager), "OnDayStart");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixOnDayStart");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreEventManager.OnDayStart");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: OnDayStart " + ex.Message);
        }

        // 交易价格（饱食影响售价）
        try
        {
            var orig = AccessTools.Method(typeof(GameItem), "GetCurrentValue");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixGetCurrentValue");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: GameItem.GetCurrentValue");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: GetCurrentValue " + ex.Message);
        }

        // 客户预算加成（心情/Nodes/昂扬影响预算）
        try
        {
            var orig = AccessTools.Method(typeof(StoreClientManager), "PickClient");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixPickClient");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClientManager.PickClient");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: PickClient " + ex.Message);
        }

        // 交易声望倍率（心情影响声望）
        try
        {
            var orig = AccessTools.Method(typeof(StoreClient), "GetTradeRepMultiplier");
            var post = AccessTools.Method(typeof(SurvivalFood), "PostfixGetTradeRepMultiplier");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: StoreClient.GetTradeRepMultiplier");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: GetTradeRepMultiplier " + ex.Message);
        }

        // 禁外出（低落/崩溃时禁外出）
        try
        {
            var orig = AccessTools.Method(typeof(MapUIManager), "OpenGoOutsideConfirm");
            var pre = AccessTools.Method(typeof(SurvivalFood), "PrefixOpenGoOutsideConfirm");
            harmony.Patch(orig, prefix: new HarmonyMethod(pre));
            Core.LogMsg("[WageSurvival] Patch OK: MapUIManager.OpenGoOutsideConfirm");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: OpenGoOutsideConfirm " + ex.Message);
        }

        // 特性成长：每天累计天数
        try
        {
            var orig = AccessTools.Method(typeof(StoreEventManager), "OnDayStart");
            var post = AccessTools.Method(typeof(SurvivalGrowthHelper), "OnDayStart");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PerkGrowth.OnDayStart");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: PerkGrowth.OnDayStart " + ex.Message);
        }

        // 特性成长：开局应用加成
        try
        {
            var orig = AccessTools.Method(typeof(Il2Cpp.PerkUIController), "OpenUI");
            var post = AccessTools.Method(typeof(SurvivalGrowthHelper), "ApplyOnPerkUiOpen");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PerkGrowth.ApplyOnPerkUiOpen");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: PerkGrowth.ApplyOnPerkUiOpen " + ex.Message);
        }

        // 新档：清_mem + 重置状态（原版踩坑：StartNewGame Postfix，不是GameMaster.NewGame）
        try
        {
            var orig = AccessTools.Method(typeof(PlayerStore), "StartNewGame");
            var post = AccessTools.Method(typeof(SaveStore), "PostfixStartNewGame");
            harmony.Patch(orig, postfix: new HarmonyMethod(post));
            Core.LogMsg("[WageSurvival] Patch OK: PlayerStore.StartNewGame");
        }
        catch (System.Exception ex)
        {
            Core.LogMsg("[WageSurvival] Patch FAIL: PlayerStore.StartNewGame " + ex.Message);
        }
    }
}
