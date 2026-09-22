using System;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal static class CustomStartingPerks
{
    // ⚠️ 顺序契约：本数组顺序 == 特性选择界面的显示顺序（EnsurePickerElements 按序遍历生成元素）。
    // 不要"顺手排序"，会打乱玩家看到的界面顺序（硬约束#3）。
    // 本数组同时是白名单：不在其中的子类 = 不可选 + IsActive 恒 false。
    // 有意排除的子类请打 [NonSelectablePerk]（见 WageGirlPerk）。
    internal static readonly CustomStartingPerk[] All = new CustomStartingPerk[]
    {
        new WagePowerPerk(),
        new DrJacksonFriendPerk(),
        new WaterMerchantPerk(),
        new AlcoholMerchantPerk(),
        new LuckScoutPerk(),
        new RiskTakerPerk(),
        new WineLoverPerk(),
        new SmilingTigerPerk(),
        new SmilingFacePerk(),
        new ThiefMagnetPerk(),
        new BadLuckPerk(),
        new BadReputationPerk(),
        new DarkGridInspectorPerk(),
        new WildeEvidencePerk(),
        new DetectivePerk(),
        new HatedByAllPerk(),
        new MadnessPerk(),
        new DestinyDicePerk(),
        new WandererPerk(),
        new InfamousPerk()
    };

    private static readonly System.Collections.Generic.Dictionary<string, StartingPerk> Created =
        new System.Collections.Generic.Dictionary<string, StartingPerk>();

    // 09-22 所有自定义特性描述统一追加的群宣传语（群号 1109707341）
    internal static string CommunityNote => LangHelper.T(
        "\n\n参考了群内网友的热心建议（群号：1109707341）！快来加入，你的建议也有可能被采纳。",
        "\n\nInspired by suggestions from our community (QQ Group: 1109707341)! Join us - your idea could be featured.");

    // 去掉\0字符，用于比较
    private static string CleanId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        return id.Replace("\0", "").Trim();
    }

    internal static CustomStartingPerk Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string cleanId = CleanId(id);
        foreach (CustomStartingPerk custom in All)
        {
            if (CleanId(custom.Id) == cleanId) return custom;
        }
        return null;
    }

    internal static bool TryGetLoc(string id, out string displayName, out string description)
    {
        displayName = "";
        description = "";
        CustomStartingPerk custom = Find(id);
        if (custom == null) return false;
        // 去掉\0字符，确保UI能正常显示
        displayName = CleanId(custom.DisplayName);
        description = CleanId(custom.Description) + CommunityNote;
        return true;
    }

    internal static StartingPerk GetOrCreate(CustomStartingPerk custom)
    {
        if (Created.TryGetValue(custom.Id, out var value) && value != null)
        {
            return value;
        }
        value = custom.Create();
        Created[custom.Id] = value;
        return value;
    }

    internal static void EnsureRegistered()
    {
        AssertAllRegistered(); // DEBUG 期防漏登记（Release 构建自动剔除）
        Created.Clear();  // 09-22 每次清空缓存，确保 MaxSlot 等字段生效
        Il2CppSystem.Collections.Generic.List<StartingPerk> perks = StartingPerkList.Perks;
        if (perks == null) return;

        foreach (CustomStartingPerk custom in All)
        {
            if (IndexOf(perks, custom.Id) < 0)
            {
                perks.Add(GetOrCreate(custom));
                Core.LogMsg("已注册特性: " + custom.Id);
            }
        }
    }

    internal static void EnsurePickerElements(PerkUIController ui)
    {
        EnsureRegistered();
        bool flag = false;
        foreach (CustomStartingPerk custom in All)
        {
            // 对齐 20:29 能选版本：只在可选区找（首次开新档 selectedPerks 为空，等价）
            StartingPerkElement val = FindElement(ui.availablePerks, custom.Id);
            Core.LogMsg("[特性UI] " + custom.Id + " element=" + (val != null ? "找到" : "没找到"));
            if ((UnityEngine.Object)(object)val != (UnityEngine.Object)null)
            {
                ApplyToElement(val, custom);
                continue;
            }
            AddPickerElement(ui, custom);
            flag = true;
        }
        if (flag)
        {
            ui.SortPerkContainer(ui.availablePerks);
        }

    }

    internal static void EnsureElement(StartingPerkElement element)
    {
        if ((UnityEngine.Object)(object)element != (UnityEngine.Object)null)
        {
            CustomStartingPerk custom = Find(element.id);
            if (custom != null)
            {
                ApplyToElement(element, custom);
            }
        }
    }

    // ============================================================
    // 阶段2 统一生命周期驱动
    // ------------------------------------------------------------
    // 所有生命周期都走同一个 Drive() 模板，保证：① 每个特性独立 try/catch（一个崩不会吃掉后面全部）
    // ② 异常日志必带特性 Id（否则 23 个特性里定位不到是谁）③ 门控策略集中可见
    // 挂点对应关系见 CustomStartingPerk.cs 顶部注释。
    // ============================================================

    /// <summary>
    /// 统一驱动模板。gateActive=true 时只驱动已激活特性。
    /// 异常隔离是硬要求：旧版 NotifyNewDay 是裸调，任一特性抛异常会让后续全部静默不执行。
    /// </summary>
    private static void Drive(string hook, Action<CustomStartingPerk> call, bool gateActive)
    {
        foreach (CustomStartingPerk custom in All)
        {
            try
            {
                if (gateActive && !StartingPerk.IsPerkActive(custom.Id)) continue;
                call(custom);
            }
            catch (Exception ex)
            {
                Core.LogMsg("[特性] " + hook + "异常 " + custom.Id + ": " + ex.Message);
            }
        }
    }

    /// <summary>开新档。权威挂点 = PlayerStore.StartNewGame Postfix。</summary>
    internal static void NotifyNewGame()
    {
        _lastDayKey = null; // 新档：清每日去重键（runID 已变，键本就会不同；此处显式化意图）
        // 不判断 IsPerkActive：NewGame 时 StartingPerk 可能未初始化，判断会返回 false 导致 OnNewGame 不执行。
        Drive("OnNewGame", p => p.OnNewGame(), gateActive: false);
    }

    // 每日幂等去重键（见 NotifyDayStart）
    private static string _lastDayKey;

    /// <summary>
    /// 每日去重键 = runID + "|" + 权威 dayCounter。
    /// 拆包依据：OnDayStart 回调时 dayCounter 已 `++`（StoreStation.StartDay ISIL 027`[rbx+0x30]++` → 032 Call OnDayStart），
    /// 所以一律以 dayCounter 为准，**不要**用"本帧是否触发过"（读档补发/自造跳天会误判）。
    /// 取不到时返回 null → 放弃去重（宁可多跑一次也不静默丢失）。
    /// </summary>
    private static string DayKey()
    {
        try
        {
            string runId = "";
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null) runId = ps.runID ?? "";
            return runId + "|" + StoreStation.GetDayCounter();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>每天开始。权威挂点 = StoreEventManager.OnDayStart Postfix。
    /// 同日重复调用会去重（自造跳天工具可能多次触发 BeginDay）。</summary>
    internal static void NotifyDayStart()
    {
        string key = DayKey();
        if (key != null && key == _lastDayKey)
        {
            Core.LogMsg("[特性] OnDayStart 同日重复调用已跳过: " + key);
            return;
        }
        _lastDayKey = key;
        Drive("OnDayStart", p => p.OnDayStart(), gateActive: true);
    }

    /// <summary>存档。挂 PlayerStore.SaveGame Postfix（priority 高于统一落盘的 0）。
    /// 不门控：未激活的特性也可能需要清理自己的残留状态。</summary>
    internal static void NotifySaveGame()
    {
        Drive("OnSaveGame", p => p.OnSaveGame(), gateActive: false);
    }

    /// <summary>读档数据就绪。由 WageSaveStore.LoadIfPending() 成功后驱动（不在 LoadGame Postfix 里）。</summary>
    internal static void NotifyGameLoaded()
    {
        Drive("OnGameLoaded", p => p.OnGameLoaded(), gateActive: false);
    }

    /// <summary>
    /// DEBUG 期防漏登记断言。All 是手工数组，历史上确实漏登记过（RetiredGunsmithPerk），
    /// 而漏登记 = 特性不可选 + IsActive 恒 false，整条特性链路静默死掉。
    /// 用 [Conditional("DEBUG")] 使 Release 构建自动剔除调用点。
    /// 反射只用于此断言，**绝不**用于运行期注册（会打破 All 的顺序契约，见下）。
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void AssertAllRegistered()
    {
        try
        {
            var registered = new System.Collections.Generic.HashSet<Type>();
            foreach (CustomStartingPerk custom in All) registered.Add(custom.GetType());

            Type[] types;
            try { types = typeof(CustomStartingPerk).Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException rtle) { types = rtle.Types; }

            foreach (Type t in types)
            {
                if (t == null || t.IsAbstract || !t.IsSubclassOf(typeof(CustomStartingPerk))) continue;
                if (registered.Contains(t)) continue;
                if (t.GetCustomAttribute<NonSelectablePerkAttribute>() != null) continue;
                Core.LogMsg("[特性] 未登记进 CustomStartingPerks.All（DEBUG 断言）: " + t.Name);
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性] 漏登记断言自身异常: " + ex.Message);
        }
    }

    private static void AddPickerElement(PerkUIController ui, CustomStartingPerk custom)
    {
        GameObject obj = UnityEngine.Object.Instantiate<GameObject>(ui.perkElementPrefab, ui.availablePerks.transform);
        StartingPerkElement component = obj.GetComponent<StartingPerkElement>();
        // 设置id时去掉null字符，保持与StartingPerk.id一致
        component.id = custom.Id.Replace("\0", "").Trim();
        component.isSelected = false;
        ApplyToElement(component, custom);
        obj.SetActive(true);
    }

    private static void ApplyToElement(StartingPerkElement element, CustomStartingPerk custom)
    {

        StartingPerk orCreate = GetOrCreate(custom);
        orCreate.maxSlot = custom.MaxSlot;
        element.perk = orCreate;

        try
        {
            // 直接设置icon属性（StartingPerkElement有icon属性，类型是Image）
            if (PerkIconLoader.HasCustomIcon(custom.Id))
            {
                Sprite icon = PerkIconLoader.GetPerkIcon(custom.Id);
                if (icon != null)
                {
                    try
                    {
                        // 用反射获取icon属性
                        var iconProp = element.GetType().GetProperty("icon",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (iconProp != null)
                        {
                            object iconImage = iconProp.GetValue(element);
                            if (iconImage != null)
                            {
                                var spriteProp = iconImage.GetType().GetProperty("sprite");
                                if (spriteProp != null)
                                {
                                    spriteProp.SetValue(iconImage, icon);
                                    var enabledProp = iconImage.GetType().GetProperty("enabled");
                                    if (enabledProp != null) enabledProp.SetValue(iconImage, true);
                                }
                            }
                            else
                            {
                            }
                        }
                        else
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                        Core.LogMsg("[特性UI] 设置icon失败: " + ex.Message);
                    }
                }
            }

            // 名称/描述直接写 rawName/rawDescription（对齐 XIAOWO，StartingPerk 标准字段）
            // 之前反射找 displayName/name 等字段名全都不存在 → 名称/描述根本没设上
            string cleanName = custom.DisplayName.Replace("\0", "").Trim();
            string cleanDesc = custom.Description.Replace("\0", "").Trim() + CommunityNote;
            orCreate.rawName = cleanName;
            orCreate.rawDescription = cleanDesc;

        }
        catch (Exception ex)
        {
            MelonLogger.Error("[特性UI] 失败: " + ex.Message + "\n" + ex.StackTrace);
        }
    }

    private static StartingPerkElement FindElement(GameObject root, string id)
    {
        string cleanId = id.Replace("\0", "").Trim();
        foreach (StartingPerkElement componentsInChild in root.GetComponentsInChildren<StartingPerkElement>(true))
        {
            if ((UnityEngine.Object)(object)componentsInChild != (UnityEngine.Object)null)
            {
                string cleanElementId = componentsInChild.id != null ? componentsInChild.id.Replace("\0", "").Trim() : "";
                if (cleanElementId == cleanId)
                {
                    return componentsInChild;
                }
            }
        }
        return null;
    }

    private static int IndexOf(Il2CppSystem.Collections.Generic.List<StartingPerk> perks, string id)
    {
        string cleanId = id.Replace("\0", "").Trim();
        for (int i = 0; i < perks.Count; i++)
        {
            if (perks[i] != null)
            {
                string cleanPerkId = perks[i].id != null ? perks[i].id.Replace("\0", "").Trim() : "";
                if (cleanPerkId == cleanId)
                {
                    return i;
                }
            }
        }
        return -1;
    }
}
