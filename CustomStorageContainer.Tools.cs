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
        public static void Init()
        {
            // 静态构造函数已经创建了sprite，这里不需要做什么
        }
        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            try
            {
                FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                }
                else
                {
                    PropertyInfo prop = obj.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(obj, value);
                    }
                }
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] SetPrivateField失败({fieldName}): {ex.Message}");
            }
        }
        public static void FillWithRandomLockedBoxes(GameItem storageBox, int count)
        {
            try
            {
                
                // 随机上锁箱子（原版正确ID，与PreBuiltItemHelper.LootCrate*对应）
                var boxList = new[]
                {
                    new { BoxId = "sec_box", Name = LangHelper.T("治安战利品箱", "Security Loot Crate") },
                    new { BoxId = "med_box", Name = LangHelper.T("医疗战利品箱", "Medical Loot Crate") },
                    new { BoxId = "eng_box", Name = LangHelper.T("工程战利品箱", "Engineering Loot Crate") },
                    new { BoxId = "service_box", Name = LangHelper.T("服务战利品箱", "Service Loot Crate") },
                    new { BoxId = "evidence_box", Name = LangHelper.T("证物箱", "Evidence Box") }
                };
                
                // 获取箱子的内部库存（优先用CreateContainer中保存的_lastCreatedInventory）
                GameInventory internalInv = _lastCreatedInventory;
                if (internalInv == null)
                {
                    // 兜底：从contentWindow获取
                    try
                    {
                        var contentWindow = storageBox.contentWindow;
                        if (contentWindow != null)
                        {
                            var invProp = contentWindow.GetType().GetProperty("inventory", BindingFlags.Public | BindingFlags.Instance);
                            if (invProp != null)
                            {
                                internalInv = invProp.GetValue(contentWindow) as GameInventory;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Core.LogMsg($"[蛙哥妙妙箱] 从contentWindow获取内部库存失败: {ex.Message}");
                    }
                }
                
                if (internalInv == null)
                {
                    Core.LogMsg("[蛙哥妙妙箱] 内部库存为null，无法填充");
                    return;
                }
                
                // 随机选 count 个不重复箱子（默认1个）
                var random = Core.Rng;
                var selectedBoxes = boxList.OrderBy(x => random.Next()).Take(count).ToArray();
                
                foreach (var box in selectedBoxes)
                {
                    try
                    {
                        // 创建上锁的箱子（用PreBuiltItemHelper.LootCrate*创建带原版内容的箱子）
                        GameItem lockedBox = CreateLootCrate(box.BoxId);
                        if (lockedBox != null)
                        {
                            // 标记为已拥有，避免显示"(未拥有)"
                            try { lockedBox.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                            try { lockedBox.DisableTag("not_purchased", true); } catch { }
                            internalInv.UncheckedAccept(lockedBox);
                        }
                        
                        // 创建指挥卡 cmd_keycard（不再用研发卡 sci_keycard 或其他钥匙卡）
                        GameItem keycard = DirectoryMaster.Item("cmd_keycard", true);
                        if (keycard != null)
                        {
                            // 标记为已拥有，避免显示"(未拥有)"
                            try { keycard.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                            try { keycard.DisableTag("not_purchased", true); } catch { }
                            internalInv.UncheckedAccept(keycard);
                        }
                    }
                    catch (Exception ex)
                    {
                        Core.LogMsg($"[蛙哥妙妙箱] 放入{box.Name}失败: {ex.Message}");
                    }
                }
                
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[蛙哥妙妙箱] FillWithRandomLockedBoxes异常: {ex.Message}\n{ex.StackTrace}");
            }
        }
        public static void RegisterToDirectory(ItemDirectory dir)
        {
            try
            {
                if (dir == null) return;
                if (((Directory<GameItem>)(object)dir).Has(CONTAINER_ID)) return;
                if (_registeredFactory == null)
                {
                    System.Func<GameItem> systemFactory = () => CreateRegisteredContainer();
                    _registeredFactory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)systemFactory);
                }
                bool ok = ((Directory<GameItem>)(object)dir).Add(CONTAINER_ID, _registeredFactory);
                Core.LogMsg($"[自定义储物箱] {(ok ? "★ 已注册" : "⚠️ 注册失败")} {CONTAINER_ID} 到 {dir.GetType().Name}（读档原生恢复窗口）");
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] 注册到目录异常: {ex.Message}");
            }
        }
}
}
