using System;
using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;
using Il2Cpp;
using System.Reflection;

namespace JacksonPerks;

// ============================================================
// 【新开局职业 UI】克隆"牧场主"(tabRancher) tab → 第 14 职业「鲁滨逊的账本」
//
// 拆包依据（全部 [L1]）：
//   - 开局职业 = NewGameData+StartType 枚举（1~13），IL2CPP 编译期常量，无法新增枚举值
//   - 职业选择 UI = MainMenuUIController 的 12 张 tab Image（tabWelcome~tabRancher，全 public 字段）
//   - NewGameData.startType 是 public 字段(0x18)，可直接写任意 int（游戏无 1-13 范围校验）
//   - HandleInitialItem 无 14 分支 → 进 StartingPerk 公共段（与无特判职业一致）
//
// v2 修复（用户实测反馈）：
//   1. 场景重载后不克隆 → 去掉 static 标志，改 GameObject.Find 场景级防重
//   2. 始终高亮 → 克隆后强制普通态 + Patch ResetAllTab Postfix 把新 tab 也纳入恢复
//   3. 点击重复触发（6 次） → 克隆体继承的原生 EventTrigger 持久事件一并清空
// v3（方案甲定稿，2026-09-08）：
//   职业正式化「鲁滨逊的账本」——克隆源改 tabRancher（0x160），删原型诊断，
//   存档修复全套保留（SaveGame 14→12 / LoadGame 恢复 / 索引预览合法化）
// ============================================================
internal static class NewStartTypeUI
{
    private const int NEW_START_TYPE = 14;
    private const string CLONE_NAME = "tab_RobinCrusoe";
    private const string NEW_START_MARKER_KEY = "WagesNewStartType_Run";       // 老单 key（v3 方案甲，仅兼容旧档）
    private const string NEW_START_MARKER_PREFIX = "WagesNewStartType_Run_";  // 09-19 per-runID key：多鲁滨逊档互不覆盖

    // 09-19 修复：旧档"有几率无状态/无法吃饭"根因 = 单 key 只存最后一次 SaveGame 的 runID，
    // 多档玩家新开鲁滨逊档后切回旧鲁滨逊档 → 标记不匹配 → LoadGame 不恢复 14 → IsActive() false（状态面板+双击全失效）
    // 修复：标记按 runID 独立存储（新 key 优先，老 key 兜底迁移）
    private static string MarkerKey(string runId) => NEW_START_MARKER_PREFIX + runId;
    internal static bool IsMarkedRun(string runId)
    {
        if (string.IsNullOrEmpty(runId)) { _pendingRecheck = true; return false; } // 09-22 读档早期 runID 未恢复：不判定 + 待重判
        try { if (UnityEngine.PlayerPrefs.GetString(MarkerKey(runId), "") == "1") return true; } catch { }
        return UnityEngine.PlayerPrefs.GetString(NEW_START_MARKER_KEY, "") == runId; // 老 key 兜底（旧档迁移）
    }
    // 09-22 runID时序修复：读档早期 runID 未恢复时 IsMarkedRun 挂起，runID 恢复后重判一次
    private static bool _pendingRecheck = false;
    internal static void RecheckIfPending()
    {
        try
        {
            if (!_pendingRecheck) return;
            var ps = Il2Cpp.PlayerStore.Instance;
            if (ps == null || string.IsNullOrEmpty(ps.runID ?? "")) return; // runID 仍未恢复，保持挂起
            _pendingRecheck = false; // 已恢复，后续 IsMarkedRun(runID) 自然重判
            if (IsMarkedRun(ps.runID))
                ps.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE; // 兜底：PostfixLoadGame 若因 runID 空未恢复，这里补恢复
        }
        catch { }
    }
    // 双语：const 无法运行时切换 → static readonly（LangHelper.IsEnglish 延迟求值）。职业英文名先拟 Space Station Robinson，可改。
    private static readonly string START_NAME = LangHelper.T("空间站鲁滨逊", "Space Station Robinson");

    // Patch MainMenuUIController.Awake（场景加载时执行，tab 字段已序列化注入）
    // 09-14 修正：编译期直接访问优先（当前游戏版本有 tabRancher 字段时最可靠——Il2Cpp 下 Type.GetType 按程序集名经常失败）；
    // 反射仅作兜底（旧版本无该字段时走字段/属性双通道）
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
            // 场景级防重：场景重载后新实例会重新克隆（不能用 static 标志，否则重载后消失）
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
            clone.transform.SetAsLastSibling();

            // 克隆体上所有文字统一改为"空间站鲁滨逊"
            // 递归遍历子物体（GetComponentsInChildren 泛型在 Il2Cpp 下不稳定，改逐层 GetComponents）
            int replaced = 0;
            ReplaceAllText(clone.transform, ref replaced);

            // 强制普通态（避免继承选中态导致"始终高亮"）
            var img = clone.GetComponent<Image>();
            if (img != null && __instance.tab != null)
                img.sprite = __instance.tab;

            // 清掉克隆体复制的所有原生点击源（Button 持久监听 + EventTrigger 持久事件），只留我们自己的
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

    // ===== 替换克隆体上所有 TMP 文字（tab 上只有职业名文字）=====
    // 诊断版：GetComponents + 反射 set_text（夜间报告同款模式），全程打日志定位失败点
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
                    // 本地化组件：直接销毁断链（拆包 [L1]：TMPLocalizer.Awake 订阅 LocalizeStringEvent 写回覆盖，
                    // 禁用 enabled 不够——Awake 已订阅；Destroy 组件后不再接收事件，set_text 才能保持）
                    if (cn.Contains("LocalizeStringEvent") || cn.Contains("TMPLocalizer"))
                    {
                        try { UnityEngine.Object.Destroy(c);  } catch { }
                        continue;
                    }
                    if (!cn.Contains("Text") && !cn.Contains("TMP")) continue;
                    System.Reflection.MethodInfo[] ms = null;
                    try { ms = c.GetType().GetMethods(); } catch { }
                    if (ms != null)
                    {
                        var nm = new System.Collections.Generic.List<string>();
                        foreach (var mi in ms)
                        {
                            if (mi == null) continue;
                            string mn = mi.Name ?? "";
                            if (mn.IndexOf("ext", StringComparison.OrdinalIgnoreCase) >= 0 || mn.IndexOf("m_", StringComparison.OrdinalIgnoreCase) >= 0)
                                nm.Add(mn);
                        }
                    }
                    bool setOk = false;
                    // 根治：编译期 Il2CppTMPro 引用（Il2Cpp 互操作把 TMPro 命名空间改为 Il2CppTMPro）
                    // 直接类型化设置 text——反射在 Il2Cpp 下拿不到子类成员（c.GetType() 返回基类），编译期类型不受影响
                    if (!setOk)
                    {
                        try
                        {
                            var tmp = c.TryCast<Il2CppTMPro.TextMeshProUGUI>();
                            if (tmp != null)
                            {
                                tmp.text = START_NAME;
                                replaced++; setOk = true;
                            }
                        }
                        catch { }
                    }
                    // 方案0：运行时 Type.GetType（编译期 TMPro 在 Il2Cpp 下 CS0246 不可行——参考 mod 全用字符串判断）
                    // Il2Cpp 下 c.GetType() 返回基类 Object（诊断实锤只有 m_CachedPtr）；运行时按程序集名取真实类型
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
                    catch { }
                    // 方案1：set_text / SetText(string) 方法反射（Il2Cpp 下 GetType 是基类，通常不可用，保留兜底）
                    if (ms != null)
                    {
                        foreach (var mi in ms)
                        {
                            if (mi == null) continue;
                            string mn = mi.Name ?? "";
                            if (mn != "set_text" && mn != "SetText") continue;
                            var ps = mi.GetParameters();
                            if (ps == null || ps.Length != 1) continue;
                            try { mi.Invoke(c, new object[] { START_NAME }); replaced++; setOk = true;  }
                            catch { }
                            break;
                        }
                    }
                    // 方案2：GetProperty("text") 属性反射（Il2Cpp 下方法名可能不含 set_text）
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
                            else
                            { }
                        }
                        catch { }
                    }
                }
            }
            for (int i = 0; i < root.childCount; i++)
                ReplaceAllText(root.GetChild(i), ref replaced);
        }
        catch { }
    }



    // Patch MainMenuUIController.ResetAllTab Postfix：新 tab 也恢复普通态（原生 ResetAllTab 只恢复 12 个原生 tab）
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

    // 新 tab 点击：设置 startType=14 + 高亮 + 描述
    private static void OnNewTabClicked(Il2Cpp.MainMenuUIController ui, GameObject tabGo)
    {
        try
        {
            // 1. 设置开局职业 = 14
            var ng = Il2Cpp.NewGameData.Instance;
            if (ng == null)
            {
                Core.LogMsg("[新职业] NewGameData.Instance 为空，跳过");
                return;
            }
            ng.startType = (Il2Cpp.NewGameData.StartType)NEW_START_TYPE;

            // 2. 高亮：全部恢复普通态，再把新 tab 设为选中态
            if (ui != null)
            {
                ui.ResetAllTab();
                var img = tabGo.GetComponent<Image>();
                if (img != null && ui.tabSelected != null)
                    img.sprite = ui.tabSelected;

                // 3. 描述 + 标题（title 显示职业名，原生 tab 点击会设 title；默认高亮 Rancher 所以 title 是"养鼠人"，必须一并改）
                if (ui.title != null) ui.title.text = START_NAME;
                // 布局实测（截图 09-09）：note 是 title 上方的小字区，只能 1-2 行，放 6 行会溢出盖住 title/body
                // → note = 标题行（新开局类型）；body = 介绍 + 每日维持 + 细节，分段（空行）分层
                if (ui.startingEquipment != null) ui.startingEquipment.text = LangHelper.T(
                    "起始装备：口粮×3、大肉×2、大瓶纯水×3、绷带×5",
                    "Starting gear: Rations ×3, Raw Meat ×2, Large Bottled Water ×3, Bandages ×5");
                if (ui.note != null) ui.note.text = LangHelper.T(
                    "新开局类型（v1.1.4 新增）",
                    "New Starting Type (v1.1.4)");
                if (ui.body != null) ui.body.text = LangHelper.T(
                    "全新的开局类型，在开局选择界面新增标签页。你被困在空间站上，像鲁滨逊一样活下去：\n" +
                    "每日维持 2200 卡路里与 2000ml 饮水，六维生存状态：饱食 / 口渴 / 健康 / 清洁 / 睡眠 / 社交\n\n" +
                    "6 条状态轴、20 个状态节点：从“饿疯 / 嗓子冒烟”到“吃饱喝足 / 透心凉”，实时影响客户、售价、议价、拾荒与伤病\n" +
                    "双击即食：食物咬一口 / 饮品喝一口 / 药品用药；食物有热量、质量、腐烂，脏水会生病\n" +
                    "三条死亡线与保底救援：饿死 / 渴死 / 病死，临界时客户救援机会\n" +
                    "粮仓与升华：饱腹满 7 天售价 +5%；满状态每 2 天升 1 级（上限 5）\n" +
                    "8 种状态客户登门买卖；按 Z 键随时打开生存状态面板\n" +
                    "容器容量减半：腰包、储存箱与机器内嵌箱子开局容量减半（主背包除外），可拖垃圾（junk）逐格升级恢复\n" +
                    "博士夜晚到访，出售食物与水",
                    "A brand-new starting type with its own tab on the start screen. Stranded on the station, survive like Robinson Crusoe:\n" +
                    "Daily 2200 kcal & 2000 ml water; six survival stats: satiety, thirst, health, cleanliness, sleep, social\n\n" +
                    "6 status axes, 20 nodes — from Starving/Parched to Well-Fed/Quenched, each affecting customers, prices, haggling, scavenging and wounds\n" +
                    "Double-click to eat, drink and take medicine; food has calories, quality and decay; dirty water makes you sick\n" +
                    "Three death lines with rescue chances\n" +
                    "Granary: sell +5% after 7 well-fed days; Ascension levels up every 2 days\n" +
                    "8 status customers drop by with special deals; press Z for the survival panel\n" +
                    "Containers start halved: pouches, storage and machine bins (main backpack excluded); drag junk to upgrade back\n" +
                    "The Doctor visits at night, selling food and water");
            }

        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] 点击处理异常: " + ex.Message);
        }
    }

    // Patch NewGameData.GetStartDisplayName：14 → 显示名
    public static void PostfixGetStartDisplayName(Il2Cpp.NewGameData.StartType startType, ref string __result)
    {
        try
        {
            if ((int)startType == NEW_START_TYPE)
                __result = START_NAME;
            // 存档名"一贫如洗"修复：保存时 startType 被合法化成 12，preview.startName=GetStartDisplayName(12)
            // → 该档带鲁滨逊标记（runID）时，12 也返回"空间站鲁滨逊"（原生 12 档无标记，不受影响）
            if ((int)startType == 12 && IsMarkedCurrentRun())
                __result = START_NAME;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] 显示名异常: " + ex.Message);
        }
    }

    // 【诊断】BuildPreviewFromStore：确认 startType=14 的存档槽在读取预览时是否被调用/正常返回
    // 用户实测：存档文件(save_207.es3)和索引都在，但主菜单存档列表不显示该槽。
    // 此 Patch 区分两种情况：①此方法根本没被调用（读取阶段失败/被跳过）②被调用但渲染层丢槽
    public static void PostfixBuildPreviewFromStore(Il2Cpp.PlayerStore store, int slotId, Il2Cpp.SavePreviewData __result)
    {
        try
        {
            // 保存时生成的 preview 若 startType=14（超枚举定义），写进索引后下次主菜单反序列化会抛异常
            // → 槽不显示。此处把索引里的 startType 改为合法值 12（RockBottom），startName 保持"新职业"（GetStartDisplayName(14) 已 Patch）
            if (store != null && __result != null && (int)store.startType == NEW_START_TYPE)
            {
                __result.startType = (Il2Cpp.NewGameData.StartType)12;
                __result.startName = START_NAME; // 预览名直接写死（startName 是序列化字符串，不走 GetStartDisplayName）
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[新职业] BuildPreviewFromStore 异常: " + ex.Message);
        }
    }

    // 【核心修复】SaveGame 前：startType=14（超枚举定义，ES3 反序列化会炸）
    // → 临时写合法值 12 存档，并存 runID 标记；Postfix 恢复 14（内存）
    public static void PrefixSaveGame(Il2Cpp.PlayerStore __instance)
    {
        try
        {
            if (__instance != null && (int)__instance.startType == NEW_START_TYPE)
            {
                string runId = __instance.runID ?? "";
                UnityEngine.PlayerPrefs.SetString(MarkerKey(runId), "1");  // per-runID（多档不互覆盖）
                UnityEngine.PlayerPrefs.SetString(NEW_START_MARKER_KEY, runId); // 老 key 同步（旧版判定兼容）
                UnityEngine.PlayerPrefs.Save(); // 09-20 B3：防强退丢标记（对比 LuckScoutPerk/PerkStatePersistence）
                __instance.startType = (Il2Cpp.NewGameData.StartType)12;
            }
        }
        catch (Exception ex) { Core.LogMsg("[新职业] PrefixSaveGame 异常: " + ex.Message); }
    }

    // 当前存档是否带鲁滨逊标记（runID 匹配——新 key 优先，老 key 兜底）
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

    // 【核心修复】LoadGame 后：若存档是 12（被我们合法化的新职业档）且 runID 标记匹配 → 恢复 14
    // 09-22 修正：v1.2.2 发布清理(098c49e)误删恢复逻辑只留空壳 → 旧档读档 startType 不恢复 → IsActive false → 状态栏消失
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
