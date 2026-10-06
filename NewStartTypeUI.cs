using System;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2Cpp;
using System.Reflection;

namespace WageSurvival;

// ============================================================
// 【新开局职业 UI】克隆"牧场主"(tabRancher) tab → 第 15 职业「空间站鲁滨逊」
//
// 10-06 从 WagePerks 复制（拆分缺口修复：单装 WS 选不了空间站鲁滨逊 → startType=15 门控不可达）。
// 拆包依据（全部 [L1]，同 WP NewStartTypeUI）：
//   - 开局职业 = NewGameData+StartType 枚举（1~13），IL2CPP 编译期常量，无法新增枚举值
//   - 职业选择 UI = MainMenuUIController 的 12 张 tab Image（tabWelcome~tabRancher，全 public 字段）
//   - NewGameData.startType 是 public 字段(0x18)，可直接写任意 int（游戏无 1-13 范围校验）
//
// 双装安全性：本挂点在 WS ApplyAll（单装）注册；双装 WS 走 ApplyGlobalConsumeOnly 不挂此链，由 WP 提供（同 key 标记兼容）。
// ============================================================
internal static class NewStartTypeUI
{
    private const int NEW_START_TYPE = 15;
    private const string CLONE_NAME = "tab_RobinCrusoe";
    private const string NEW_START_MARKER_KEY = "WagesNewStartType_Run";       // 老单 key（v3 方案甲，仅兼容旧档）
    private const string NEW_START_MARKER_PREFIX = "WagesNewStartType_Run_";  // 09-19 per-runID key：多鲁滨逊档互不覆盖

    private static string MarkerKey(string runId) => NEW_START_MARKER_PREFIX + runId;
    internal static bool IsMarkedRun(string runId)
    {
        if (string.IsNullOrEmpty(runId)) { _pendingRecheck = true; return false; } // 读档早期 runID 未恢复：不判定 + 待重判
        try { if (UnityEngine.PlayerPrefs.GetString(MarkerKey(runId), "") == "1") return true; } catch { }
        return UnityEngine.PlayerPrefs.GetString(NEW_START_MARKER_KEY, "") == runId; // 老 key 兜底（旧档迁移）
    }
    private static bool _pendingRecheck = false;
    internal static void RecheckIfPending()
    {
        try
        {
            if (!_pendingRecheck) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || string.IsNullOrEmpty(ps.runID ?? "")) return;
            _pendingRecheck = false;
            if (IsMarkedRun(ps.runID))
                ps.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE;
        }
        catch (System.Exception ex) { Core.LogMsg("[NewStartTypeUI] 异常: " + ex.Message); }
    }
    private static readonly string START_NAME = LangHelper.T("空间站鲁滨逊", "Space Station Robinson");
    // 群宣传语（10-06 WS 内联：WP 侧 CustomStartingPerks.CommunityNote 同文本）
    private static readonly string COMMUNITY_NOTE = LangHelper.T(
        "\n\n参考了群内网友的热心建议（群号：1109707341）！快来加入，你的建议也有可能被采纳。",
        "\n\nInspired by suggestions from our community (QQ Group: 1109707341)! Join us - your idea could be featured.");

    private static UnityEngine.UI.Image GetTabRancher(Il2Cpp.MainMenuUIController mc)
    {
        try
        {
            var direct = mc.tabRancher;
            if (direct != null) return direct;
        }
        catch { /* 当前版本无 tabRancher 字段 → 走反射兜底 */ }
        try
        {
            var t = Il2CppSystem.Type.GetType("MainMenuUIController, Assembly-CSharp");
            if (t == null) return null;
            object val = null;
            var f = t.GetField("tabRancher");
            if (f != null) { val = f.GetValue(mc); }
            else
            {
                var p = t.GetProperty("tabRancher");
                if (p != null && p.GetGetMethod() != null) val = p.GetGetMethod().Invoke(mc, null);
            }
            if (val == null) return null;
            return val as UnityEngine.UI.Image;
        }
        catch { return null; }
    }

    public static void PostfixAwake(Il2Cpp.MainMenuUIController __instance)
    {
        try
        {
            if (GameObject.Find(CLONE_NAME) != null) return;

            var tabSrc = GetTabRancher(__instance);
            if (tabSrc == null)
            {
                Core.LogMsg("[新职业] tabRancher 不可用（当前游戏版本无此成员），跳过克隆");
                return;
            }

            var template = tabSrc.gameObject;
            var clone = UnityEngine.Object.Instantiate<GameObject>(template, template.transform.parent);
            clone.name = CLONE_NAME;
            clone.transform.SetAsFirstSibling();

            int replaced = 0;
            ReplaceAllText(clone.transform, ref replaced);

            var img = clone.GetComponent<Image>();
            if (img != null && __instance.tab != null)
                img.sprite = __instance.tab;

            var btn = clone.GetComponent<Button>();
            if (btn == null) btn = clone.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            var et = clone.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et != null) et.triggers.Clear();
            var capturedClone = clone;
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(
                () => OnNewTabClicked(__instance, capturedClone)));

        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] 克隆异常: " + ex.Message);
        }
    }

    private static void ReplaceAllText(Transform root, ref int replaced)
    {
        try
        {
            if (root == null) return;
            var cs = root.gameObject.GetComponents<UnityEngine.Component>();
            if (cs != null)
            {
                foreach (var c in cs)
                {
                    if (c == null) continue;
                    string cn = "";
                    try { cn = c.GetIl2CppType().FullName ?? ""; } catch { }
                    if (string.IsNullOrEmpty(cn)) { try { cn = c.GetType().Name ?? ""; } catch { } }
                    if (cn.Contains("LocalizeStringEvent") || cn.Contains("TMPLocalizer"))
                    {
                        try { UnityEngine.Object.Destroy(c); } catch { }
                        continue;
                    }
                    if (!cn.Contains("Text") && !cn.Contains("TMP")) continue;
                    bool setOk = false;
                    try
                    {
                        var tmp = c.TryCast<Il2CppTMPro.TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.text = START_NAME;
                            replaced++; setOk = true;
                        }
                    }
                    catch (System.Exception ex) { Core.LogMsg("[NewStartTypeUI] 异常: " + ex.Message); }
                    try
                    {
                        Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
                        if (tmpType != null)
                        {
                            var prop = tmpType.GetProperty("text");
                            if (prop != null)
                            {
                                prop.SetValue(c, START_NAME);
                                replaced++; setOk = true;
                            }
                        }
                    }
                    catch (System.Exception ex) { Core.LogMsg("[NewStartTypeUI] 异常: " + ex.Message); }
                    if (!setOk)
                    {
                        try
                        {
                            var prop = c.GetType().GetProperty("text");
                            if (prop != null)
                            {
                                prop.SetValue(c, START_NAME);
                                replaced++; setOk = true;
                            }
                        }
                        catch (System.Exception ex) { Core.LogMsg("[NewStartTypeUI] 异常: " + ex.Message); }
                    }
                }
            }
            for (int i = 0; i < root.childCount; i++)
                ReplaceAllText(root.GetChild(i), ref replaced);
        }
        catch (System.Exception ex) { Core.LogMsg("[NewStartTypeUI] 异常: " + ex.Message); }
    }

    public static void PostfixResetAllTab(Il2Cpp.MainMenuUIController __instance)
    {
        try
        {
            var go = GameObject.Find(CLONE_NAME);
            if (go == null) return;
            var img = go.GetComponent<Image>();
            if (img != null && __instance.tab != null)
                img.sprite = __instance.tab;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] ResetAllTab 恢复异常: " + ex.Message);
        }
    }

    private static void OnNewTabClicked(Il2Cpp.MainMenuUIController ui, GameObject tabGo)
    {
        try
        {
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng == null)
            {
                Core.LogMsg("[新职业] NewGameData.Instance 为空，跳过");
                return;
            }
            ng.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE;

            if (ui != null)
            {
                ui.ResetAllTab();
                var img = tabGo.GetComponent<Image>();
                if (img != null && ui.tabSelected != null)
                    img.sprite = ui.tabSelected;

                if (ui.title != null) ui.title.text = START_NAME;
                if (ui.startingEquipment != null) ui.startingEquipment.text = LangHelper.T(
                    "起始装备：口粮×3、大肉×2、大瓶纯水×3、绷带×5",
                    "Starting gear: Rations ×3, Raw Meat ×2, Large Bottled Water ×3, Bandages ×5");
                if (ui.note != null) ui.note.text = LangHelper.T(
                    "新开局类型",
                    "New Starting Type");
                if (ui.body != null) ui.body.text = LangHelper.T(
                    "全新的开局类型，在开局选择界面新增标签页。你被困在空间站上，像鲁滨逊一样活下去：\n" +
                    "每日维持 2200 卡路里与 2000ml 饮水，六维生存状态：饱食 / 口渴 / 健康 / 清洁 / 睡眠 / 社交\n\n" +
                    "6 条状态轴、20 个状态节点：从“饿疯 / 嗓子冒烟”到“吃饱喝足 / 透心凉”，实时影响客户、售价、议价、拾荒与伤病\n" +
                    "双击即食：食物咬一口 / 饮品喝一口 / 药品用药；食物有热量、质量、腐烂，脏水会生病\n" +
                    "三条死亡线与保底救援：饿死 / 渴死 / 病死，临界时客户救援机会\n" +
                    "粮仓与升华：饱腹满 7 天售价 +5%；满状态每 2 天升 1 级（上限 5）\n" +
                    "8 种状态客户登门买卖；按 Z 键随时打开生存状态面板\n" +
                    "容器容量减半：腰包、储存箱与机器内嵌箱子开局容量减半（主背包除外），可拖垃圾（junk）逐格升级恢复\n" +
                    "博士夜晚到访，出售食物与水\n" +
                    "水瓶打印机：电子元件升级瓶型（满级打印 6000ml 超大瓶），金属锭升级质量（100 出普通水、更高优质/纯水）\n\n" +
                    "特性成长：每累计存活50天且当天现金>0时+1点+1槽（全局继承新档）；困难模式额外+1点+1槽；负面特性>正面特性额外+1点+1槽\n\n" +
                    COMMUNITY_NOTE,
                    "A brand-new starting type with its own tab on the start screen. Stranded on the station, survive like Robinson Crusoe:\n" +
                    "Daily 2200 kcal & 2000 ml water; six survival stats: satiety, thirst, health, cleanliness, sleep, social\n\n" +
                    "6 status axes, 20 nodes — from Starving/Parched to Well-Fed/Quenched, each affecting customers, prices, haggling, scavenging and wounds\n" +
                    "Double-click to eat, drink and take medicine; food has calories, quality and decay; dirty water makes you sick\n" +
                    "Three death lines with rescue chances\n" +
                    "Granary: sell +5% after 7 well-fed days; Ascension levels up every 2 days\n" +
                    "8 status customers drop by with special deals; press Z for the survival panel\n" +
                    "Containers start halved: pouches, storage and machine bins (main backpack excluded); drag junk to upgrade back\n" +
                    "The Doctor visits at night, selling food and water\n" +
                    "Bottle printer: electronic parts upgrade bottle type (max 6000ml jug), metal ingots upgrade quality (100 = plain water, higher = premium/pure)\n\n" +
                    "Perk growth: every 50 global days +1 point +1 slot (persists across saves); Hard mode +1 slot; picking 10+ cost negative perks +1 slot\n\n" +
                    COMMUNITY_NOTE);
            }

        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] 点击处理异常: " + ex.Message);
        }
    }

    public static void PostfixGetStartDisplayName(Il2Cpp.NewGameData.StartType startType, ref string __result)
    {
        try
        {
            if ((int)startType == NEW_START_TYPE)
                __result = START_NAME;
            if ((int)startType == 12 && IsMarkedCurrentRun())
                __result = START_NAME;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] 显示名异常: " + ex.Message);
        }
    }

    public static void PostfixBuildPreviewFromStore(Il2Cpp.PlayerStore store, int slotId, Il2Cpp.SavePreviewData __result)
    {
        try
        {
            if (store != null && __result != null && (int)store.startType == NEW_START_TYPE)
            {
                __result.startType = (Il2Cpp.NewGameData.StartType)12;
                __result.startName = START_NAME;
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] BuildPreviewFromStore 异常: " + ex.Message);
        }
    }

    // 存档格式兼容标记（PlayerPrefs 直写豁免：必须在存档文件写入【之前】立即持久，WageSaveStore 打烊落盘时序在后）
    public static void PrefixSaveGame(Il2Cpp.PlayerStore __instance)
    {
        try
        {
            if (__instance != null && (int)__instance.startType == NEW_START_TYPE)
            {
                string runId = __instance.runID ?? "";
                UnityEngine.PlayerPrefs.SetString(MarkerKey(runId), "1");
                UnityEngine.PlayerPrefs.SetString(NEW_START_MARKER_KEY, runId);
                UnityEngine.PlayerPrefs.Save();
                __instance.startType = (Il2Cpp.NewGameData.StartType)12;
            }
        }
        catch (Exception ex) { Core.LogMsg("[新职业] PrefixSaveGame 异常: " + ex.Message); }
    }

    private static bool IsMarkedCurrentRun()
    {
        try
        {
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null) return false;
            return IsMarkedRun(ps.runID ?? "");
        }
        catch { return false; }
    }

    public static void PostfixSaveGame(Il2Cpp.PlayerStore __instance)
    {
        try
        {
            if (__instance != null && IsMarkedRun(__instance.runID ?? ""))
                __instance.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE;
        }
        catch (Exception ex) { Core.LogMsg("[新职业] PostfixSaveGame 异常: " + ex.Message); }
    }

    public static void PostfixLoadGame(Il2Cpp.PlayerStore __instance)
    {
        try
        {
            if (__instance != null && IsMarkedRun(__instance.runID ?? ""))
                __instance.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE;
        }
        catch (Exception ex) { Core.LogMsg("[新职业] PostfixLoadGame 异常: " + ex.Message); }
    }
}
