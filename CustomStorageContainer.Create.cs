using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace JacksonPerks
{

public static partial class CustomStorageContainer
{
        public static GameItem CreateContainer()
        {
            try
            {

                // 确认_customSprite已创建
                if (_customSprite == null)
                {
                    Core.LogMsg("[自定义储物箱] _customSprite为null，尝试重新创建...");
                    _customSprite = CreateCustomBoxSprite();
                }

                // 1. 创建内部库存窗口（52×10=520格，宽度加7）
                var invWindow = DirectoryUtils.CreateInventoryWindow(3, 3, true); // 容器v2：段0=3x3（拖螺丝升级到52x10）
                PixelWindow contentWindow = invWindow.Item1;
                GameInventory internalInv = invWindow.Item2;

                // 2. 创建空物品
                GameItem container = ItemDirectory.CreateEmptyItem(null);
                if (container == null)
                {
                    Core.LogMsg("[自定义储物箱] CreateEmptyItem失败，用storage_bay兜底");
                    container = DirectoryMaster.Item("storage_bay", true);
                }
                if (container == null)
                {
                    Core.LogMsg("[自定义储物箱] 物品创建失败");
                    return null;
                }

                // 3. 绑定内容窗口（关键！内部库存通过这个窗口关联）
                container.SetContentWindow(contentWindow);

                // 4. 设置内部库存标识
                internalInv.identifier = CONTAINER_ID;
                _lastCreatedInventory = internalInv; // 保存内部库存，供FillWithRandomLockedBoxes使用

                // 5. 设置外观（用你设计的蓝色/青色箱子，32×32像素自动对应2×2 shape）
                try
                {
                    container.SetSpriteAndShape(CUSTOM_ATLAS, CUSTOM_SPRITE_NAME);
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[自定义储物箱] SetSpriteAndShape失败: {ex.Message}");
                }

                // 6. 初始化容器（设置标签、校验委托等）
                try
                {
                    ContainerHelper.InitContainerItem(internalInv, container);
                    
                    // 【关键】只允许"已拥有"物品入库（拦截未拥有 not_purchased 物品）
                    // 用户反馈 bug：客户端交易时未拥有物品能拖进妙妙箱 → 用原版 AllowOnlyOwnedItems 修复
                    // （此前清空委托导致未拥有物品也能入库；自定义委托在 IL2CPP 下类型不匹配会爆红，改回原版 API）
                    try
                    {
                        ContainerHelper.AllowOnlyOwnedItems(internalInv);
                    }
                    catch (Exception ex)
                    {
                        Core.LogMsg($"[自定义储物箱] AllowOnlyOwnedItems 设置失败: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[自定义储物箱] InitContainerItem失败: {ex.Message}");
                    // 兜底：InitContainerItemTagOnly
                    try
                    {
                        ContainerHelper.InitContainerItemTagOnly(container);
                    }
                    catch (Exception ex2)
                    {
                        Core.LogMsg($"[自定义储物箱] 兜底也失败: {ex2.Message}");
                    }
                }

                // 8. 设置容器标签（参考MiniSmugglerBay）
                try
                {
                    container.EnableTag("CONTAINER_TAG");
                    container.EnableTag("CUSTOM_STORAGE_TAG"); // 拆包 09-10：鲁滨逊容器系统排除专用（蛙哥箱子专属标签，防被"拖 junk 宽+1"劫持）
                    container.EnableTag("ITEM_HIDDEN_TAG");
                    container.EnableTag("SYSTEM_TAG");
                    container.EnableTag("SYSTEM_TAG_UTILITY");
                    container.SetGameItemType("STORAGE");
                    // 容器v2：段位标记（0=3x3起步；读档按 wb_stage 重设网格）
                    try { if (!container.IsTag("wb_stage")) container.EnableTag("wb_stage", true); ContainerUpgradeV2.SetTagIntValue(container, "wb_stage", 0); } catch { }
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[自定义储物箱] 设置标签失败: {ex.Message}");
                }

                // 9. 设置名称、描述、价值
                container.SetName(CONTAINER_NAME);
                SetPrivateField(container, "_identifier_k__BackingField", CONTAINER_ID);
                SetPrivateField(container, "_identifierName_k__BackingField", "TYPE-STRING_" + CONTAINER_ID);
                SetPrivateField(container, "_shortDescription_k__BackingField", CONTAINER_DESC);
                SetPrivateField(container, "_longDescription_k__BackingField", CONTAINER_DESC);
                SetPrivateField(container, "_unitValue_k__BackingField", (long)500);
                SetPrivateField(container, "_unitBaseValue_k__BackingField", (long)500);

                // 10. 验证内部库存大小
                try
                {
                    if (internalInv is GameGridInventory gridInv)
                    {
                        // 尝试获取width和height
                        var wProp = gridInv.GetType().GetProperty("width", BindingFlags.Public | BindingFlags.Instance);
                        var hProp = gridInv.GetType().GetProperty("height", BindingFlags.Public | BindingFlags.Instance);
                        if (wProp != null && hProp != null)
                        {
                        }
                        // 调用Validate刷新
                        gridInv.Validate();
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                    Core.LogMsg($"[自定义储物箱] 验证内部库存失败: {ex.Message}");
                }

                return container;
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] 创建失败: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }
        public static GameItem CreateLootCrate(string itemId)
        {
            try
            {
                var map = new System.Collections.Generic.Dictionary<string, string> {
                    { "evidence_box", "LootCrateEvidence" },
                    { "med_box", "LootCrateMedical" },
                    { "sec_box", "LootCrateSecurity" },
                    { "service_box", "LootCrateService" },
                    { "eng_box", "LootCrateEngineering" }
                };
                if (map.TryGetValue(itemId, out string methodName))
                {
                    var t = typeof(Il2Cpp.PreBuiltItemHelper);
                    var mi = t.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (mi != null)
                    {
                        var crate = mi.Invoke(null, null) as GameItem;
                        if (crate != null)
                        {
                            try { Il2Cpp.LockHelper.LockUpContainer(crate); } catch { }
                            return crate;
                        }
                    }
                }
                // 兜底1：DirectoryMaster创建空箱子（可能无内容，但至少有箱子）
                try
                {
                    var gi = DirectoryMaster.Item(itemId, true);
                    if (gi != null)
                    {
                        try { Il2Cpp.LockHelper.LockUpContainer(gi); } catch { }
                        return gi;
                    }
                }
                catch (Exception ex2) { Core.LogMsg($"[蛙哥妙妙箱] 兜底DirectoryMaster创建失败 {itemId}: {ex2.Message}"); }

                // 兜底2：用storage_bay保底（保证永远返回非null）
                try
                {
                    var backup = DirectoryMaster.Item("storage_bay", true);
                    if (backup != null)
                    {
                        try { Il2Cpp.LockHelper.LockUpContainer(backup); } catch { }
                        try { backup.SetName(LangHelper.T("补给箱", "Supply Crate")); } catch { }
                        return backup;
                    }
                }
                catch (Exception ex3) { Core.LogMsg($"[蛙哥妙妙箱] 终极保底storage_bay创建失败: {ex3.Message}"); }

                return null;
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[蛙哥妙妙箱] CreateLootCrate异常 {itemId}: {ex.Message}");
                return null;
            }
        }
        private static GameItem CreateRegisteredContainer()
        {
            GameItem item = CreateContainer();
            if (item != null) return item;
            // 【兼容性】外部工具（NEI Item Browser / Item Manager 等"物品添加类"mod）
            // 在非游戏/非商店场景调用 DirectoryMaster.Item("custom_storage_box") 时，
            // CreateContainer 可能因 UI 上下文未就绪而失败返回 null。
            // 降级返回原生 storage_bay，保证调用方永远拿不到 null（避免崩溃），
            // 但注意：此降级箱子无 contentWindow，仅用于工具预览/占位，不做正式游玩用途。
            try { return DirectoryMaster.Item("storage_bay", true); } catch { }
            return null;
        }
}
}
