using System;
using UnityEngine;
using Il2Cpp;
using Il2CppTMPro;

namespace WagePerks;

/// <summary>
/// 面板助手（10-07 用户需求：蛙娘/鲁滨逊面板 UI 能放大缩小 + 数值安全线分色 + 位置记忆）。
/// 机制实锤（拆包）：原版 CustomUIDebugWindows.ApplyScale(CustomUIWindow) 对 window.Rect 设
/// Transform.localScale=Vector3(uiScale,uiScale,uiScale)（CustomUIDebugWindows.txt:2097/:2246）——
/// localScale 作用于窗口根 RectTransform，ContentRoot 与全部子元素跟随缩放。
/// 颜色：CustomUIElement 暴露 text（TMP）+ progressBar.fillImage（Image），GetElement(windowId,elementId) 可取（CustomUIManager.cs:246）。
/// 位置：窗口 Rect.anchoredPosition（拖拽柄移动），PlayerPrefs 持久化。
/// 注意：面板每次刷新都是 CloseWindow+CreateWindow（localScale/位置重置）→ 创建后必须调 ApplySavedScale 重放。
/// </summary>
internal static class PanelZoomHelper
{
    private const float MIN_SCALE = 0.6f;
    private const float MAX_SCALE = 2.0f;
    private const float STEP_UP = 1.1f;

    private static string Key(string windowId) => "wp_panel_scale_" + windowId;
    private static string KeyX(string windowId) => "wp_panel_posx_" + windowId;
    private static string KeyY(string windowId) => "wp_panel_posy_" + windowId;

    // 安全线分色：≥2/3 绿 / ≥1/3 黄 / <1/3 红（10-07 用户拍板：数值按安全线变色）
    private static readonly Color ColorGood = new Color(0.24f, 0.72f, 0.34f);
    private static readonly Color ColorWarn = new Color(0.85f, 0.62f, 0.08f);
    private static readonly Color ColorBad = new Color(0.82f, 0.24f, 0.24f);

    /// <summary>面板创建成功后调用：重放上次缩放值 + 位置（面板重建会丢 localScale/anchoredPosition）</summary>
    internal static void ApplySavedScale(string windowId)
    {
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            var w = mgr.GetWindow(windowId);
            if (w == null || w.Rect == null) return;
            float s = PlayerPrefs.GetFloat(Key(windowId), 1f);
            if (s >= MIN_SCALE && s <= MAX_SCALE && Mathf.Abs(s - 1f) > 0.001f)
            {
                w.Rect.localScale = new Vector3(s, s, s);
            }
            if (PlayerPrefs.HasKey(KeyX(windowId)) && PlayerPrefs.HasKey(KeyY(windowId)))
            {
                var rt = w.Rect;
                rt.anchoredPosition = new Vector2(PlayerPrefs.GetFloat(KeyX(windowId)), PlayerPrefs.GetFloat(KeyY(windowId)));
            }
        }
        catch (Exception ex) { Core.LogMsg("[PanelZoom] ApplySavedScale 异常: " + ex.Message); }
    }

    /// <summary>每帧调用（InputActionManager.Update Postfix 链）：hover 面板窗口 + 滚轮 → 缩放并持久化；窗口打开时检测拖拽位置变化并持久化</summary>
    internal static void FrameUpdate()
    {
        try
        {
            float scroll = Input.mouseScrollDelta.y;
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            Vector2 mouse = Input.mousePosition;
            CheckWindow(mgr, "wage_girl_panel", mouse, scroll);
            CheckWindow(mgr, "rc_status", mouse, scroll);
        }
        catch (Exception ex) { Core.LogMsg("[PanelZoom] FrameUpdate 异常: " + ex.Message); }
    }

    /// <summary>数值分色：progressBar 元素（elementId）与 label 元素（elementId+"_l"）按归一化值上色。构建完成后调用。</summary>
    internal static void ApplyStatusColors(string windowId, params (string elementId, float normalized)[] items)
    {
        if (items == null || items.Length == 0) return;
        try
        {
            var mgr = Il2Cpp.CustomUIManager.Instance;
            if (mgr == null) return;
            foreach (var (elementId, normalized) in items)
            {
                Color c = normalized >= 0.66f ? ColorGood : (normalized >= 0.33f ? ColorWarn : ColorBad);
                try
                {
                    var bar = mgr.GetElement(windowId, elementId);
                    if (bar != null && bar.progressBar != null && bar.progressBar.fillImage != null)
                    {
                        bar.progressBar.fillImage.color = c;
                    }
                }
                catch { }
                try
                {
                    var lbl = mgr.GetElement(windowId, elementId + "_l");
                    if (lbl != null && lbl.text != null)
                    {
                        lbl.text.color = c;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex) { Core.LogMsg("[PanelZoom] ApplyStatusColors 异常: " + ex.Message); }
    }

    private static void CheckWindow(Il2Cpp.CustomUIManager mgr, string windowId, Vector2 mouse, float scroll)
    {
        try
        {
            if (!mgr.IsOpen(windowId)) return;
            var w = mgr.GetWindow(windowId);
            if (w == null || w.Rect == null) return;
            // 位置记忆：检测拖拽位置变化（值变化才写，避免每帧写 PlayerPrefs）
            var pos = w.Rect.anchoredPosition;
            if (!PlayerPrefs.HasKey(KeyX(windowId)) || Mathf.Abs(PlayerPrefs.GetFloat(KeyX(windowId)) - pos.x) > 0.5f
                || Mathf.Abs(PlayerPrefs.GetFloat(KeyY(windowId)) - pos.y) > 0.5f)
            {
                PlayerPrefs.SetFloat(KeyX(windowId), pos.x);
                PlayerPrefs.SetFloat(KeyY(windowId), pos.y);
            }
            // 缩放：hover + 滚轮
            if (Mathf.Approximately(scroll, 0f)) return;
            // overlay 层 Canvas 渲染模式：ScreenSpaceOverlay 传 null camera；若为 ScreenSpaceCamera 此处需传相机（先按 null 试，实测可调）
            if (!RectTransformUtility.RectangleContainsScreenPoint(w.Rect, mouse, null)) return;
            float s = w.Rect.localScale.x;
            s = scroll > 0f ? s * STEP_UP : s / STEP_UP;
            s = Mathf.Clamp(s, MIN_SCALE, MAX_SCALE);
            w.Rect.localScale = new Vector3(s, s, s);
            PlayerPrefs.SetFloat(Key(windowId), s);
        }
        catch { /* 单窗口异常不影响其他窗口 */ }
    }

    // §4（审阅 20261018）：指南滚动条目高度——BeginScroll 不建 ContentSizeFitter（Q2 实锤），
    //   BeginColumn 只建 VLG → 条目必须撑开 content 高度（XIAOWO 同款）。
    //   高度取 TMP preferredHeight（文本固有高），比固定高度更稳（长/短文本不裁切、不留大空隙）。
    // 10-18 二次修复（用户实测截图：12 条文本全部叠在一起、不能滚）：
    //   只设 LayoutElement.preferredHeight 不够——列容器 VLG 若 childControlHeight=false，
    //   VLG 按子项 rect 实际高度参与布局，preferredHeight 被忽略 → 每条仍是固定行高（约39px），
    //   多行正文溢出压住下一条，且 12×39≈468 < 视口470 → content 不超过视口，永远滚不动。
    //   修法：① 先按目标宽度算 TMP 固有高（GetPreferredValues 带显式宽度，避免用过期宽度）；
    //         ② 同时写 LayoutElement.preferredHeight 与 rect 实际 sizeDelta.y（两种 VLG 模式都覆盖）。
    internal static void SizeGuideLabel(Il2Cpp.CustomUIBuilder b, float width)
    {
        try
        {
            var last = b.Last;
            if (last == null || last.text == null) return;
            var tmp = last.text as TMP_Text;
            if (tmp == null) return;
            float h = 24f;
            try
            {
                string txt = tmp.text ?? "";
                var vals = tmp.GetPreferredValues(txt, width, 0f); // 按目标宽度算换行后固有高
                if (vals.y > 0f) h = vals.y;
            }
            catch
            {
                try { h = tmp.preferredHeight; } catch { }
            }
            if (h < 24f) h = 24f;
            last.SetPreferredSize(width, h + 6f); // LayoutElement（childControlHeight=true 时生效）
            try
            {
                var go = last.root;
                if (go != null)
                {
                    var rt = go.GetComponent<RectTransform>();
                    if (rt != null) rt.sizeDelta = new Vector2(width, h + 6f); // rect 实高（childControlHeight=false 时生效）
                }
            }
            catch { }
        }
        catch { }
    }

    // 10-18：指南末尾收口——把滚动 content 高度按「子项实高之和 + 间距」定死，并补 ContentSizeFitter。
    //   BeginScroll 原生不建 CFS，content 高度不随子项增长 → 内容再多也滚不动（用户实测「没有实现滚动」）。
    internal static void FinishGuideScroll(Il2Cpp.CustomUIBuilder b, float spacing = 4f)
    {
        try
        {
            var last = b.Last;
            if (last == null || last.root == null) return;
            var sr = last.root.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (sr == null || sr.content == null) return;
            var content = sr.content;
            int n = content.childCount;
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                var ch = content.GetChild(i);
                if (ch == null) continue;
                var crt = ch.GetComponent<RectTransform>();
                if (crt == null) continue;
                total += crt.sizeDelta.y;
            }
            if (n > 1) total += spacing * (n - 1);
            if (total < 1f) return;
            content.sizeDelta = new Vector2(content.sizeDelta.x, total);
            if (content.GetComponent<UnityEngine.UI.ContentSizeFitter>() == null)
            {
                var cfs = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                cfs.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            }
            Core.LogMsg("[指南] 滚动收口: 子项 " + n + " 个, content 高=" + total);
        }
        catch (Exception ex) { Core.LogMsg("[指南] FinishGuideScroll 异常: " + ex.Message); }
    }
}
