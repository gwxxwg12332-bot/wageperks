using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace WagePerks;

// ============================================================
// 蛙哥物品注册：许可（3级）+ 充电器（3级）
// ============================================================
internal static class WageBrokerItems
{
    // 物品ID
    internal const string PERMIT_1_ID = "wage_permit_1";
    internal const string PERMIT_2_ID = "wage_permit_2";
    internal const string PERMIT_3_ID = "wage_permit_3";
    internal const string CHARGER_1_ID = "wage_charger_1";
    internal const string CHARGER_2_ID = "wage_charger_2";
    internal const string CHARGER_3_ID = "wage_charger_3";

    // 物品工厂缓存
    private static Il2CppSystem.Func<GameItem> _permit1Factory;
    private static Il2CppSystem.Func<GameItem> _permit2Factory;
    private static Il2CppSystem.Func<GameItem> _permit3Factory;
    private static Il2CppSystem.Func<GameItem> _charger1Factory;
    private static Il2CppSystem.Func<GameItem> _charger2Factory;
    private static Il2CppSystem.Func<GameItem> _charger3Factory;

    // 注册到物品目录
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            RegisterOne(dir, PERMIT_1_ID, ref _permit1Factory, CreatePermit1);
            RegisterOne(dir, PERMIT_2_ID, ref _permit2Factory, CreatePermit2);
            RegisterOne(dir, PERMIT_3_ID, ref _permit3Factory, CreatePermit3);
            RegisterOne(dir, CHARGER_1_ID, ref _charger1Factory, CreateCharger1);
            RegisterOne(dir, CHARGER_2_ID, ref _charger2Factory, CreateCharger2);
            RegisterOne(dir, CHARGER_3_ID, ref _charger3Factory, CreateCharger3);
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] 注册异常: " + ex.Message); }
    }

    private static void RegisterOne(ItemDirectory dir, string id, ref Il2CppSystem.Func<GameItem> cache, Func<GameItem> factory)
    {
        try
        {
            if (((Directory<GameItem>)(object)dir).Has(id)) { Core.LogMsg("[蛙哥物品] " + id + " 已存在，跳过注册"); return; }
            if (cache == null)
            {
                System.Func<GameItem> sf = () => factory();
                cache = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)sf);
            }
            bool ok = ((Directory<GameItem>)(object)dir).Add(id, cache);
            Core.LogMsg("[蛙哥物品] " + (ok ? "★ " + id + " 已注册" : "⚠️ " + id + " 注册失败"));
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] 注册 " + id + " 异常: " + ex.Message); }
    }

    // 通用：设置物品基础属性
    private static GameItem CreateBaseItem(string id, string name, string desc, int price, string spriteKey)
    {
        GameItem it = ItemDirectory.CreateEmptyItem(null);
        if (it == null) return null;

        // 设置identifier
        SetField(it, "_identifier_k__BackingField", id);
        SetField(it, "_identifierName_k__BackingField", "TYPE-STRING_" + id);

        // 设置名称和描述
        it.SetName(name);
        it.shortDescription = desc;
        it.longDescription = desc;

        // 价格
        it.unitValue = price;
        it.unitBaseValue = price;

        // shape 2x2（照养蛊机——大物品）
        try { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 2); it.SetShape(gsb.Build()); it.modifiedShape = gsb.Build(); } catch { }

        // sprite
        try { it.SetSprite("custom_atlas", spriteKey); } catch { }

        return it;
    }

    private static void SetField(GameItem it, string fieldName, object value)
    {
        try
        {
            var f = typeof(GameItem).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (f != null) f.SetValue(it, value);
        }
        catch { }
    }

    // ===== 许可（3级）=====
    private static GameItem CreatePermit1()
    {
        try
        {
            var it = CreateBaseItem(PERMIT_1_ID,
                LangHelper.T("蛙哥的许可（一级）", "Wage's Permit (Tier 1)"),
                LangHelper.T("每晚外出次数 +1（可叠加）。拾荒时仍可能受伤。", "+1 night outing per night (stacks). Scavenging may still cause injuries."),
                800, "wage_permit_1_sprite");
            if (it == null) return null;
            try { it.EnableTag("paper", true); } catch { } // 文档属性
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreatePermit1异常: " + ex.Message); return null; }
    }

    private static GameItem CreatePermit2()
    {
        try
        {
            var it = CreateBaseItem(PERMIT_2_ID,
                LangHelper.T("蛙哥的许可（二级）", "Wage's Permit (Tier 2)"),
                LangHelper.T("每晚外出次数 +1（可叠加）。拾荒受伤概率减半。", "+1 night outing per night (stacks). Scavenging injury chance halved."),
                1500, "wage_permit_2_sprite");
            if (it == null) return null;
            try { it.EnableTag("paper", true); } catch { }
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreatePermit2异常: " + ex.Message); return null; }
    }

    private static GameItem CreatePermit3()
    {
        try
        {
            var it = CreateBaseItem(PERMIT_3_ID,
                LangHelper.T("蛙哥的许可（三级）", "Wage's Permit (Tier 3)"),
                LangHelper.T("每晚外出次数 +1（可叠加）。拾荒时完全不会受伤。", "+1 night outing per night (stacks). Complete immunity to scavenging injuries."),
                2500, "wage_permit_3_sprite");
            if (it == null) return null;
            try { it.EnableTag("paper", true); } catch { }
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreatePermit3异常: " + ex.Message); return null; }
    }

    // ===== 充电器（3级）=====
    private static GameItem CreateCharger1()
    {
        try
        {
            var it = CreateBaseItem(CHARGER_1_ID,
                LangHelper.T("蛙哥充电器（一级）", "Wage's Charger (Tier 1)"),
                LangHelper.T("每晚自动给背包所有电池充3点电量（拥有即生效，不消耗）。", "Automatically charges all batteries in your backpack by 3 per night (persistent, not consumed)."),
                500, "wage_charger_1_sprite");
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreateCharger1异常: " + ex.Message); return null; }
    }

    private static GameItem CreateCharger2()
    {
        try
        {
            var it = CreateBaseItem(CHARGER_2_ID,
                LangHelper.T("蛙哥充电器（二级）", "Wage's Charger (Tier 2)"),
                LangHelper.T("每晚自动给背包所有电池充6点电量（拥有即生效，不消耗）。", "Automatically charges all batteries in your backpack by 6 per night (persistent, not consumed)."),
                1000, "wage_charger_2_sprite");
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreateCharger2异常: " + ex.Message); return null; }
    }

    private static GameItem CreateCharger3()
    {
        try
        {
            var it = CreateBaseItem(CHARGER_3_ID,
                LangHelper.T("蛙哥充电器（三级）", "Wage's Charger (Tier 3)"),
                LangHelper.T("每晚自动给背包所有电池充满电（拥有即生效，不消耗）。", "Fully charges all batteries in your backpack every night (persistent, not consumed)."),
                2000, "wage_charger_3_sprite");
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙哥物品] CreateCharger3异常: " + ex.Message); return null; }
    }
}
