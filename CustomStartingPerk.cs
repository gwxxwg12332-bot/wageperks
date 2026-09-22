using System;
using System.Reflection;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace JacksonPerks;

// 阶段2 生命周期驱动层（2026-09-23）：
// 本类是所有"特性"的统一生命周期载体。**不要**再新建第二个特性基类
// （历史上曾有 WagePerkBase，零子类且 IsActive 读幻影键，已删除）。
//
// 四个生命周期钩子 + 权威挂点（均有实测/拆包证据）：
//   OnNewGame()       ← PlayerStore.StartNewGame  Postfix。**禁止**挂 GameMaster.NewGame（实测从不触发）
//   OnDayStart()      ← StoreEventManager.OnDayStart Postfix，唯一每日权威信号。
//                        **不要**挂 StoreClientManager.OnNewDay（8 字节自增计数，且会被静默跳过）
//   OnSaveGame()      ← PlayerStore.SaveGame Postfix，priority 高于 WageSaveStore 的 0（先写内存，最后统一落盘）
//   OnGameLoaded()    ← WageSaveStore.LoadIfPending() 数据就绪之后。**不能**挂 LoadGame Postfix（容器未就绪）
//
// IsActive() 故意**不**放在基类：各特性判定来源不同（多数走 Core.PerkActive，
// 但 RobinCrusoePerk 走 ps.startType、WageGirlPerk 走 WagePowerPerk），统一会错。
internal abstract class CustomStartingPerk
{
    internal abstract string Id { get; }
    internal abstract string DisplayName { get; }
    internal abstract string Description { get; }
    internal virtual int Cost => 1;
    internal virtual int MaxSlot => 0;
    internal virtual int Type => 0;
    internal virtual string[] IncompatibleIds => Array.Empty<string>();

    internal abstract void OnNewGame();

    /// <summary>每天开始。由 StoreEventManager.OnDayStart 统一驱动。</summary>
    internal virtual void OnDayStart() { }

    /// <summary>存档时。**契约：只允许写内存（WageSaveStore.SetXxx），禁止自己调 Flush()**，
    /// 否则会截断后续特性的写入（统一落盘由 WageSaveStore 的 priority 0 Postfix 收尾）。</summary>
    internal virtual void OnSaveGame() { }

    /// <summary>读档数据就绪后。**契约：必须幂等**，允许被多次调用。</summary>
    internal virtual void OnGameLoaded() { }

    internal StartingPerk Create()
    {
        List<string> val = new List<string>();
        foreach (string text in IncompatibleIds)
        {
            val.Add(text);
        }
        StartingPerk perk = new StartingPerk
        {
            id = Id,
            cost = Cost,
            maxSlot = MaxSlot,
            incompatiblePerks = val
        };
        // 设置 type：0=POSITIVE绿 1=NEGATIVE红 2=NEUTRAL黄（先直接赋值，失败再反射兜底）
        try
        {
            perk.type = (StartingPerk.StartingPerkType)Type;
        }
        catch (Exception typeEx)
        {
            Core.LogMsg("[特性] " + Id + " 直接赋值type失败: " + typeEx.Message);
            try
            {
                var typeField = typeof(StartingPerk).GetField("type", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (typeField != null)
                {
                    typeField.SetValue(perk, Enum.ToObject(typeField.FieldType, Type));
                }

            }
            catch (Exception reflEx)
            {
                Core.LogMsg("[特性] " + Id + " 反射type也失败: " + reflEx.Message);
            }
        }
        return perk;
    }
}

/// <summary>
/// 标记「非可选特性」：是 CustomStartingPerk 子类，但**有意**不登记进 CustomStartingPerks.All。
/// 用途：DEBUG 期漏登记断言据此豁免，避免把"有意排除"误报成"漏登记"。
/// 现有例子：WageGirlPerk（门面特性，IsActive 委托给 WagePowerPerk，并入「蛙哥牛逼」不单独选择）。
/// 反例：RetiredGunsmithPerk 是**已取消**特性——不加本标注，因为断言应该把它报出来提醒清理（阶段6 随死代码删除）。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class NonSelectablePerkAttribute : Attribute { }