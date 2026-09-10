using System;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal static class CustomStartingPerks
{
    internal static readonly CustomStartingPerk[] All = new CustomStartingPerk[]
    {
        new FrogPowerPerk(),
        new DrJacksonFriendPerk(),
        new RetiredGunsmithPerk(),
        new WaterMerchantPerk(),
        new AlcoholMerchantPerk(),
        new LuckScoutPerk(),
        new RiskTakerPerk(),
        new WineLoverPerk(),
        new SmilingTigerPerk(),
        new ThiefMagnetPerk(),
        new BadLuckPerk(),
        new BadReputationPerk(),
        new WildeEvidencePerk(),
        new DestinyDicePerk()
    };

    private static readonly System.Collections.Generic.Dictionary<string, StartingPerk> Created =
        new System.Collections.Generic.Dictionary<string, StartingPerk>();

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
        description = CleanId(custom.Description);
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

    internal static void NotifyNewGame()
    {
        // 不判断 IsPerkActive：NewGame 时 StartingPerk 可能未初始化，判断会返回 false 导致 OnNewGame 不执行。
        foreach (CustomStartingPerk custom in All)
        {
            try { custom.OnNewGame(); } catch (Exception ex) { Core.LogMsg("[特性] OnNewGame异常 " + custom.Id + ": " + ex.Message); }
        }
    }

    internal static void NotifyNewDay()
    {
        foreach (CustomStartingPerk custom in All)
        {
            if (StartingPerk.IsPerkActive(custom.Id))
            {
                custom.OnNewDay();
            }
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
            string cleanDesc = custom.Description.Replace("\0", "").Trim();
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
