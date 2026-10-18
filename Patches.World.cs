using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Reflection;
using Il2CppTMPro;
using WagePerks;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WagePerks;

internal static partial class Patches
{


	public static void PostfixInitDirectory(ItemDirectory __instance)
	{
		try
		{
			CustomStorageContainer.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[自定义储物箱] PostfixInitDirectory: " + ex.Message);
		}
		try
		{
			LuckScoutBackpackUpgrade.RegisterToDirectory(__instance);
		}
		catch (System.Exception ex2)
		{
			Core.LogMsg("[虚空珠] PostfixInitDirectory: " + ex2.Message);
		}
		try
		{
			GuMachineSystem.RegisterToDirectory(__instance);
			WageGirlSystem.RegisterToDirectory(__instance); // 09-21 蛙娘实体
		}
		catch (System.Exception ex3)
		{
			Core.LogMsg("[养蛊机] PostfixInitDirectory: " + ex3.Message);
		}
		try
		{
			WageBrokerItems.RegisterToDirectory(__instance);
			WageBrother.RegisterCard(__instance); // 10-03 服务卡进物品库（PS_DebugTool可调出真卡）
			WageBrother.RegisterOrderTicket(__instance); // 10-16 P1-5.2 点货签进物品库
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[蛙哥物品] PostfixInitDirectory: " + ex4.Message);
		}
	}

// 10-19 P3 A组#10 注释作废：贸易表·未挂（派活单§四；未注册=现状未生效，保持；恢复功能需补注册）
	public static void PostfixTradeSheetFoundryShop(object __result)
	{
		DrJacksonFriendPerk.FoundryShopPatch.Postfix(__result);
	}

// 10-19 P3 A组#11 注释作废：贸易表·未挂（派活单§四；未注册=现状未生效，保持；恢复功能需补注册）
	public static void PostfixTradeSheetEnergyFarmShop(object __result)
	{
		DrJacksonFriendPerk.EnergyFarmShopPatch.Postfix(__result);
	}

// 10-19 P3 A组#12 注释作废：贸易表·未挂（派活单§四；未注册=现状未生效，保持；恢复功能需补注册）
	public static void PrefixEmporiumShowAfterhourInv(EmporiumEntry __instance)
	{
		DrJacksonFriendPerk.EmporiumEntryShowAfterhourPatch.Prefix(__instance);
	}

	// 特性意图：禁用化学瓶创建（防刷/平衡性）。保留短路但加注释说明理由——第三方同挂会被跳过，
	// 若未来有mod依赖化学瓶创建需在此加判定。
	public static bool PrefixCreateAcidBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static bool PrefixCreateBaseBottle(ref GameItem __result)
	{
		__result = null;
		return false;
	}

	public static void PostfixStartOfDayOpenUI(StartOfDayUIManager __instance)
	{
		// 10-18 追加：入口日志——定案"面板实际打开时 Postfix 是否触发"（此前玩家反馈与日志对不上，怀疑测的是旧版）
		try { Core.LogMsg("[夜间报告] 步骤0：OpenUI Postfix 触发, contentGroupObject=" + (__instance == null ? "null" : (__instance.contentGroupObject == null ? "cg=null" : __instance.contentGroupObject.name)) + ", queue=" + Core.NightReportQueue.Count); } catch { }
		TryAppendNightReport(__instance); // §1-A：不消费队列（消费权归「开始营业」），关掉重开可重新注入
		EnsureMorningScroll(__instance); // §1 步骤3：OpenUI Postfix 挂滚动容器（Q1 实锤后启用）
	}

	// 10-18 主菜单面板残留修复（玩家截图实锤：鲁滨逊+蛙娘面板叠主菜单）：
	//   CustomUIManager 窗口跨场景存活——PlayerStore guard 只防"新创建"、不清理"已存在"窗口。
	//   回主菜单（QuitToMenu/OnMainMenu）主动关闭两面板；进档后由 ShowPanel/RefreshStatusPanel 重建。
	public static void ClosePanelsOnMainMenu()
	{
		try
		{
			var mgr = Il2Cpp.CustomUIManager.Instance;
			if (mgr == null) return;
			bool any = false;
			try { if (mgr.IsOpen("rc_status")) { mgr.CloseWindow("rc_status"); any = true; } } catch { }
			try { if (mgr.IsOpen("wage_girl_panel")) { mgr.CloseWindow("wage_girl_panel"); any = true; } } catch { }
			if (any) Core.LogMsg("[主菜单] 面板残留清理：rc_status/wage_girl_panel 已关闭");
		}
		catch { }
	}

	public static void PostfixStartOfDayButtonClicked(StartOfDayUIManager __instance)
	{
		// 10-18 挂点修正（09-40 日志实证：OpenUI Postfix 步骤0 零命中=从不触发 → EnsureMorningScroll 从未执行 → 滚动容器从未挂载，
		//   注入走本挂点/ShowMorningReport 正常。EnsureMorningScroll 幂等（按名查找防重建），改挂到已证明触发的挂点）
		EnsureMorningScroll(__instance);
		// §1-A（第五轮 20261018）：玩家点了「开始营业」才算消费——关掉面板重开仍能重新注入
		if (TryAppendNightReport(__instance)) Core.NightReportQueue.Clear();
	}

	public static void PostfixStartOfDayShowMorningReport(StartOfDayUIManager __instance)
	{
		EnsureMorningScroll(__instance); // 10-18 同上：已证明触发的挂点补挂滚动容器（幂等）
		TryAppendNightReport(__instance); // §1-A：不消费队列
	}

	// §1 步骤1/2（2026-10-18 五项需求）：逐条注入（原生结构=每行一个 TMP），上限 50 条 + 折叠提示。
	// §1-A（第五轮 20261018）：返回 bool——UI 未就绪=false（队列保留）；空队列=true（调用方可安全 Clear）；
	//   成功注入也不 Clear（消费权归「开始营业」ButtonClicked）；注入前清旧行（同一次打开内多 Postfix 去重）。
	private static bool TryAppendNightReport(StartOfDayUIManager __instance)
	{
		try
		{
			if (Core.NightReportQueue.Count == 0)
			{
				// 10-18：queue 空也置顶——原版行也在 content 里，ScrollRect 位置漂移同样发生（玩家截图实锤）
				// 10-18 玩家拍板：去掉测试用文字（AppendDebugTestLines 已删——滚动已闭环，无需测试行）
				try { if (__instance != null && __instance.contentGroupObject != null) { var sr0 = __instance.contentGroupObject.GetComponentInParent<UnityEngine.UI.ScrollRect>(); if (sr0 != null) sr0.verticalNormalizedPosition = 1f; } } catch { }
				return true;
			}
			if (__instance == null || __instance.startOfDayTMPPrefab == null || __instance.contentGroupObject == null)
			{
				Core.LogMsg("[夜间报告] 报告UI未就绪，无法追加（队列保留不丢）"); // 09-20 拆包：Clear 移到判空后——UI 未就绪时不清队列（原清丢内容）
				return false;
			}
			// §1-A：清上一轮注入的行（InitPanel 会 Destroy 子物体，但同一次打开内多次 Postfix 仍需去重）
			try
			{
				var parent = __instance.contentGroupObject.transform;
				var stale = new System.Collections.Generic.List<UnityEngine.GameObject>();
				for (int ci = 0; ci < parent.childCount; ci++)
				{
					var ch = parent.GetChild(ci);
					if (ch == null) continue;
					var cgo = ch.gameObject;
					if (cgo != null && cgo.name != null && cgo.name.StartsWith("wage_night_line_"))
						stale.Add(cgo);
				}
				foreach (var s in stale) { try { UnityEngine.Object.Destroy(s); } catch { } }
			}
			catch { }
			const int MAX_LINES = 50; // §1 步骤1：上限 50 条，超限取最后 50 条
			var lines = new System.Collections.Generic.List<string>();
			try
			{
				var arr = Core.NightReportQueue.ToArray();
				int total = arr.Length;
				int start = total > MAX_LINES ? total - MAX_LINES : 0;
				for (int i = start; i < total; i++) lines.Add(arr[i]);
				if (total > MAX_LINES)
					lines.Add(LangHelper.T("…（另有 " + (total - MAX_LINES) + " 条已折叠）", "… (" + (total - MAX_LINES) + " more lines folded)"));
			}
			catch { }
			// 10-18 去重：收集 content 已有行文本前缀（清旧行后 content 只剩原版 nightLogs 行）。
			//   玩家截图实锤：原版也记蛙娘事件（销赃/物资箱）→ mod 队列再注入同内容 → 同屏重复。前缀 12 字符命中即跳过。
			var existingPrefix = new System.Collections.Generic.HashSet<string>();
			try
			{
				var parent = __instance.contentGroupObject.transform;
				for (int ci = 0; ci < parent.childCount; ci++)
				{
					var ch = parent.GetChild(ci);
					if (ch == null) continue;
					var t = ch.GetComponentInChildren<TextMeshProUGUI>(true);
					if (t != null && !string.IsNullOrEmpty(t.text))
					{
						string k = t.text.Length > 12 ? t.text.Substring(0, 12) : t.text;
						existingPrefix.Add(k);
					}
				}
			}
			catch { }
			// 逐条注入：全部成功返回 true；失败保留（不 Clear，调用方按返回值决定是否消费）
			bool allOk = true;
			foreach (var line in lines)
			{
				bool dup = false;
				try
				{
					string kl = line.Length > 12 ? line.Substring(0, 12) : line;
					dup = existingPrefix.Contains(kl);
				}
				catch { }
				if (dup) { Core.LogMsg("[夜间报告] 跳过重复行(原版已含): " + (line.Length > 20 ? line.Substring(0, 20) + "…" : line)); continue; }
				if (!AppendOneLine(__instance, line)) allOk = false;
			}
			// 10-18 审阅定案：置顶/定高统一走 ResetNightReportScroll（EnsureMorningScroll 尾部调——容器建好后 ScrollRect 才有效）
			// 10-18 玩家拍板：去掉测试用文字（AppendDebugTestLines 已删——滚动已闭环，无需测试行）
			return allOk;
		}
		catch (System.Exception ex4)
		{
			Core.LogMsg("[夜间报告] 追加异常: " + ex4.Message);
			return false;
		}
	}

	// §1 步骤1：注入单条（找到 TMP 组件并成功 set_text 才算成功）。返回 bool。
	private static bool AppendOneLine(StartOfDayUIManager mgr, string line)
	{
		try
		{
			if (mgr == null || mgr.startOfDayTMPPrefab == null || mgr.contentGroupObject == null) return false;
			GameObject gameObject = UnityEngine.Object.Instantiate(mgr.startOfDayTMPPrefab, mgr.contentGroupObject.transform);
			if (!(gameObject != null)) return false;
			gameObject.name = "wage_night_line_" + (mgr.contentGroupObject.transform.childCount - 1); // §1-A：命名（清旧行去重用）
			gameObject.SetActive(value: true);
			// 10-18 拆包实锤（回传 WR8ntMcPF5iHk24sQdYZJq / dvBZysu8obYWHXY3XB6b9u）：
			//   原版 = Instantiate(prefab) → GetComponent<TextMeshProUGUI>()（只查根物体自身，List 泛型约束锁死）→ set_text。
			//   原版行前缀 "-" 由 InitPanel 拼接 → mod 注入必须带 "-"（否则与原版行视觉不一致）。
			//   原版有 null 保护（GetComponent==null 跳过不崩）——对齐。
			try
			{
				var tmpRoot = gameObject.GetComponent<TextMeshProUGUI>();
				if (tmpRoot != null)
				{
					tmpRoot.text = "-" + line;
					Core.LogMsg("[夜间报告] 注入行 OK(原版路径): " + (line.Length > 24 ? line.Substring(0, 24) + "…" : line));
					return true;
				}
				// 兜底1：子物体 TMP（万一 prefab 结构有嵌套）
				var tmps = gameObject.GetComponentsInChildren<TextMeshProUGUI>(true);
				if (tmps != null && tmps.Length > 0)
				{
					tmps[0].text = "-" + line;
					Core.LogMsg("[夜间报告] 注入行 OK(InChildren): " + (line.Length > 24 ? line.Substring(0, 24) + "…" : line) + "（TMP×" + tmps.Length + "）");
					return true;
				}
				// 兜底2：TryCast 全组件扫描（防 il2cpp 绑定缺陷）
				Component[] all = gameObject.GetComponentsInChildren<Component>(true);
				if (all != null)
				{
					foreach (Component c in all)
					{
						if (c == null) continue;
						var tmp = c.TryCast<TextMeshProUGUI>();
						if (tmp == null) continue;
						tmp.text = "-" + line;
						Core.LogMsg("[夜间报告] 注入行 OK(TryCast): " + (line.Length > 24 ? line.Substring(0, 24) + "…" : line));
						return true;
					}
				}
				Core.LogMsg("[夜间报告] 未找到TMP文本组件，无法设置文本（根=null, comps=" + (all == null ? "null" : all.Length.ToString()) + "）");
				return false;
			}
			catch (System.Exception ex2)
			{
				Core.LogMsg("[夜间报告] 强类型设置文本失败: " + ex2.Message);
				return false;
			}
		}
		catch { return false; }
	}

	// §1 步骤3：挂滚动容器（Q1 实锤后启用）
	// Q1 结论：contentGroupObject 每次 InitPanel 被 Destroy 重建子物体 → 滚动容器必须插在 contentGroupObject 父级（子级会被清）；
	//          InitPanel 全段无 SetParent → 父级插入安全（一次性 SetParent，幂等判据防重复）。
	// 判据（幂等）：contentGroupObject.GetComponentInParent<ScrollRect>() != null → 跳过。
	private static void EnsureMorningScroll(StartOfDayUIManager mgr)
	{
		try
		{
			if (mgr == null || mgr.contentGroupObject == null) return;
			var contentGo = mgr.contentGroupObject;
			var contentRt = contentGo.GetComponent<UnityEngine.RectTransform>();
			if (contentRt == null) return;
			// §1-C（第五轮 20261018）：幂等按名查找。注意 Find 只查直接子级——scrollGo 挂在 contentGroupObject 原父级（非 mgr.transform 下）
			//   必须用 origParent.Find，否则每次 OpenUI 都重建一套滚动容器（叠套：行注入新 content、旧容器空转）
			var origParent = contentRt.parent;
			if (origParent == null) return;
			try
			{
				var existTf = origParent.Find("wage_morning_scroll");
				if (existTf != null && existTf.GetComponent<UnityEngine.UI.ScrollRect>() != null) return;
			}
			catch { }
			// 10-18 拆包实锤（回传 g6ZQGtygJcubT2KAdaaqv6）：contentGroupObject 是 stretch 锚点场景字段，sizeDelta 是相对偏移可为负——
			//   用 origSize 直接写 content 会得到负宽（日志实测 -280x-535）→ 行宽塌缩竖排。必须用 SetParent 前的实际 rect 尺寸。
			var realW = contentRt.rect.width;
			var realH = contentRt.rect.height;
			// 0) 记录原内容区几何（ScrollRect 必须落在原内容区大小/位置，避免遮挡原生"开始营业"按钮——§1 验收第4条）
			var origAnchored = contentRt.anchoredPosition;
			var origSize = contentRt.sizeDelta;
			var origAnchorMin = contentRt.anchorMin;
			var origAnchorMax = contentRt.anchorMax;
			var origPivot = contentRt.pivot;
			// 10-18 四次定案追加：offsetMin/offsetMax 是"rect 相对锚框的边界"（不依赖 pivot/anchoredPosition 中心偏移语义）——
			//   scrollGo 顶部对齐原 content 顶部（offsetMax.y=origMax.y），高度=parentH（offsetMin.y=origMax.y-parentH）
			var origOffsetMin = contentRt.offsetMin;
			var origOffsetMax = contentRt.offsetMax;
			// 10-18 追加修复（"无法滚动"P0）：scrollGo 几何不能直接复制 origSize（stretch 偏移可为负）。
			//   stretch 锚点下 rect 尺寸 = 父尺寸 + sizeDelta → origSize.y=-535 且窗口内容区≈440 时 scrollGo rect 高 = 440-535 = -95（负）→
			//   viewport 负高 → RectMask2D 负裁剪 → 滚动区域不存在。换算为实际 rect 尺寸：sizeDelta = (realW-parentW, realH-parentH)
			var parentRt = origParent.GetComponent<UnityEngine.RectTransform>();
			float parentW = 0f, parentH = 0f;
			if (parentRt != null) { parentW = parentRt.rect.width; parentH = parentRt.rect.height; }
			// 1) 新建 ScrollRect 节点（挂回 contentGroupObject 原父级——几何按原父级复制才不偏移）
			var scrollGo = new GameObject("wage_morning_scroll");
			scrollGo.transform.SetParent(origParent, false);
			var sr = scrollGo.AddComponent<UnityEngine.UI.ScrollRect>();
			var scrollRt = scrollGo.GetComponent<UnityEngine.RectTransform>();
			// 1b) 审阅定案（07:29 第三版）：scrollGo 直接复制原 content 的 RectTransform 五元组（anchors/pivot/sizeDelta/anchoredPosition）。
			//   parentH=992 是父容器（含页签栏/底栏），realH=456.6 才是原版夜报真正可视区——铺满父级=视口溢出盖页签（07:25 实测截图）。
			//   原 content 就是游戏摆好的夜报区，五元组照抄 → rect 与原 content 逐像素重合，不需要任何 offset 换算
			//   （offset 换算=前面几轮全部翻车的根源；sizeDelta 负值=(1178-1458, 456.6-992)=(-280,-535.35) 是 stretch 语义下的正确表达）。
			try
			{
				scrollRt.anchorMin = origAnchorMin;
				scrollRt.anchorMax = origAnchorMax;
				scrollRt.pivot = origPivot;
				scrollRt.sizeDelta = origSize;
				scrollRt.anchoredPosition = origAnchored;
			}
			catch { }
			// 2) viewport：ScrollRect 直接子节点 + RectMask2D（拉伸填满 scrollGo）
			var vpGo = new GameObject("wage_morning_viewport");
			vpGo.transform.SetParent(scrollGo.transform, false);
			vpGo.AddComponent<UnityEngine.UI.RectMask2D>();
			// 10-18 滚轮命中修复：viewport 必须带 Graphic（Image）作 raycast 靶子——Unity 滚轮事件沿父链找 IScrollHandler，
			//   鼠标悬在行间空白处时若无 Image 则 raycast 无命中 → OnScroll 不触发 → 滚轮无效。透明 Image 不遮挡内容（content 在其上层渲染）
			var vpImg = vpGo.AddComponent<UnityEngine.UI.Image>();
			try { vpImg.color = new UnityEngine.Color(0f, 0f, 0f, 0f); vpImg.raycastTarget = true; } catch { }
			var vpRt = vpGo.GetComponent<UnityEngine.RectTransform>();
			try
			{
				vpRt.anchorMin = new UnityEngine.Vector2(0f, 0f);
				vpRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
				vpRt.offsetMin = UnityEngine.Vector2.zero;
				vpRt.offsetMax = UnityEngine.Vector2.zero;
			}
			catch { }
			// 3) contentGroupObject 移入 viewport——审阅定案（§2.4 改动2）：标准滚动视图配置。
			//   Unity pivot 语义：pivot=(0,0) 是左下角、(0.5,1) 是顶边中心（此前景注释三处全反，已修正）。
			//   锚框=(0,1)-(1,1)=视口顶边横线 → pivot=(0.5,1) 顶边中心 + anchoredPosition=0 → content 顶边贴视口顶，向下延伸；
			//   sizeDelta=(0,parentH) → 宽=视口宽（锚框宽），高=parentH 初始（随后 VLG+CFS 按内容撑高）
			contentGo.transform.SetParent(vpGo.transform, false);
			try
			{
				contentRt.anchorMin = new UnityEngine.Vector2(0f, 1f);
				contentRt.anchorMax = new UnityEngine.Vector2(1f, 1f);
				contentRt.pivot = new UnityEngine.Vector2(0.5f, 1f);
				contentRt.anchoredPosition = UnityEngine.Vector2.zero;
				contentRt.sizeDelta = new UnityEngine.Vector2(0f, realH); // 审阅 07:29：初始=可视区高 realH（非 parentH），随后 CFS 撑到内容高
			}
			catch { }
			// 4) ScrollRect 配置（规格固定参数 + §1-C：viewport 必须赋值——否则 viewport=自身 Rect，与 RectMask2D 所在的 vpRt 不是同一矩形）
			sr.content = contentRt;
			sr.viewport = vpRt;
			sr.vertical = true;
			sr.horizontal = false;
			sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
			sr.scrollSensitivity = 30;
			// 10-18 滚轮诊断（09-51 日志实证根因）：EventTrigger.OnScroll 挂 vpGo 命中大量但 onValueChanged 零——
			//   EventTrigger 实现 IScrollHandler 在 vpGo 抢先消费 Scroll 事件，ExecuteHierarchy 从 vpGo 向上找到第一个
			//   IScrollHandler 即停 → 父级 scrollGo 的 ScrollRect 永远收不到滚轮事件 = 诊断劫持滚动。已删除 EventTrigger。
			//   保留 sr.onValueChanged（挂在 ScrollRect 自身，不影响事件冒泡）：滚动发生即打印 norm 验证。
			try
			{
				sr.onValueChanged.AddListener(Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction<UnityEngine.Vector2>>(NightReportScrollDebug));
				Core.LogMsg("[夜间报告] 滚动诊断已挂（onValueChanged→norm）");
			}
			catch (System.Exception dex) { Core.LogMsg("[夜间报告] 滚动诊断挂载失败: " + dex.Message); }
			// 10-18 几何探针（四次定案辅助）：挂载后打印 scrollGo/vpGo/content 实际 rect+锚点+行位置——空面板据此一次定位
			try
			{
				Core.LogMsg("[夜间报告] 几何探针: parentH=" + parentH + " realW=" + realW + " realH=" + realH
					+ " | scrollGo rect=" + scrollRt.rect + " anchored=" + scrollRt.anchoredPosition + " offsetMin=" + scrollRt.offsetMin + " offsetMax=" + scrollRt.offsetMax
					+ " anchorMin=" + scrollRt.anchorMin + " anchorMax=" + scrollRt.anchorMax + " pivot=" + scrollRt.pivot
					+ " | vpGo rect=" + vpRt.rect + " | content rect=" + contentRt.rect + " anchored=" + contentRt.anchoredPosition + " pivot=" + contentRt.pivot);
				if (contentGo != null && contentGo.transform.childCount > 0)
				{
					var firstLine = contentGo.transform.GetChild(0);
					var rt0 = firstLine.GetComponent<UnityEngine.RectTransform>();
					string geo = "?";
					try { geo = "rect=" + rt0.rect + " sizeDelta=" + rt0.sizeDelta + " pivot=" + rt0.pivot; } catch { }
					string tmpInfo = "?";
					try
					{
						var tmp0 = firstLine.GetComponent<Il2CppTMPro.TextMeshProUGUI>();
						if (tmp0 != null)
							tmpInfo = "fontSize=" + tmp0.fontSize + " autoSize=" + tmp0.enableAutoSizing
								+ " prefW=" + tmp0.preferredWidth + " prefH=" + tmp0.preferredHeight
								+ " wrap=" + tmp0.enableWordWrapping + " len=" + tmp0.text.Length;
					}
					catch { }
					Core.LogMsg("[夜间报告] 行探针: " + firstLine.name + " 世界pos=" + firstLine.position + " 局部pos=" + firstLine.localPosition + " | " + geo + " | " + tmpInfo);
				}
			}
			catch { }
			// 10-18 玩家拍板：取消自绘滚动条（按钮/手柄/槽全删），只用滚轮。
			//   滚轮依赖：ScrollRect 挂在 scrollGo，vpGo 透明 Image(raycastTarget=true) 保证鼠标悬停内容区任意位置
			//   raycast 命中 → 沿父链找 IScrollHandler → OnScroll 触发。按钮/手柄是运行时自绘的易碎 UI（每次布局都翻车），删除根治。
			// 5) contentGroupObject：VLG+CFS（审阅 §2.4 改动3 + 07:09 复核 §2.3/2.4 修正）。
			//   竖排根因=CFS horizontalFit=PreferredSize 正反馈（content 宽被 preferred 缩 → 行宽再缩）。
			//   上次过度修正把 VLG childControlWidth 也关死 → 行宽锁 prefab 744.86（可用宽少 37%）=文字挤在一起。
			//   正确组合：childControlWidth=true（行宽=content 宽 1178）+ horizontalFit=Unconstrained（CFS 不缩宽）——
			//   CFS 端不缩宽则无正反馈回路，放开 VLG 宽度控制安全。
			var vlg = contentGo.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
			if (vlg == null) vlg = contentGo.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
			vlg.childControlWidth = true;   // 行宽=content 宽（恢复原版行宽，防"挤在一起"）
			vlg.childControlHeight = true;   // VLG 把行高写回（preferred 高）
			vlg.childForceExpandWidth = false;
			vlg.childForceExpandHeight = false;
			vlg.spacing = 2f;
			vlg.padding = new UnityEngine.RectOffset(0, 24, 0, 0); // 审阅 07:29：右侧留 24px 给滚动条（防压字）
			var cfs = contentGo.GetComponent<UnityEngine.UI.ContentSizeFitter>();
			if (cfs == null) cfs = contentGo.AddComponent<UnityEngine.UI.ContentSizeFitter>();
			cfs.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained; // ★ 防竖排的关键，保持不变
			cfs.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
			Core.LogMsg("[夜间报告] 步骤3：早报滚动容器已挂（scrollGo rect=" + scrollRt.rect.width.ToString("0.0") + "x" + scrollRt.rect.height.ToString("0.0") + "，content=" + realW.ToString("0.0") + "x" + realH.ToString("0.0") + "）");
			ResetNightReportScroll(sr); // 审阅 §2.4 改动4/5：容器建好+VLG/CFS 配置后统一置顶（ForceUpdateCanvases 后才读到新高度）
		}
		catch (System.Exception ex) { Core.LogMsg("[夜间报告] 步骤3异常: " + ex.Message); }
	}

	// 10-18 滚动诊断回调（具名静态方法——lambda 直接 AddListener 编译 CS1660 转 IntPtr，必须 ConvertDelegate 包方法组）
	private static void NightReportScrollDebug(UnityEngine.Vector2 v)
	{
		try { Core.LogMsg("[夜间报告] onValueChanged: norm=" + v.y.ToString("0.000")); } catch { }
	}

	// 10-18 审阅 §2.4 改动4/5 定案（拆包侧对照放行）：统一置顶入口——替代散落的 3 处置顶 + 手工定高。
	//   verticalNormalizedPosition 的 setter 内部会 UpdateBounds，但 VLG/CFS 的尺寸要等 Canvas.ForceUpdateCanvases
	//   才落地；没有这两行，置顶读到的是旧高度（"看起来写了、实际没生效"典型成因）。
	//   每次行数变化之后调用（EnsureMorningScroll 尾部=容器建好后容器才有效）。10-18 玩家拍板删自绘滚动条 → 无 _nrSr 字段，改传参。
	private static void ResetNightReportScroll(UnityEngine.UI.ScrollRect sr)
	{
		try
		{
			if (sr == null || sr.content == null) return;
			UnityEngine.Canvas.ForceUpdateCanvases();
			UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(sr.content);
			UnityEngine.Canvas.ForceUpdateCanvases();
			sr.verticalNormalizedPosition = 1f;
			Core.LogMsg("[夜间报告] 置顶完成: contentH=" + sr.content.rect.height.ToString("0.0")
				+ " viewH=" + (sr.viewport != null ? sr.viewport.rect.height.ToString("0.0") : "0")
				+ " norm=" + sr.verticalNormalizedPosition.ToString("0.00"));
		}
		catch (System.Exception ex) { Core.LogMsg("[夜间报告] 置顶异常: " + ex.Message); }
	}
// 10-19 P3 A组#13 待确认用途（派活单§四：声望修正·倾向补注册；未注册=现状未生效，拍板后接线或作废）
	public static void PrefixModReputation(ref double modValue)
	{
		try
		{
			if (modValue < 0) modValue = modValue / 2.0; // 精确减半：-3→-1.5、-5→-2.5、-7→-3.5、-10→-5
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[信誉减半] Prefix异常: " + ex.Message);
		}
	}


	// 09-22 制卡降上城区声望根因修复：mod 违禁品跳过"客户曝光"链（ClientExposeFeature）
	// 曝光链（拆包实锤）：PlacedItemForBuying → CanClientExposeAnyFeature → ClientExposeFeature →
	//   CanClientExposeThisFeature → 对话 + StoreReputation.ModReputation(客户faction, -4, true) + ExposeFeature(词条移除)
	// mod 违禁品（wage_ 前缀 / 吞噬融合 CANNIBALISM_VALUE / 电池融合 BREEDER_POWER_SOURCE_ITEM_TAG）
	// 被客户浏览即曝光 → 扣该客户 faction 声望 -4 + 词条划掉。跳过曝光：不扣声望、词条保留、违禁品打标保留。
	public static bool PrefixClientExposeFeature(GameItem gameItem)
	{
		try
		{
			if (gameItem == null) return true;
			string id = "";
			try { id = gameItem.identifier ?? ""; } catch { }
			bool isWage = id.StartsWith("wage_") || gameItem.IsTag("CANNIBALISM_VALUE") || gameItem.IsTag("BREEDER_POWER_SOURCE_ITEM_TAG");
			if (isWage)
			{
				Core.LogMsg("[声望修复] " + id + " 是 mod 物品，跳过客户曝光（不再扣声望/划词条）");
				return false;
			}
		}
		catch (System.Exception ex)
		{
			Core.LogMsg("[声望修复] Prefix异常: " + ex.Message);
		}
		return true;
	}
	// 诊断（用完删）：static ModReputation(String,int,bool) 日志——验证曝光扣声望走 static 版（value=-4）
// 10-19 P3 A组#14 待确认用途（派活单§四：声望修正·倾向补注册；未注册=现状未生效，拍板后接线或作废）
	public static void PrefixModReputationStatic(string factionId, int value)
	{
		try { Core.LogMsg("[声望诊断] ModReputationStatic faction=" + factionId + " value=" + value); } catch { }
	}
}
