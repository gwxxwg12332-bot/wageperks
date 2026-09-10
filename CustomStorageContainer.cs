using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace JacksonPerks
{
    /// <summary>
    /// 自定义储物容器 - 2×2外部占用，内部大容量
    /// 创建新物品+自定义sprite，不影响其他storage_bay
    /// </summary>
    public static class CustomStorageContainer
    {
        public const string CONTAINER_ID = "custom_storage_box";
        public static string CONTAINER_NAME => LangHelper.T("蛙哥妙妙箱", "Frog Wonder Box");
        public static string CONTAINER_DESC => LangHelper.T("蛙哥传奇当铺的镇店之宝，占用2×2空间，内部52×10大容量储物箱，什么都能放（包括箱子和机器）。", "The treasure of Frog's legendary pawnshop. 2x2 footprint, 52x10 internal storage. Can hold anything (including crates and machines).");
        public const string CUSTOM_ATLAS = "custom_atlas";
        public const string CUSTOM_SPRITE_NAME = "custom_storage_box_sprite";

        // 用静态构造函数确保_customSprite在任何方法调用之前已创建
        private static Sprite _customSprite;
        private static GameInventory _lastCreatedInventory; // 保存最后创建的内部库存，供FillWithRandomLockedBoxes使用

    /// <summary>最近一次 CreateContainer 创建的内部库存（供工具箱等改容量用）</summary>
    internal static GameInventory LastCreatedInventory => _lastCreatedInventory;
        private static bool _spriteCreated = false;

        static CustomStorageContainer()
        {
            try
            {
                _customSprite = CreateCustomBoxSprite();
                _spriteCreated = (_customSprite != null);
                Core.LogMsg($"[自定义储物箱] 静态构造函数创建sprite: {(_spriteCreated ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] 静态构造函数异常: {ex.Message}");
            }
        }

        public static void Init()
        {
            // 静态构造函数已经创建了sprite，这里不需要做什么
        }

        public static Sprite GetCustomSprite()
        {
            return _customSprite;
        }

        /// <summary>
        /// 创建自定义箱子sprite（用用户设计图缩小到32×32像素，保留好看外观+像素风格）
        /// </summary>
        private static Sprite CreateCustomBoxSprite()
        {
            try
            {
                // 用嵌入的像素数据（用户设计图缩小到32×32，最近邻插值保留像素风）
                int width = StorageBoxPixels.Width;
                int height = StorageBoxPixels.Height;
                Color[] pixels = StorageBoxPixels.GetPixels();
                
                
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.DontSave;
                tex.SetPixels(pixels);
                tex.Apply();
                
                // 创建sprite（pixelsPerUnit=100，32/100=0.32单位，跟卫生纸一样大）
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.DontSave;
                
                return sprite;
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] CreateCustomBoxSprite异常: {ex.Message}\n{ex.StackTrace}");
                return CreateFallbackSprite();
            }
        }
        
        /// <summary>
        /// 游戏风格的像素箱子（保留设计图元素：紫色边框+青色发光条+金属灰+锁扣+铆钉，像素风暗色调）
        /// </summary>
        private static Color GetGameStyleBoxPixel(int x, int y, int w, int h)
        {
            // 透明背景（游戏物品都是透明背景）
            if (x < 2 || x >= w - 2 || y < 3 || y >= h - 2)
            {
                // 圆角效果
                if ((x < 3 && y < 4) || (x >= w - 3 && y < 4) ||
                    (x < 3 && y >= h - 3) || (x >= w - 3 && y >= h - 3))
                {
                    return new Color(0, 0, 0, 0);
                }
            }
            
            // 外边框（暗紫色，保留设计图元素）
            if (x == 2 || x == w - 3 || y == 3 || y == h - 3)
            {
                // 顶部高光
                if (y == 3 && x > 3 && x < w - 4)
                {
                    return new Color(0.50f, 0.35f, 0.55f, 1f);
                }
                return new Color(0.38f, 0.25f, 0.42f, 1f);
            }
            
            // 内边框（金属灰）
            if (x == 3 || x == w - 4 || y == 4 || y == h - 4)
            {
                if (y == 4 && x > 4 && x < w - 5)
                {
                    return new Color(0.55f, 0.52f, 0.48f, 1f);
                }
                return new Color(0.42f, 0.40f, 0.37f, 1f);
            }
            
            // 主体（暗金属灰，带渐变）
            float gradient = (y - 5) / (float)(h - 8);
            float r = 0.38f - gradient * 0.08f;
            float g = 0.36f - gradient * 0.08f;
            float b = 0.33f - gradient * 0.07f;
            
            // 顶部高光带
            if (y >= 5 && y <= 7 && x > 4 && x < w - 5)
            {
                r += 0.10f; g += 0.10f; b += 0.08f;
            }
            
            // 底部阴影带
            if (y >= h - 6 && y <= h - 5 && x > 4 && x < w - 5)
            {
                r -= 0.08f; g -= 0.08f; b -= 0.06f;
            }
            
            // 青色发光条（保留设计图元素，暗青色，像素风）
            int glowY = h / 2 - 1;
            if (y >= glowY && y <= glowY + 1 && x > 5 && x < w - 6)
            {
                // 跳过锁扣位置
                int lockX = w / 2;
                if (x < lockX - 4 || x > lockX + 4)
                {
                    if (y == glowY)
                    {
                        return new Color(0.35f, 0.65f, 0.70f, 1f); // 亮青色
                    }
                    else
                    {
                        return new Color(0.20f, 0.45f, 0.50f, 1f); // 暗青色
                    }
                }
            }
            
            // 中间分隔线（箱子盖和箱体的分界线）
            if (y == h / 2 + 2 && x > 4 && x < w - 5)
            {
                r = 0.28f; g = 0.26f; b = 0.23f;
            }
            
            // 锁扣（中间，紫色金属，保留设计图元素）
            int lockX2 = w / 2;
            int lockY2 = h / 2 - 2;
            if ((x >= lockX2 - 3 && x <= lockX2 + 3 && y >= lockY2 && y <= lockY2 + 6) ||
                (x >= lockX2 - 2 && x <= lockX2 + 2 && y >= lockY2 - 2 && y <= lockY2))
            {
                // 锁扣高光（紫色）
                if (x == lockX2 - 2 || y == lockY2 - 1)
                {
                    return new Color(0.55f, 0.40f, 0.60f, 1f);
                }
                // 锁孔
                if (x == lockX2 && y == lockY2 + 3)
                {
                    return new Color(0.15f, 0.10f, 0.18f, 1f);
                }
                return new Color(0.45f, 0.30f, 0.50f, 1f);
            }
            
            // 铆钉（四角，紫色金属，保留设计图元素）
            int rivet = 5;
            if ((x == rivet && y == rivet + 1) || (x == w - 1 - rivet && y == rivet + 1) ||
                (x == rivet && y == h - 1 - rivet) || (x == w - 1 - rivet && y == h - 1 - rivet))
            {
                return new Color(0.55f, 0.40f, 0.60f, 1f);
            }
            
            // 噪点纹理（像素风质感）
            if ((x * 7 + y * 13) % 19 == 0)
            {
                r -= 0.04f; g -= 0.04f; b -= 0.03f;
            }
            
            return new Color(
                Mathf.Clamp01(r),
                Mathf.Clamp01(g),
                Mathf.Clamp01(b),
                1f
            );
        }
        
        /// <summary>
        /// 备用sprite（代码生成的简单金属箱子）
        /// </summary>
        private static Sprite CreateFallbackSprite()
        {
            int width = 32;
            int height = 32;
            try
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.DontSave;
                
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = GetBoxPixel(x, y, width, height);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.DontSave;
                return sprite;
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] CreateFallbackSprite异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建自定义储物箱（完整工厂链路，参考MiniSmugglerBay）
        /// </summary>
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
                var invWindow = DirectoryUtils.CreateInventoryWindow(52, 10, true);
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

        /// <summary>
        /// 获取箱子某个像素的颜色（全新设计，金属箱子风格）
        /// </summary>
        private static Color GetBoxPixel(int x, int y, int w, int h)
        {
            // 边框（金属灰色）
            int border = Math.Max(2, w / 16);
            if (x < border || x >= w - border || y < border || y >= h - border)
            {
                float brightness = 1.0f - (x + y) / (float)(w + h) * 0.4f;
                return new Color(0.5f * brightness, 0.5f * brightness, 0.55f * brightness, 1f);
            }

            // 主体（深灰蓝色）
            Color bodyColor = new Color(0.25f, 0.28f, 0.32f, 1f);

            // 中央横向分隔线
            int midY = h / 2;
            if (y >= midY - 1 && y <= midY + 1)
            {
                return new Color(0.15f, 0.17f, 0.2f, 1f);
            }

            // 中央把手（上半部分中央）
            int handleX = w / 2;
            int handleY = h / 4;
            int handleW = Math.Max(8, w / 4);
            int handleH = Math.Max(3, h / 10);
            if (x >= handleX - handleW / 2 && x < handleX + handleW / 2 &&
                y >= handleY - handleH / 2 && y < handleY + handleH / 2)
            {
                if (x == handleX - handleW / 2 || x == handleX + handleW / 2 - 1 ||
                    y == handleY - handleH / 2 || y == handleY + handleH / 2 - 1)
                {
                    return new Color(0.6f, 0.6f, 0.65f, 1f);
                }
                return new Color(0.1f, 0.12f, 0.15f, 1f);
            }

            // 铆钉（四角）
            int rivetSize = Math.Max(1, w / 32);
            int rivetOffset = Math.Max(3, w / 8);
            if ((x >= rivetOffset && x < rivetOffset + rivetSize &&
                 y >= rivetOffset && y < rivetOffset + rivetSize) ||
                (x >= w - rivetOffset - rivetSize && x < w - rivetOffset &&
                 y >= rivetOffset && y < rivetOffset + rivetSize) ||
                (x >= rivetOffset && x < rivetOffset + rivetSize &&
                 y >= h - rivetOffset - rivetSize && y < h - rivetOffset) ||
                (x >= w - rivetOffset - rivetSize && x < w - rivetOffset &&
                 y >= h - rivetOffset - rivetSize && y < h - rivetOffset))
            {
                return new Color(0.7f, 0.7f, 0.75f, 1f);
            }

            // 下半部分标签区域
            if (y > midY + h / 8)
            {
                int labelX = w / 2;
                int labelY = h * 3 / 4;
                int labelW = Math.Max(10, w / 2);
                int labelH = Math.Max(4, h / 6);
                if (x >= labelX - labelW / 2 && x < labelX + labelW / 2 &&
                    y >= labelY - labelH / 2 && y < labelY + labelH / 2)
                {
                    return new Color(0.4f, 0.42f, 0.35f, 1f);
                }
            }

            return bodyColor;
        }

        /// <summary>
        /// 设置私有字段
        /// </summary>
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
        
        /// <summary>
        /// 创建带原版内容的上锁箱子（用PreBuiltItemHelper.LootCrate*）
        /// </summary>
        private static GameItem CreateLootCrate(string itemId)
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
        
        /// <summary>
        /// 往蛙哥妙妙箱里放随机上锁的箱子 + 指挥卡（command_keycard）
        /// 需求：不要研发卡(sci_keycard)生成在箱子里；只要一张指挥卡(cmd_keycard)放随机箱子
        /// </summary>
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


        private static Il2CppSystem.Func<GameItem> _registeredFactory;

        /// <summary>
        /// 把 custom_storage_box 注册到游戏物品目录（DirectoryMaster.Item 的工厂表）。
        /// 这样读档时游戏走原生路径 SaveManager.DecodeNodes → DirectoryMaster.Item(identifier)
        /// → 我们的工厂创建带 contentWindow 的完整箱子 → 能打开；内部内容由 DecodeNodes 按存档恢复。
        /// 与 EmptyNukeBarrel 注册 empty_nuclear_waste_barrel 同一机制。
        /// </summary>
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

        /// <summary>
        /// DirectoryMaster 工厂创建：返回空箱子（含窗口/标签/类型/sprite，不带内容）。
        /// 注意：DirectoryMaster.Item 创建出带 childItems 的物品会被游戏警告并移除，
        /// 所以工厂只返回空箱子；内容由读档时 DecodeNodes 按存档 uuid 恢复。
        /// </summary>
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