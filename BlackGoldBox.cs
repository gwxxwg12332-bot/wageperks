using System;
using System.Collections.Generic;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace WageBlackGold;
/// <summary>
/// 黑金典藏箱（赞助 mod 独立 DLL）——全物品收纳 / 免搜查（ITEM_HIDDEN_TAG）/ 16 组样式切换 / 博士夜晚 2000 上架 / 读档恢复。
/// 2026-10-08 用户拍板：独立新 mod，非 WagePerks/WageSurvival 内容。
/// </summary>
public static class BlackGoldBox
{
    public const string BOX_ID = "wage_blackgold_box";
    public const string BOX_TAG = "WBG_BOX_TAG"; // 10-13 Step4④：黑金独立随档身份（妙妙箱链 IsWageBox/IsBlackGoldBox 排除；读档 identifier/tag 全丢场景兜底）
    public const string ATLAS = "wage_blackgold_atlas";
    public const long BOX_PRICE = 2000;

    // 16 组样式：13 素材 + 3 随意占位（复用 base）
    public static readonly string[] StyleKeys = new string[]
    {
        "blackgold_base", "blackgold_bath", "blackgold_bottle", "blackgold_brewing",
        "blackgold_contraband", "blackgold_food", "blackgold_gears", "blackgold_gem",
        "blackgold_gun", "blackgold_mechanical", "blackgold_medical", "blackgold_tool", "blackgold_weapon",
        "blackgold_base", "blackgold_base", "blackgold_base", // 3 占位槽（待赞助者素材）
    };

    private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static Il2CppSystem.Func<GameItem> _registeredFactory;

    // 素材预加载（Core.OnLateInitializeMelon 调用——防 RenderHandler 懒加载死锁）
    public static void LoadAllSprites()
    {
        foreach (var k in StyleKeys)
        {
            if (_sprites.ContainsKey(k)) continue;
            try
            {
                _sprites[k] = PerkIconLoader.LoadEmbeddedPng(k + ".png");
            }
            catch (Exception ex) { Core.LogMsg("[黑金典藏箱] 素材加载失败 " + k + ": " + ex.Message); }
        }
        Core.LogMsg("[黑金典藏箱] 素材预加载完成: " + _sprites.Count + "/" + StyleKeys.Length);
    }

    public static Sprite GetSprite(string key)
    {
        if (_sprites.TryGetValue(key, out var s)) return s;
        LoadAllSprites();
        _sprites.TryGetValue(key, out s);
        return s;
    }

    // RenderHandler.LoadFromAtlas 拦截（自定义 atlas → 返回嵌入 sprite）
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath == ATLAS)
            {
                var sp = GetSprite(name);
                if (sp != null) { __result = sp; return false; }
            }
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] PrefixLoadFromAtlas 异常: " + ex.Message); }
        return true;
    }

    // 创建容器：20×20=400 格 + 3×3 占地 + 四 tag（CONTAINER/ITEM_HIDDEN/SYSTEM/SYSTEM_TAG_UTILITY）+ 定价 2000
    public static GameItem CreateContainer()
    {
        try
        {
            // 1. 内部库存窗口（400 格）——妙妙箱同款链（CustomStorageContainer.Create.cs:27）
            // 分段 try：游戏早期（如 ItemSpawner 全库探测）原版创建链可能未就绪——未就绪返回 null 不打 NRE
            PixelWindow contentWindow = null;
            GameInventory internalInv = null;
            try
            {
                var invWindow = DirectoryUtils.CreateInventoryWindow(20, 20, true);
                contentWindow = invWindow.Item1;
                internalInv = invWindow.Item2;
            }
            catch (Exception ex) { Core.LogMsg("[黑金典藏箱] CreateInventoryWindow 失败: " + ex.Message + "\n" + ex.StackTrace); }
            if (contentWindow == null || internalInv == null)
            {
                Core.LogMsg("[黑金典藏箱] 创建失败（CreateInventoryWindow 未就绪——游戏早期调用）；真机购买场景再测");
                return null;
            }

            // 2. 空物品创建（storage_bay 兜底）
            GameItem container = null;
            try { container = ItemDirectory.CreateEmptyItem(null); }
            catch (Exception ex) { Core.LogMsg("[黑金典藏箱] CreateEmptyItem 异常: " + ex.Message + "\n" + ex.StackTrace); }
            if (container == null)
            {
                try { container = DirectoryMaster.Item("storage_bay", true); }
                catch (Exception ex) { Core.LogMsg("[黑金典藏箱] storage_bay 兜底异常: " + ex.Message + "\n" + ex.StackTrace); }
            }
            if (container == null) { Core.LogMsg("[黑金典藏箱] 创建失败（CreateEmptyItem + storage_bay 兜底均 null）"); return null; }

            // 绑定内容窗口（关键：内部库存通过该窗口关联）
            container.SetContentWindow(contentWindow);
            internalInv.identifier = BOX_ID;

            // 外观：默认样式（SetSpriteAndShape 会污染 shape——下方强制 3×3）
            try { container.SetSpriteAndShape(ATLAS, "blackgold_base"); } catch (Exception ex) { Core.LogMsg("[黑金典藏箱] SetSpriteAndShape: " + ex.Message); }

            // 容器初始化（标签/校验委托）
            try
            {
                ContainerHelper.InitContainerItem(internalInv, container);
                try { ContainerHelper.AllowOnlyOwnedItems(internalInv); } catch (Exception ex) { Core.LogMsg("[黑金典藏箱] AllowOnlyOwnedItems: " + ex.Message); }
            }
            catch (Exception ex) { Core.LogMsg("[黑金典藏箱] InitContainerItem: " + ex.Message); }

            // 标签：妙妙箱完整同款 5 标签（CustomStorageContainer.Create.cs:95-99）——10-08 三轮定案：
            // 前两轮变量混改（SYSTEM_TAG 与 identifier 属性兜底同时变），NRE 在 ItemContextHandler（identifier 未设的未知物品）
            // 与 MachineBlender（缺 SYSTEM_TAG 走普通 tooltip 分发）间跳——恢复 5 标签齐全 + identifier 设上 = 妙妙箱同款正常路径。
            // SYSTEM_TAG/SYSTEM_TAG_UTILITY 让 tooltip 走系统分支（不 NRE + 面板/属性正常显示）
            try
            {
                container.EnableTag("CONTAINER_TAG");
                container.EnableTag("CUSTOM_STORAGE_TAG");
                container.EnableTag("ITEM_HIDDEN_TAG");
                container.EnableTag("SYSTEM_TAG");
                container.EnableTag("SYSTEM_TAG_UTILITY");
                container.EnableTag(BlackGoldBox.BOX_TAG); // 10-13 Step4④：黑金独立专属 tag——妙妙箱链（IsWageBox/IsUpgradeableContainer/IsBlackGoldBox）按它排除，不依赖 identifier（读档丢场景兜底）
                container.SetGameItemType("STORAGE");
            }
            catch (Exception ex) { Core.LogMsg("[黑金典藏箱] 标签设置: " + ex.Message); }

            // 占地派生：:107 SetSpriteAndShape 已正确派生 3×3（素材 13 张全部 48×48，48/16=3——拆包 10-10 读 PNG 头实证）
            // 【10-10 删除】原强制 3×3 写入（SetShape+modifiedShape）违反铁律 ContainerUpgradeV2.Shape.cs:49-51（shape=场景占地绝不能写；
            //   modifiedShape=拖拽三元组零消费者脏状态——写它=污染下次布局读到的摆位状态）——WBG 是项目唯一违反处，已移除

            // 名称/描述/价值（2000）——10-08 实锤：MachineBlender.CreateTooltip 读 item.identifier（[0x1C0]）判 "blender"，
            // identifier null → 柜台/库存 hover 每帧 NRE + IsBlackGold 判定失败（切换失效）+ tooltip 空（"没有面板"）。
            // 修复：反射（SetField 双保险）+ 原版公开 setter（identifier/identifierName/unitValue）都写，确保非 null。
            container.SetName(WageAPI.LangHelper.T("黑金典藏箱", "Black Gold Vault"));
            SetField(container, "_identifier_k__BackingField", BOX_ID);
            SetField(container, "_identifierName_k__BackingField", "TYPE-STRING_" + BOX_ID);
            string desc = WageAPI.LangHelper.T("赞助者典藏箱：可收纳一切物品，搜查豁免，Shift+双击切换样式。", "Sponsor vault box: stores anything, inspection-proof, Shift+double-click to switch style.");
            SetField(container, "_shortDescription_k__BackingField", desc);
            SetField(container, "_longDescription_k__BackingField", desc);
            SetField(container, "_unitValue_k__BackingField", BOX_PRICE);
            SetField(container, "_unitBaseValue_k__BackingField", BOX_PRICE);
            // 原版公开 setter 兜底（VTable set_identifier/set_identifierName——走原版方法，Il2Cpp 下比反射稳）
            try { container.identifier = BOX_ID; } catch { }
            try { container.identifierName = "TYPE-STRING_" + BOX_ID; } catch { }
            try { container.unitValue = BOX_PRICE; } catch { }
            try { container.unitBaseValue = BOX_PRICE; } catch { }
            // 创建后实证日志：identifier/tag 状态（一次重启验证 NRE 根因闭合）
            try
            {
                string idv = (container.identifier == null) ? "NULL" : container.identifier;
                bool isBlender = false;
                try { isBlender = container.IsGameItemType("blender"); } catch { }
                long uid = 0;
                try { uid = container.uniqueId; } catch { }
                Core.LogMsg("[黑金典藏箱] 创建实证: identifier=" + idv + " uniqueId=" + uid + " IsBlenderType=" + isBlender);
            }
            catch { }

            return container;
        }
        catch (Exception ex)
        {
            Core.LogMsg("[黑金典藏箱] 创建失败: " + ex.Message + "\n" + ex.StackTrace);
            return null;
        }
    }

    // 注册到物品目录（读档原生恢复窗口；CustomStorageContainer.Tools.cs:125-143 先例）
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            if (((Directory<GameItem>)(object)dir).Has(BOX_ID)) return;
            if (_registeredFactory == null)
            {
                System.Func<GameItem> systemFactory = () => CreateContainer();
                _registeredFactory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)systemFactory);
            }
            bool ok = ((Directory<GameItem>)(object)dir).Add(BOX_ID, _registeredFactory);
            Core.LogMsg("[黑金典藏箱] " + (ok ? "★ 已注册" : "⚠️ 注册失败") + " " + BOX_ID + " → " + dir.GetType().Name);
        }
        catch (Exception ex) { Core.LogMsg("[黑金典藏箱] 注册异常: " + ex.Message); }
    }

    // 目录初始化 Postfix（PatchRegistry 第 5 组挂点）
    public static void PostfixInitDirectory(ItemDirectory __instance) => RegisterToDirectory(__instance);

    // 10-08 拆包 AI 回传（请求单 5 项闭合）：backing field 真名 = 尖括号版（dump.cs:57783 `<identifier>k__BackingField`），
    // 下划线版（妙妙箱旧写法）GetField 必 null；identifier=构造器赋值（CreateEmptyItem 参数），建议直接用公开 set_identifier。
    // SetField 双写法（尖括号/下划线都试）+ 属性兜底（TrimStart('_') 后命中公开属性 setter）。
    private static void SetField(GameItem it, string name, object value)
    {
        try
        {
            var rt = it.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var f = rt.GetField(name, flags);
            if (f == null && name.StartsWith("_")) f = rt.GetField("<" + name.TrimStart('_').Replace("_k__BackingField", "") + ">k__BackingField", flags);
            if (f != null) { f.SetValue(it, value); return; }
            var propName = name.Replace("_k__BackingField", "").TrimStart('_');
            var p = rt.GetProperty(propName, flags);
            if (p != null && p.CanWrite) p.SetValue(it, value);
        }
        catch { }
    }
}
