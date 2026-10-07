using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace WageSurvival;

// ============================================================
// 配置器入口（10-07 用户拍板：主菜单按钮 A+B 组合，Memos TBEoFrNzZyhUNwWYwn236m）
// 主菜单按钮：克隆 moddingButton → 改名"WageSurvival 配置" → onClick 打开配置器窗口
// 窗口：CustomUIManager 浮动窗口（ws_guide 同款模式）+ MelonPreferences 持久化（SurvivalConfig）
// 开局生效：配置项读时取值（MelonPreferences.Value 实时）→ 新档/读档天然生效，无需额外挂点
// 按钮注入与 tab 克隆并存无冲突（不同字段/对象，ResetAllTab 不影响按钮）
// ============================================================
internal static class SurvivalConfigUI
{
    private const string BTN_NAME = "ws_config_button";
    private const string WIN_ID = "ws_config";

    // 主菜单按钮注入（在 NewStartTypeUI.PostfixAwake 末尾调用——同挂点，场景加载时执行）
    internal static void InjectMainMenuButton(Il2Cpp.MainMenuUIController mc)
    {
        try
        {
            if (mc == null) return;
            if (GameObject.Find(BTN_NAME) != null) return; // 场景级防重（同 tab 克隆模式）

            var btnSrc = GetMenuButton(mc);
            if (btnSrc == null)
            {
                Core.LogMsg("[WS配置] 主菜单按钮源不可用（moddingButton/creditButton 等候选字段均无），跳过注入");
                return;
            }

            var template = btnSrc.gameObject;
            var clone = UnityEngine.Object.Instantiate<GameObject>(template, template.transform.parent);
            clone.name = BTN_NAME;
            // 位置放 moddingButton 之后（克隆体默认在源旁边，SetAsLastSibling 避免遮挡 tab 区）
            clone.transform.SetAsLastSibling();

            // 改名（首个 TMP 文字→"WageSurvival 配置"；失败不阻塞——按钮功能优先）
            ReplaceFirstText(clone.transform, LangHelper.T("WageSurvival 配置", "WS Config"));

            // 清掉克隆体复制的所有原生点击源，只留我们自己的（对齐 tab 克隆模式）
            var btn = clone.GetComponent<Button>();
            if (btn == null) btn = clone.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            var et = clone.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et != null) et.triggers.Clear();
            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(
                () => ShowConfigWindow()));

            Core.LogMsg("[WS配置] 主菜单配置按钮注入完成");
        }
        catch (System.Exception ex) { Core.LogMsg("[WS配置] 按钮注入异常: " + ex.Message); }
    }

    // 10-07 P1-5：双装时 WS 走 ApplyGlobalConsumeOnly（不挂 NewStartTypeUI）→ 配置按钮无人注入——
    // 独立 Awake Postfix 包装（只注入按钮，无 tab 依赖；InjectMainMenuButton 有场景级防重，与 WP 的 tab 克隆并存无冲突）
    internal static void PostfixAwake(Il2Cpp.MainMenuUIController __instance)
    {
        try { InjectMainMenuButton(__instance); }
        catch (System.Exception ex) { Core.LogMsg("[WS配置] PostfixAwake 异常: " + ex.Message); }
    }

    // 取主菜单按钮源：纯反射多候选（moddingButton/creditButton/startGameButton 等——原版=场景预置+public 字段注入）
    // 10-07 P1-5 修：原 Il2CppSystem.Type.GetType("MainMenuUIController, Assembly-CSharp") 字符串反射在 Il2Cpp 下易失败
    //   （Assembly 名/类型解析不匹配）→ 按钮源 null → 跳过注入。改用编译期 typeof(Il2Cpp.MainMenuUIController) 直接 GetField，最稳。
    private static UnityEngine.UI.Button GetMenuButton(Il2Cpp.MainMenuUIController mc)
    {
        try
        {
            var t = typeof(Il2Cpp.MainMenuUIController);
            string[] names = { "moddingButton", "creditButton", "startGameButton", "modsButton", "settingsButton" };
            foreach (var fn in names)
            {
                try
                {
                    var f = t.GetField(fn);
                    if (f != null) { var v = f.GetValue(mc); if (v != null) { var b = v as UnityEngine.UI.Button; if (b != null) return b; } }
                }
                catch { }
                try
                {
                    var p = t.GetProperty(fn);
                    if (p != null && p.GetGetMethod() != null) { var v = p.GetGetMethod().Invoke(mc, null); if (v != null) { var b = v as UnityEngine.UI.Button; if (b != null) return b; } }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    // 替换克隆体上首个 TMP 文字（简化版——tab 克隆的 ReplaceAllText 是 private 且带计数，这里只需改名）
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
                    try
                    {
                        var tmp = c.TryCast<Il2CppTMPro.TextMeshProUGUI>();
                        if (tmp != null) { tmp.text = text; return; }
                    }
                    catch { }
                    try
                    {
                        var cmp = c.TryCast<UnityEngine.UI.Text>();
                        if (cmp != null) { cmp.text = text; return; }
                    }
                    catch { }
                }
            }
            // 子物体递归（按钮文字可能在子节点）
            for (int i = 0; i < root.childCount; i++)
            {
                var ch = root.GetChild(i);
                if (ch != null) ReplaceFirstText(ch, text);
            }
        }
        catch { }
    }

    // ============ 配置器窗口（CustomUIManager 浮动窗口——ws_guide 同款模式） ============
    internal static void ShowConfigWindow()
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            if (mgr.IsOpen(WIN_ID)) mgr.CloseWindow(WIN_ID);
            var b = mgr.CreateWindow(WIN_ID, LangHelper.T("WageSurvival 配置", "WS Config"), "overlay");
            if (b == null) return;
            b.SetSize(440, 460).SetPosition(Vector2.zero);
            try
            {
                var w = mgr.GetWindow(WIN_ID);
                if (w != null)
                {
                    var rt = w.Rect;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
                    rt.anchoredPosition = new Vector2(-16, -180);
                }
            }
            catch (System.Exception ex) { Core.LogMsg("[WS配置] 窗口定位异常: " + ex.Message); }
            b.BeginColumn(4f);

            // ① 机器效率减半（bool 可切换）
            bool eff = SurvivalConfig.EffHalfEnabled;
            b.AddLabel(LangHelper.T("机器效率减半：", "Machine Eff Half: ") + (eff ? LangHelper.T("开", "ON") : LangHelper.T("关", "OFF")), "ws_cfg_eff_hdr");
            b.AddButton(eff ? LangHelper.T("点击关闭减半", "Click: Disable Half") : LangHelper.T("点击开启减半", "Click: Enable Half"),
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { SurvivalConfig.ToggleEffHalf(); ShowConfigWindow(); } catch { } })), "ws_cfg_eff_btn");

            // ② 减半比例（数值项——提示改 cfg）
            b.AddLabel(LangHelper.T("减半比例：", "Half Ratio: ") + SurvivalConfig.EffRatio + LangHelper.T("（0~1，MelonPreferences.cfg 可改）", " (0~1, edit MelonPreferences.cfg)"), "ws_cfg_ratio");

            // ③ 捡漏直觉拾荒加成
            b.AddLabel(LangHelper.T("捡漏拾荒加成：", "LuckScout Scav Bonus: ") + "+" + SurvivalConfig.LuckScoutScavBonus + LangHelper.T("（MelonPreferences.cfg 可改）", " (edit MelonPreferences.cfg)"), "ws_cfg_luck");

            // 提示行
            b.AddLabel(LangHelper.T("提示：配置在 MelonPreferences.cfg 的 [WageSurvival] 段；改了不生效先删配置文件重新生成。", "Config lives in [WageSurvival] of MelonPreferences.cfg; if changes don't apply, delete the cfg to regenerate."), "ws_cfg_hint");

            b.AddButton(LangHelper.T("关闭", "Close"), DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((System.Action)(() => { try { CustomUIManager.Instance.CloseWindow(WIN_ID); } catch { } })), "ws_cfg_close");
            b.End();
            b.Show();
        }
        catch (System.Exception ex) { Core.LogMsg("[WS配置] 窗口异常: " + ex.Message); }
    }
}
