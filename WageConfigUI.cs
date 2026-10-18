using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace WagePerks;

// ============================================================
// 10-19 WP 配置菜单（派活单_WP配置菜单实施_20261019）
// 主菜单按钮（wp_config_button，照抄 WS SurvivalConfigUI.cs:23-131 改名）+ 480×640 浮动窗口
// 三段式滚动（BeginScroll 540f → 每条 SizeGuideLabel 430f → FinishGuideScroll 4f → End×2）
// 14 页分页 + 列表/调整页交互 + 落盘（BuildConfig.Cat.SaveToFile）+ 34 条失效项 ⚠ 标注
// 硬约束（设计稿 §1.4/§5.3）：只用 AddLabel/AddButton/BeginScroll/BeginColumn/End；
//   底部导航必须在 End×2 之后（WageBrother.cs:1310 踩坑）；不读写 [WageSurvival] 段；不删 HardMode
// ============================================================
internal static class WageConfigUI
{
    private const string BTN_NAME = "wp_config_button";
    private const string WIN_ID = "wp_config";
    private static int _page = 0;                 // 0-based，共 14 页
    private static string _editingKey = null;     // 调整页当前键（null=列表页）
    private static bool _selfChecked = false;

    // 14 页分组定义（与设计稿 §2.4 分页表一致）
    private static readonly string[][] Pages = new string[][]
    {
        new[] { "A 通用/开局" },                                        // 1: 6
        new[] { "B 拾荒与稀有率" },                                      // 2: 8
        new[] { "C 容器与蛙哥箱" },                                      // 3: 5
        new[] { "D 生存六维（鲁滨逊）" },                                // 4: 23
        new[] { "E 鲁滨逊电话与NPC" },                                   // 5: 9
        new[] { "F NPC 来访" },                                          // 6: 6
        new[] { "G 饮食与洗浴" },                                        // 7: 7
        new[] { "H 蛙娘·六维与好感" },                                   // 8: 10
        new[] { "I 蛙娘·预算", "J 蛙娘·偷窃" },                          // 9: 5+10
        new[] { "K 蛙娘·寄卖" },                                         // 10: 8
        new[] { "L 蛙娘·门路" },                                         // 11: 21
        new[] { "M 蛙娘·销赃" },                                         // 12: 12
        new[] { "N 蛙娘·其它", "O 蛙哥" },                               // 13: 13+5
        new[] { "P 特性·招贼体质", "Q 特性·精神错乱", "R 特性·治安眼线", "S 命运之骰", "T B2 服务与机器", "U 剧情", "V 兼容层" }, // 14: 6+2+1+3+11+11+4
    };

    // ============ 主菜单按钮注入（照抄 WS SurvivalConfigUI.cs:23-131，仅改名+双装 sibling 修正） ============
    internal static void PostfixAwake(Il2Cpp.MainMenuUIController __instance)
    {
        try { InjectMainMenuButton(__instance); }
        catch (Exception ex) { Core.LogMsg("[WP配置] PostfixAwake 异常: " + ex.Message); }
    }

    internal static void InjectMainMenuButton(Il2Cpp.MainMenuUIController mc)
    {
        try
        {
            if (mc == null) return;
            if (GameObject.Find(BTN_NAME) != null) return; // 场景级防重

            var btnSrc = GetMenuButton(mc);
            if (btnSrc == null)
            {
                Core.LogMsg("[WP配置] 主菜单按钮源不可用（moddingButton/creditButton 等候选字段均无），跳过注入");
                return;
            }

            var template = btnSrc.gameObject;
            var clone = UnityEngine.Object.Instantiate<GameObject>(template, template.transform.parent);
            clone.name = BTN_NAME;
            clone.transform.SetAsLastSibling();
            // 双装顺序稳定：WP 按钮排到 WS 按钮之前（设计稿 §2.1——两个都 SetAsLastSibling 会抢位，连续启动顺序不一致）
            try
            {
                var wsBtn = GameObject.Find("ws_config_button");
                if (wsBtn != null) clone.transform.SetSiblingIndex(wsBtn.transform.GetSiblingIndex());
            }
            catch { }

            ReplaceFirstText(clone.transform, LangHelper.T("Wage's Perks 配置", "WP Config"));

            var btn = clone.GetComponent<Button>();
            if (btn == null) btn = clone.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            var et = clone.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et != null) et.triggers.Clear();
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(
                () => ShowConfigWindow()));

            Core.LogMsg("[WP配置] 主菜单配置按钮注入完成");
        }
        catch (Exception ex) { Core.LogMsg("[WP配置] 按钮注入异常: " + ex.Message); }
    }

    private static UnityEngine.UI.Button GetMenuButton(Il2Cpp.MainMenuUIController mc)
    {
        try
        {
            var t = typeof(Il2Cpp.MainMenuUIController);
            string[] names = { "moddingButton", "creditButton", "startGameButton", "modsButton", "settingsButton" };
            foreach (var fn in names)
            {
                try { var f = t.GetField(fn); if (f != null) { var v = f.GetValue(mc); if (v != null) { var b = v as UnityEngine.UI.Button; if (b != null) return b; } } } catch { }
                try { var p = t.GetProperty(fn); if (p != null && p.GetGetMethod() != null) { var v = p.GetGetMethod().Invoke(mc, null); if (v != null) { var b = v as UnityEngine.UI.Button; if (b != null) return b; } } } catch { }
            }
        }
        catch { }
        return null;
    }

    private static void ReplaceFirstText(Transform root, string text)
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
                    try { var tmp = c.TryCast<Il2CppTMPro.TextMeshProUGUI>(); if (tmp != null) { tmp.text = text; return; } } catch { }
                    try { var cmp = c.TryCast<UnityEngine.UI.Text>(); if (cmp != null) { cmp.text = text; return; } } catch { }
                }
            }
            for (int i = 0; i < root.childCount; i++) { var ch = root.GetChild(i); if (ch != null) ReplaceFirstText(ch, text); }
        }
        catch { }
    }

    // ============ 配置窗口（480×640，锚 -16,-200；滚动 540=640-100 页头+底部） ============
    internal static void ShowConfigWindow()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen(WIN_ID)) mgr.CloseWindow(WIN_ID);
            var b = mgr.CreateWindow(WIN_ID, LangHelper.T("Wage's Perks 配置", "WP Config"), "overlay");
            if (b == null) return;
            b.SetSize(480, 640).SetPosition(Vector2.zero);
            try { var w = mgr.GetWindow(WIN_ID); if (w != null) { var rt = w.Rect; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1); rt.anchoredPosition = new Vector2(-16, -200); } } catch { }

            if (!_selfChecked) { _selfChecked = true; WageConfigMeta.SelfCheck(); }

            if (_editingKey != null) DrawAdjustPage(b);
            else DrawListPage(b);

            b.Show();
        }
        catch (Exception ex) { Core.LogMsg("[WP配置] 窗口异常: " + ex.Message); }
    }

    // ============ 列表页 ============
    private static void DrawListPage(Il2Cpp.CustomUIBuilder b)
    {
        var items = PageItems(_page);
        // 页头（滚动区前固定区）
        string groupNames = string.Join(" + ", Pages[_page]);
        b.AddLabel(LangHelper.T("配置 · 第 " + (_page + 1) + "/14 页 · " + groupNames + "（" + items.Count + " 项）",
            "Config · Page " + (_page + 1) + "/14 · " + groupNames + " (" + items.Count + ")"), "wp_hdr");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        // S1 鲁滨逊专属页脚提示（D/E 页）
        if (_page == 3 || _page == 4)
        {
            b.AddLabel(LangHelper.T("鲁滨逊档：本页数值仍生效，但六维衰减/拾荒结算由 WageSurvival 执行。",
                "Robinson save: values on this page still apply; stat decay/scavenging is handled by WageSurvival."), "wp_s1");
            PanelZoomHelper.SizeGuideLabel(b, 430f);
        }

        b.BeginScroll(540f);
        b.BeginColumn(4f);
        foreach (var it in items)
        {
            string disp = (it.IsBroken ? "⚠ " : "") + it.Desc + " = " + CurrentValue(it);
            b.AddLabel(disp, "wp_l_" + it.Key);
            PanelZoomHelper.SizeGuideLabel(b, 430f);
            b.AddButton(ButtonText(it), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => OnItemClick(it))), "wp_b_" + it.Key);
            PanelZoomHelper.SizeGuideLabel(b, 430f);
        }
        PanelZoomHelper.FinishGuideScroll(b, 4f);
        b.End(); // 退 column
        b.End(); // 退 scroll

        // 底部导航（End×2 之后——滚动区外，不用滚动即可点）
        DrawNav(b);
    }

    // ============ 调整页（数值/布尔/字符串单项） ============
    private static void DrawAdjustPage(Il2Cpp.CustomUIBuilder b)
    {
        var it = FindItem(_editingKey);
        if (it == null) { _editingKey = null; DrawListPage(b); return; }

        b.AddLabel(LangHelper.T("调整 · " + it.Key + " · " + it.Desc, "Adjust · " + it.Key + " · " + it.Desc), "wp_a_hdr");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        b.AddLabel(LangHelper.T("当前：" + CurrentValue(it) + "    默认：" + it.Default, "Current: " + CurrentValue(it) + "  Default: " + it.Default), "wp_a_val");
        PanelZoomHelper.SizeGuideLabel(b, 430f);

        if (it.Type == "bool")
        {
            bool cur = ReadBool(it.Key);
            b.AddButton(LangHelper.T(cur ? "点击关闭" : "点击开启", cur ? "Click OFF" : "Click ON"),
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { WriteBool(it.Key, !cur); Save(); _editingKey = null; ShowConfigWindow(); } catch (Exception ex) { Core.LogMsg("[WP配置] bool切换异常: " + ex.Message); } })), "wp_a_toggle");
            PanelZoomHelper.SizeGuideLabel(b, 430f);
        }
        else if (it.Type == "string")
        {
            b.AddLabel(LangHelper.T("字符串配置：请直接编辑 MelonPreferences.cfg 的 [WagesPerks] 段（本菜单不提供输入框）。",
                "String config: edit MelonPreferences.cfg [WagesPerks] section directly (no input box in this menu)."), "wp_a_str");
            PanelZoomHelper.SizeGuideLabel(b, 430f);
        }
        else // int / float
        {
            bool isFloat = it.Type == "float";
            // int: ±1/±10；float: ±0.05/±0.5
            AddStepButton(b, it, isFloat ? -0.5 : -10, isFloat ? "-0.5" : "-10");
            AddStepButton(b, it, isFloat ? -0.05 : -1, isFloat ? "-0.05" : "-1");
            AddStepButton(b, it, isFloat ? 0.05 : 1, isFloat ? "+0.05" : "+1");
            AddStepButton(b, it, isFloat ? 0.5 : 10, isFloat ? "+0.5" : "+10");
        }
        // 恢复默认 + 返回
        b.AddButton(LangHelper.T("恢复默认（" + it.Default + "）", "Reset Default (" + it.Default + ")"),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { WriteDefault(it); Save(); ShowConfigWindow(); } catch (Exception ex) { Core.LogMsg("[WP配置] 恢复默认异常: " + ex.Message); } })), "wp_a_reset");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        b.AddButton(LangHelper.T("← 返回列表", "< Back"),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { _editingKey = null; ShowConfigWindow(); } catch { } })), "wp_a_back");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
    }

    private static void AddStepButton(Il2Cpp.CustomUIBuilder b, WageConfigItem it, double delta, string label)
    {
        b.AddButton(label, DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() =>
        {
            try
            {
                if (it.Type == "float")
                {
                    float cur = ReadFloat(it.Key);
                    WriteFloat(it.Key, (float)(cur + delta));
                }
                else
                {
                    int cur = ReadInt(it.Key);
                    WriteInt(it.Key, cur + (int)delta);
                }
                Save();
                ShowConfigWindow();
            }
            catch (Exception ex) { Core.LogMsg("[WP配置] 数值调整异常: " + ex.Message); }
        })), "wp_a_step_" + it.Key + "_" + label);
        PanelZoomHelper.SizeGuideLabel(b, 430f);
    }

    // ============ 底部导航（滚动区外） ============
    private static void DrawNav(Il2Cpp.CustomUIBuilder b)
    {
        // 上一页（首页置灰=Label 非按钮）
        if (_page > 0)
        {
            b.AddButton(LangHelper.T("◀ 上一页", "< Prev"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { _page--; ShowConfigWindow(); } catch { } })), "wp_nav_prev");
        }
        else
        {
            b.AddLabel(LangHelper.T("◀ 上一页（已是首页）", "< Prev (first page)"), "wp_nav_prev_off");
        }
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        // 下一页（末页置灰）
        if (_page < Pages.Length - 1)
        {
            b.AddButton(LangHelper.T("下一页 ▶", "Next >"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { _page++; ShowConfigWindow(); } catch { } })), "wp_nav_next");
        }
        else
        {
            b.AddLabel(LangHelper.T("下一页 ▶（已是末页）", "Next > (last page)"), "wp_nav_next_off");
        }
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        // 本页恢复默认（仅非失效项）
        b.AddButton(LangHelper.T("本页恢复默认", "Reset This Page"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { ResetPageDefaults(_page); Save(); ShowConfigWindow(); } catch (Exception ex) { Core.LogMsg("[WP配置] 本页恢复默认异常: " + ex.Message); } })), "wp_nav_reset");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
        // 关闭
        b.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { Il2Cpp.CustomUIManager.Instance.CloseWindow(WIN_ID); } catch { } })), "wp_nav_close");
        PanelZoomHelper.SizeGuideLabel(b, 430f);
    }

    // ============ 条目工具 ============
    private static System.Collections.Generic.List<WageConfigItem> PageItems(int page)
    {
        var list = new System.Collections.Generic.List<WageConfigItem>();
        var groups = Pages[page];
        foreach (var it in WageConfigMeta.Items)
        {
            foreach (var g in groups)
            {
                if (it.Group == g) { list.Add(it); break; }
            }
        }
        return list;
    }

    private static WageConfigItem FindItem(string key)
    {
        foreach (var it in WageConfigMeta.Items) if (it.Key == key) return it;
        return null;
    }

    private static string CurrentValue(WageConfigItem it)
    {
        try
        {
            if (it.Type == "bool") return ReadBool(it.Key) ? "开" : "关";
            if (it.Type == "float") return ReadFloat(it.Key).ToString("0.##");
            if (it.Type == "string") { string s = ReadString(it.Key); return s.Length > 26 ? s.Substring(0, 26) + "…" : s; }
            return ReadInt(it.Key).ToString();
        }
        catch { return "?"; }
    }

    private static string ButtonText(WageConfigItem it)
    {
        if (it.IsBroken) return LangHelper.T("[ 不生效 ]", "[ Inactive ]");
        if (it.Type == "bool") return LangHelper.T("[ 开 ]", "[ ON ]"); // 列表页 bool 也进调整页切换（点击弹调整页）
        if (it.Type == "string") return LangHelper.T("[ 只读 · 改 cfg ]", "[ Read-only ]");
        return LangHelper.T("[ 调整 ]", "[ Adjust ]");
    }

    private static void OnItemClick(WageConfigItem it)
    {
        try
        {
            if (it.IsBroken)
            {
                Notify(BrokenReason(it));
                return;
            }
            _editingKey = it.Key; // 所有可调项统一进调整页（bool/数值/string 都走调整页细分）
            ShowConfigWindow();
        }
        catch (Exception ex) { Core.LogMsg("[WP配置] 条目点击异常: " + ex.Message); }
    }

    private static string BrokenReason(WageConfigItem it)
    {
        if (it.Status == "S3") return LangHelper.T("⚠ 已移交 WageSurvival：WP 侧挂点已删，改它无效（" + it.Desc + "）", "Moved to WageSurvival: WP hook removed, editing has no effect");
        if (it.Status == "S4") return LangHelper.T("⚠ 常量空转：声明存在但无任何消费代码，改它无效（" + it.Desc + "）", "Dead constant: declared but never consumed");
        if (it.Status == "S4b") return LangHelper.T("⚠ 仅显示：数值只影响文案，实际结算由 WageSurvival 执行（" + it.Desc + "）", "Display only: affects tooltip text; real calc by WageSurvival");
        return LangHelper.T("⚠ 死配置：代码中无引用，改它无效（" + it.Desc + "）", "Dead config: no code reference, editing has no effect");
    }

    private static void ResetPageDefaults(int page)
    {
        foreach (var it in PageItems(page))
        {
            if (it.IsBroken) continue; // 失效项不动
            try { WriteDefault(it); } catch { }
        }
    }

    private static void WriteDefault(WageConfigItem it)
    {
        if (it.Type == "bool") WriteBool(it.Key, it.Default == "true");
        else if (it.Type == "float") WriteFloat(it.Key, float.Parse(it.Default, System.Globalization.CultureInfo.InvariantCulture));
        else if (it.Type == "string") WriteString(it.Key, it.Default);
        else WriteInt(it.Key, int.Parse(it.Default));
    }

    private static void Notify(string msg)
    {
        try { var su = Il2Cpp.StoreUIManager.Instance; if (su != null) su.Notify(msg, "green"); } catch { }
    }

    private static void Save()
    {
        try { BuildConfig.Cat?.SaveToFile(false); } catch (Exception ex) { Core.LogMsg("[WP配置] 落盘异常: " + ex.Message); }
    }

    // ============ 值读写（MelonPreferences，键必须在 InitPrefs CreateEntry 过） ============
    private static int ReadInt(string key) { try { return MelonPreferences.GetEntryValue<int>("WagesPerks", key); } catch { return 0; } }
    private static bool ReadBool(string key) { try { return MelonPreferences.GetEntryValue<bool>("WagesPerks", key); } catch { return false; } }
    private static float ReadFloat(string key) { try { return MelonPreferences.GetEntryValue<float>("WagesPerks", key); } catch { return 0f; } }
    private static string ReadString(string key) { try { return MelonPreferences.GetEntryValue<string>("WagesPerks", key) ?? ""; } catch { return ""; } }
    private static void WriteInt(string key, int v) { try { MelonPreferences.SetEntryValue("WagesPerks", key, v); } catch (Exception ex) { Core.LogMsg("[WP配置] 写值异常: " + ex.Message); } }
    private static void WriteBool(string key, bool v) { try { MelonPreferences.SetEntryValue("WagesPerks", key, v); } catch (Exception ex) { Core.LogMsg("[WP配置] 写值异常: " + ex.Message); } }
    private static void WriteFloat(string key, float v) { try { MelonPreferences.SetEntryValue("WagesPerks", key, v); } catch (Exception ex) { Core.LogMsg("[WP配置] 写值异常: " + ex.Message); } }
    private static void WriteString(string key, string v) { try { MelonPreferences.SetEntryValue("WagesPerks", key, v); } catch (Exception ex) { Core.LogMsg("[WP配置] 写值异常: " + ex.Message); } }
}
