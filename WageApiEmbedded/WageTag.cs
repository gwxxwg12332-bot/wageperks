using System;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WageAPI;

/// <summary>
/// WageTag 权威 tag 封装（2026-10-06，tag 权威封装候选第一步。WP+WS 两份同源，与 WageItemGrant 同模式）。
/// 统一 tag 读/写/判定入口，替代裸读手拼链。新代码一律走本入口；存量按 bug 驱动迁移（不盲迁 152 处裸读）。
///
/// 【机制实锤（cheatsheet mod-dev-cheatsheet.md:36）】
/// GameItem 双 TagSystem：state(0x1B0)=模板态 / modifiedState(0x1B8)=实例态。
/// - 读：GetTagReadonly/IsTag 读 modifiedState（实例态）——权威
/// - 写：EnableTag/ModifyTag/DisableTag 改 state → ValidateShapeState → SyncModifiedState 自动写回 modifiedState（原版自带链）——写后立即可读
/// 本封装只包装原版 API（try/catch + 默认值 + 语义注释），不改机制。
///
/// 【禁裸读清单（已知坑，别踩）】
/// ① CALORIE_VALUE_TAG / CALORIE：直读不对（Top10#2）→ 统一走 RobinCrusoePerk.GetCalLeft()/ItemHelper.GetCalLeft()
/// ② STOLEN_VALUE_INT：赃物热度——用 StolenHelper.SetHeat/GetHeat 封装，勿裸读
/// ③ 聚合 tag（TOTAL_PERCENTAGE_*）：由 ModuleHelper.ComputeModuleEffect 写入/重算，只读不改（改=被覆盖）
/// ④ 场景位置机器 tags 不随档（09-14 实锤）——跨档状态用 WageSaveStore 双写，勿依赖机器 tag
/// </summary>
public static class WageTag
{
    /// <summary>权威读 int（实例态 modifiedState；无 tag/异常→fallback）。</summary>
    public static int GetInt(GameItem item, string tag, int fallback = 0)
    {
        try
        {
            var t = item.GetTagReadonly(tag);
            if (t != null) return t.valueInt;
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTag] GetInt 异常 " + tag + ": " + ex.Message); }
        return fallback;
    }

    /// <summary>权威读 float（动物属性 ANIMAL_*_TAG 等；无 tag/异常→fallback）。</summary>
    public static float GetFloat(GameItem item, string tag, float fallback = 0f)
    {
        try
        {
            var t = item.GetTagReadonly(tag);
            if (t != null) return t.GetFloat();
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTag] GetFloat 异常 " + tag + ": " + ex.Message); }
        return fallback;
    }

    /// <summary>权威判定存在性（IsTag 读实例态 modifiedState）。</summary>
    public static bool Has(GameItem item, string tag)
    {
        try { return item != null && item.IsTag(tag); } catch { return false; }
    }

    /// <summary>权威写 int（无 tag 先 EnableTag 存在性 → ModifyTag SetInt；原版自动 SyncModifiedState 同步实例态）。</summary>
    public static void SetInt(GameItem item, string tag, int value)
    {
        try
        {
            if (item == null) return;
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetInt(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTag] SetInt 异常 " + tag + ": " + ex.Message); }
    }

    /// <summary>权威写 float（同上）。</summary>
    public static void SetFloat(GameItem item, string tag, float value)
    {
        try
        {
            if (item == null) return;
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetFloat(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch (Exception ex) { Core.Log?.Msg("[WageTag] SetFloat 异常 " + tag + ": " + ex.Message); }
    }

    /// <summary>权威增减 int（读→加→写）。</summary>
    public static void AddInt(GameItem item, string tag, int delta)
    {
        SetInt(item, tag, GetInt(item, tag) + delta);
    }

    /// <summary>权威开启 tag。</summary>
    public static void Enable(GameItem item, string tag)
    {
        try { if (item != null && !item.IsTag(tag)) item.EnableTag(tag, true); } catch (Exception ex) { Core.Log?.Msg("[WageTag] Enable 异常 " + tag + ": " + ex.Message); }
    }

    /// <summary>权威关闭 tag（原版 DisableTag 自带 ValidateShapeState+SyncModifiedState）。</summary>
    public static void Disable(GameItem item, string tag)
    {
        try { if (item != null && item.IsTag(tag)) item.DisableTag(tag, true); } catch (Exception ex) { Core.Log?.Msg("[WageTag] Disable 异常 " + tag + ": " + ex.Message); }
    }
}
