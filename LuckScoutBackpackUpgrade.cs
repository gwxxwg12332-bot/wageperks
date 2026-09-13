using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;



// ============================================================

// 虚空珠：1格占地、可带出去拾荒、内部20x10网格

// 初始1格解锁，用垃圾(junk)升级逐格解锁

// 锁格子用 GameGridInventory.SetShape 原生机制（'1'=可放置/开放格，'0'=关闭/障碍格，2.5.36 拆包实锤）

// 自定义sprite（PNG加载 + Patch RenderHandler.LoadFromAtlas）

// ============================================================

internal static class LuckScoutBackpackUpgrade

{

    // ===== 自定义sprite =====

    private const string CUSTOM_ATLAS = "custom_atlas";

    private const string CUSTOM_SPRITE_KEY = "void_bead_sprite";

    private static Sprite _customSprite;

    // 嵌入的PNG字节数组（191字节，16x16深紫色虚空珠图标），发给别人也能正常显示，不依赖外部文件

private static readonly byte[] EMBEDDED_VOID_BEAD_PNG = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0xF3, 0xFF, 0x61, 0x00, 0x00, 0x03, 0x7E, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x7D, 0x53, 0x6D, 0x6C, 0x53, 0x65, 0x14, 0x3E, 0xEF, 0xFD, 0xEE, 0xA5, 0x1D, 0xFD, 0x18, 0xBB, 0xDD, 0xDD, 0x47, 0xCB, 0x66, 0xD9, 0x5A, 0xB6, 0xB9, 0xC2, 0x00, 0x2B, 0x2C, 0x60, 0x91, 0x88, 0xC2, 0x30, 0x6A, 0xA6, 0xA2, 0xC1, 0x1F, 0x90, 0x4C, 0xA3, 0xC6, 0x44, 0xD4, 0x88, 0xF8, 0x63, 0x90, 0xA8, 0xD1, 0xC4, 0xC4, 0x98, 0x88, 0x91, 0x09, 0x2A, 0x89, 0x09, 0x2B, 0xD1, 0x64, 0x80, 0x06, 0x23, 0xE1, 0xC7, 0xEA, 0x40, 0x27, 0x63, 0x22, 0x63, 0x65, 0x42, 0xBB, 0xAD, 0x9D, 0xAC, 0xA5, 0x5D, 0x47, 0xBB, 0xDB, 0x8F, 0xDB, 0xDB, 0xF6, 0xBE, 0xA6, 0x1B, 0x4B, 0x14, 0x8D, 0xE7, 0xCF, 0x79, 0xDF, 0xF3, 0x9E, 0xE7, 0x7D, 0xCE, 0xC9, 0x39, 0x0F, 0x82, 0x7F, 0x18, 0x46, 0x00, 0x08, 0xEF, 0x70, 0xFE, 0xBE, 0x23, 0x52, 0x08, 0x76, 0x26, 0xA6, 0x7F, 0x1E, 0x15, 0x90, 0x72, 0xC1, 0x4E, 0xAF, 0x18, 0xEA, 0x09, 0x4F, 0xCB, 0x00, 0x07, 0x70, 0xE9, 0xFD, 0xEF, 0x08, 0x62, 0xF1, 0xD0, 0xD9, 0x89, 0x49, 0x80, 0x03, 0x68, 0x57, 0xA3, 0xFF, 0xF1, 0x6A, 0xBE, 0xEA, 0x21, 0xFD, 0x96, 0x8E, 0xFD, 0x49, 0x01, 0x36, 0xB3, 0x9A, 0xA6, 0xFE, 0x28, 0x2A, 0x73, 0x03, 0x1C, 0x54, 0xEF, 0x06, 0xC3, 0xBF, 0xD9, 0x01, 0xDA, 0xC5, 0x43, 0x35, 0xDB, 0x84, 0x63, 0xBB, 0xCB, 0xCA, 0x9E, 0x7F, 0xF6, 0x01, 0xEB, 0x11, 0xEF, 0xD3, 0x96, 0x8B, 0x3B, 0xE7, 0xE3, 0xCB, 0x3F, 0x5F, 0xBB, 0x51, 0xFC, 0xA4, 0xB5, 0x94, 0xD7, 0xB5, 0x7A, 0x88, 0xEE, 0x86, 0xEE, 0x79, 0x72, 0x6A, 0x01, 0x5C, 0xBA, 0x20, 0x6C, 0x36, 0xEF, 0xB2, 0x73, 0x24, 0xF1, 0x9E, 0x16, 0x5B, 0xCD, 0x4F, 0x39, 0xDA, 0xEE, 0x1B, 0xA3, 0x86, 0x47, 0x6E, 0xFA, 0xFD, 0x55, 0xF7, 0x37, 0x0E, 0xBA, 0x02, 0xD8, 0xDB, 0x58, 0x51, 0x64, 0xFE, 0x28, 0xE5, 0xF5, 0x5C, 0x82, 0xFC, 0x22, 0x0E, 0x95, 0x5C, 0xE9, 0xC7, 0x9E, 0x4B, 0xAB, 0x0B, 0xEE, 0x6A, 0xCF, 0x31, 0xAD, 0x86, 0x93, 0x10, 0x4A, 0x38, 0x83, 0xB9, 0xC9, 0x55, 0xC1, 0xF0, 0x14, 0xAB, 0x47, 0x00, 0x0D, 0xE6, 0x47, 0x60, 0xE5, 0xF6, 0xB6, 0x7D, 0x87, 0xBF, 0x7D, 0xFF, 0x94, 0xB3, 0xE8, 0x72, 0x58, 0x2B, 0x4C, 0x4F, 0x50, 0x86, 0xC8, 0x3B, 0x5F, 0x0E, 0xEC, 0xF1, 0xA1, 0x05, 0xF6, 0x52, 0x7F, 0x00, 0x6B, 0xC4, 0x4F, 0xB7, 0x2F, 0x51, 0x6F, 0x78, 0xB8, 0xA5, 0x59, 0xFE, 0x97, 0x3F, 0xC7, 0x33, 0x4A, 0x1A, 0x71, 0x05, 0x42, 0x52, 0x44, 0xF2, 0x5E, 0xAA, 0xE3, 0xB9, 0x37, 0x66, 0xE6, 0xE8, 0xD4, 0x71, 0xF6, 0x33, 0xE5, 0xA0, 0xD1, 0x49, 0xB9, 0x4E, 0xCE, 0x7C, 0x7D, 0xE4, 0x36, 0x15, 0x7E, 0x6C, 0xBE, 0x82, 0x0D, 0xB6, 0xB3, 0x75, 0x74, 0x6E, 0xB9, 0x35, 0xAA, 0x7C, 0x64, 0x28, 0x2F, 0x6A, 0x3D, 0x84, 0xA9, 0x90, 0x0A, 0x46, 0x14, 0x5E, 0x93, 0xAD, 0x25, 0x6C, 0x68, 0x03, 0x0A, 0xE2, 0x70, 0x21, 0x5D, 0x33, 0xCB, 0x38, 0x6B, 0xD6, 0x47, 0xF1, 0x54, 0xA0, 0x6F, 0x2C, 0x1B, 0xF2, 0xC8, 0xC4, 0xE4, 0xF7, 0x39, 0x76, 0xEE, 0x14, 0x61, 0x11, 0xF7, 0x3A, 0x73, 0x82, 0xFC, 0x02, 0xC3, 0xE5, 0x74, 0xA4, 0xB1, 0xE5, 0x96, 0x2C, 0x38, 0x22, 0xD1, 0xB8, 0x4A, 0x6B, 0x93, 0xEB, 0xA9, 0x7A, 0xDA, 0x85, 0x62, 0x30, 0x0C, 0x02, 0x51, 0x4D, 0x12, 0x58, 0x50, 0xC7, 0xA6, 0xCF, 0x60, 0x82, 0x27, 0x5A, 0xEF, 0x11, 0x9A, 0xCF, 0x99, 0x58, 0x51, 0xD5, 0x85, 0x13, 0xEF, 0x12, 0xF9, 0xC2, 0xCC, 0xB6, 0xB8, 0xFF, 0x0C, 0xFF, 0xC3, 0x75, 0xC7, 0xE9, 0x3C, 0xD0, 0x6F, 0x66, 0xD5, 0x0B, 0xC6, 0x40, 0xF2, 0x27, 0x7E, 0x8D, 0x60, 0xCB, 0x85, 0xE9, 0xEF, 0xF2, 0xE7, 0xE5, 0xD7, 0xA0, 0x5E, 0xD0, 0x28, 0x75, 0x05, 0x39, 0x4F, 0x51, 0x52, 0x45, 0x3C, 0x12, 0xF7, 0x3A, 0xD0, 0x8A, 0x89, 0x5A, 0xCE, 0xD5, 0x73, 0x35, 0xDD, 0x7B, 0x85, 0x4A, 0xE7, 0x7E, 0x3B, 0xA4, 0x2F, 0x7B, 0xF4, 0xCA, 0x56, 0xF3, 0x71, 0x93, 0xA8, 0x4E, 0xFF, 0x9A, 0x2F, 0xDA, 0xB6, 0x12, 0xF9, 0xD9, 0xDC, 0x60, 0xF2, 0x1C, 0x6D, 0xE5, 0xDA, 0xB1, 0x85, 0x94, 0xE1, 0xE6, 0x0C, 0xC9, 0x5E, 0x2B, 0x0E, 0x03, 0xCF, 0x4A, 0x99, 0x38, 0xA7, 0xD9, 0x12, 0xD3, 0x5C, 0x3D, 0xD1, 0x3B, 0xE4, 0xDE, 0x17, 0xD9, 0xD4, 0x4D, 0x11, 0xC9, 0xE4, 0xC8, 0xED, 0x18, 0x8A, 0x79, 0x24, 0x26, 0xB3, 0xD6, 0xCE, 0x3E, 0x68, 0x32, 0x36, 0xB4, 0xE7, 0x1E, 0xB6, 0xBD, 0x9E, 0x0D, 0x66, 0xC3, 0xE4, 0x68, 0xE2, 0x32, 0xA1, 0x27, 0x1B, 0x60, 0x30, 0xD5, 0x07, 0xB4, 0x61, 0x54, 0xAD, 0x65, 0x68, 0x26, 0xCB, 0x4A, 0xDF, 0x1C, 0x0E, 0x9D, 0xFD, 0x00, 0x21, 0x04, 0xFD, 0x9B, 0x40, 0x45, 0x77, 0xD6, 0x97, 0x6A, 0xAE, 0xFC, 0xE2, 0xC5, 0x2A, 0xD5, 0x52, 0x31, 0xBE, 0xB4, 0xFF, 0x19, 0x77, 0x65, 0x87, 0x70, 0xCD, 0x17, 0xC7, 0x93, 0xB3, 0x03, 0xCC, 0x2D, 0x74, 0x99, 0xD0, 0x69, 0x13, 0x45, 0x9B, 0xA1, 0x85, 0x36, 0x52, 0xAD, 0x43, 0x81, 0x42, 0x6A, 0xAF, 0x6F, 0xE2, 0xD5, 0xF3, 0x08, 0xD0, 0xFC, 0xE4, 0xA8, 0x3B, 0xEB, 0x99, 0x1F, 0x09, 0xEF, 0xFE, 0x78, 0x0E, 0x36, 0x72, 0x19, 0xA9, 0xBE, 0x7F, 0x70, 0xAE, 0xF7, 0xE4, 0xBA, 0x65, 0x5D, 0x34, 0x5F, 0x4C, 0xAB, 0xDA, 0xFC, 0x38, 0x92, 0x8C, 0x04, 0xF0, 0x8C, 0x3D, 0x63, 0x30, 0xB5, 0x64, 0xBE, 0xAA, 0xB3, 0xFB, 0xD0, 0x04, 0x60, 0x0C, 0x18, 0x21, 0x40, 0x78, 0x51, 0x0B, 0x08, 0xE0, 0x04, 0x19, 0x42, 0x5E, 0x39, 0x9A, 0x3E, 0xFA, 0xA3, 0xDD, 0xE6, 0x3E, 0x3D, 0x5B, 0x98, 0x4A, 0xB1, 0x76, 0x9E, 0x69, 0x2B, 0x7F, 0x1B, 0x89, 0xC9, 0x55, 0x74, 0x80, 0x0A, 0x0D, 0xE8, 0x32, 0xCB, 0x98, 0x71, 0x29, 0xA1, 0xFF, 0x2F, 0x31, 0x61, 0x80, 0x27, 0x55, 0x8C, 0x31, 0xDA, 0x53, 0x7E, 0x54, 0xE7, 0x9B, 0xB8, 0x31, 0x2A, 0xAC, 0x54, 0x9A, 0x2B, 0x9B, 0xD6, 0xED, 0x0F, 0x31, 0xD7, 0xC3, 0x32, 0xA3, 0xF3, 0x8A, 0x4A, 0xE7, 0xC5, 0x44, 0x3C, 0xF6, 0xF2, 0xCE, 0x3E, 0x4B, 0x60, 0x81, 0xF1, 0xFF, 0x84, 0xD5, 0xF4, 0x96, 0x50, 0x72, 0x5A, 0xE8, 0x2A, 0x7F, 0x89, 0xF5, 0x7F, 0xF8, 0xCA, 0x92, 0xEC, 0xE6, 0xBB, 0x45, 0xB7, 0x68, 0x7F, 0x01, 0xDD, 0x9F, 0x76, 0x6C, 0x99, 0x45, 0xA3, 0x35, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 };



    // ===== 升级相关 =====

    private const string BACKPACK_TAG = "VOID_BEAD_TAG";

    private const string SLOTS_TAG = "VOID_BEAD_SLOTS_TAG";

    private const string MATERIAL_ID = "junk";

    private static DateTime _lastConsume = DateTime.MinValue;



    // 虚空珠物品列表（读档恢复时用）

    private static readonly List<GameItem> _beadItems = new List<GameItem>();
    // 开局延迟应用锁格（容器就绪后强制 SetShape，防开局创建时被初始化覆盖）
    private static readonly List<Tuple<GameItem, int>> _pendingShapes = new List<Tuple<GameItem, int>>();

    // 内部网格尺寸

    private const int GRID_WIDTH = 20;

    private const int GRID_HEIGHT = 10;

    private const int MAX_SLOTS = GRID_WIDTH * GRID_HEIGHT; // 200



    // ===== 静态构造函数：加载PNG sprite =====

    static LuckScoutBackpackUpgrade()

    {

        try

        {

            try

            {

                byte[] pngBytes = EMBEDDED_VOID_BEAD_PNG; // 嵌入DLL的字节（发布可用，不依赖外部文件）
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = (TextureWrapMode)1,
                    hideFlags = (HideFlags)61
                };
                Type icType = null;
                foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = a.GetTypes(); }
                    catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                    foreach (Type t in types)
                    {
                        if (t != null && t.Name == "ImageConversion") { icType = t; break; }
                    }
                    if (icType != null) break;
                }
                if (icType != null)
                {
                    var loadMethod = icType.GetMethod("LoadImage",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                        null, new Type[] { typeof(Texture2D), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>) }, null);
                    if (loadMethod != null)
                        loadMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)pngBytes });
                }
                _customSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                _customSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception ex) { Core.LogMsg("[虚空珠] 加载sprite失败: " + ex.Message); }


        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 静态构造函数异常: " + ex.Message); }

    }



    // Patch RenderHandler.LoadFromAtlas

    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)

    {

        try

        {

            if (atlasPath == CUSTOM_ATLAS && name == CUSTOM_SPRITE_KEY && _customSprite != null)

            {

                __result = _customSprite;

                return false;

            }

        }

        catch { }

        return true;

    }



    // ===== SetShape 锁格子工具 =====

    /// <summary>

    /// 生成锁格子形状字符串：前slots个'0'（开放），后面'1'（障碍不能放）

    /// 按行优先排列：第0行前N格开放，第1行继续...

    /// </summary>

    private static string BuildLockedShape(int slots, int width, int height)

    {

        int total = width * height;

        char[] chars = new char[total];

        for (int i = 0; i < total; i++)

        {

            // 09-10 用户实测'封印和解锁反了' → 回滚：'0'=可放置/开放，'1'=关闭/锁（原实现正确；此前按拆包 stub 误改反）
            chars[i] = (i < slots) ? '0' : '1';

        }

        return new string(chars);

    }



    /// <summary>

    /// 应用锁格子形状到 GameGridInventory

    /// </summary>

    private static void ApplyLockedShape(GameGridInventory inv, int slots)

    {

        try

        {

            if (inv == null) return;

            if (slots < 1) slots = 1;

            if (slots > MAX_SLOTS) slots = MAX_SLOTS;

            string shape = BuildLockedShape(slots, GRID_WIDTH, GRID_HEIGHT);

            string __head = shape.Substring(0, Math.Min(20, shape.Length));

            inv.SetShape(shape, GRID_WIDTH);

            inv.Validate();

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] ApplyLockedShape异常: " + ex.Message); }

    }



    // 每帧调用

    internal static void ProcessPendingValidate()
    {
        try
        {
            if (_pendingShapes.Count == 0) return;
            for (int i = _pendingShapes.Count - 1; i >= 0; i--)
            {
                var pe = _pendingShapes[i];
                var item = pe.Item1;
                if (item == null) { _pendingShapes.RemoveAt(i); continue; }
                var cw = item.contentWindow;
                if (cw == null || cw.childElement == null) continue; // 容器未就绪，下帧再试
                var inv = cw.childElement.Cast<GameGridInventory>();
                if (inv != null)
                {
                    // 【09-13 不降级保护】pending 目标不得低于当前已升级 slots（读档重建入队 1 不能覆盖升级后的 6）
                    int target = Math.Max(pe.Item2, GetTagInt(item, SLOTS_TAG));
                    ApplyLockedShape(inv, target);
                    try { cw.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + target + LangHelper.T("/200格)", "/200 slots)"); } catch { }
                }
                _pendingShapes.RemoveAt(i);
            }
        }
        catch { }
    }



    // ===== 读档恢复：SetContentWindow后识别虚空珠并恢复SetShape =====

    public static void PostfixSetContentWindow(GameItem __instance)

    {

        try

        {

            if (__instance == null) return;

            if (!__instance.IsTag(BACKPACK_TAG)) return;

            // 加入_beadItems列表（去重）

            try { if (!_beadItems.Contains(__instance)) _beadItems.Add(__instance); } catch { }



            // 修改PixelWindow背景色

            try

            {

                var cw = __instance.contentWindow;

                if (cw != null)

                {

                    IntPtr winPtr = cw.Pointer;

                    IntPtr winBgPtr = (IntPtr)(winPtr.ToInt64() + 0x28); // handler节点

                    IntPtr winBgObjPtr = Marshal.ReadIntPtr(winBgPtr);

                    if (winBgObjPtr != IntPtr.Zero)

                    {

                        var winBgObj = new Il2CppSystem.Object(winBgObjPtr);

                        var winBgMono = winBgObj.Cast<MonoBehaviour>();

                        if (winBgMono != null)

                        {

                            var winImg = winBgMono.GetComponent<UnityEngine.UI.Image>();

                            if (winImg != null) winImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

                        }

                    }

                }

            } catch (Exception exw) { Core.LogMsg("[虚空珠] 读档改背景异常: " + exw.Message); }



            // 读档后恢复SetShape锁格子 + 更新标题

            int slots = GetTagInt(__instance, SLOTS_TAG);

            if (slots <= 0) slots = 1;


            try { if (__instance.contentWindow != null) __instance.contentWindow.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + slots + LangHelper.T("/200格)", "/200 slots)"); } catch { }

            try

            {

                var cw = __instance.contentWindow;

                if (cw != null && cw.childElement != null)

                {

                    string invType = cw.childElement.GetType().Name;


                    var inv = cw.childElement.Cast<GameGridInventory>();

                    if (inv != null)

                    {

                        ApplyLockedShape(inv, slots);


                    }

                    else

                    {

                        Core.LogMsg("[虚空珠] 读档警告: 内部库存类型" + invType + "不是GameGridInventory，Cast失败，无法恢复SetShape（将在打开窗口时双保险恢复）");

                    }

                }

            } catch (Exception exv) { Core.LogMsg("[虚空珠] 读档恢复SetShape异常: " + exv.Message); }




        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] PostfixSetContentWindow异常: " + ex.Message); }

    }



    // 检查玩家所有库存（4 主背包 + 递归容器）是否已有虚空珠储物袋（09-13 多刷根治：发放前判定，位置无关）
    public static bool HasAnyVoidBeadStorage()
    {
        try
        {
            var allInvs = new System.Collections.Generic.List<GameInventory>();
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null) return false;
            try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            try { var v = emporium.frontInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
            var visited = new HashSet<IntPtr>();
            var stack = new Stack<GameInventory>(allInvs);
            while (stack.Count > 0)
            {
                var inv = stack.Pop();
                if (inv == null || inv.childItems == null) continue;
                for (int i = 0; i < inv.childItems.Count; i++)
                {
                    var it = inv.childItems[i];
                    if (it == null || !visited.Add(it.Pointer)) continue;
                    if (it.IsTag(BACKPACK_TAG) || it.identifier == "void_bead_storage") return true;
                    try
                    {
                        var cw = it.contentWindow;
                        if (cw == null || cw.childElement == null) continue;
                        var inner = cw.childElement.Cast<GameGridInventory>();
                        if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return false;
    }

    // ===== 读档恢复：LoadGame完成后遍历玩家所有背包找虚空珠并恢复SetShape（根本方案） =====
    // 不依赖_beadItems列表（读档后列表为空），直接遍历玩家4个背包
    // 加固（2.5.39）：容器内容读档后延迟加载 → 立即恢复可能漏掉容器内虚空珠（尤其满级无法"碰一下自愈"）
    // → 立即尝试 + 未找到则挂 Core.OnUpdate 轮询 600 帧重试；满级 DoUpgrade 也触发形状恢复

    private static int _restoreFramesLeft = 0;

    // Core.OnUpdate 挂钩：每帧轻量重试（容器内容加载完成后一次成功即停）
    public static void OnUpdateRestore()
    {
        try
        {
            if (_restoreFramesLeft <= 0) return;
            _restoreFramesLeft--;
            if (RestoreAllBeadsInPlayerInventories()) _restoreFramesLeft = 0;
        }
        catch { }
    }

    public static void PostfixLoadGame()
    {
        try { PerkStatePersistence.ResetCache(); } catch { } // LoadGame 切档：清 runID 缓存，防 key 前缀串用
        try
        {
            if (RestoreAllBeadsInPlayerInventories()) { _restoreFramesLeft = 0; return; }
            _restoreFramesLeft = 180; // 容器内容延迟加载：每帧重试，最多约3秒（成功即停；原600帧递归遍历全库存造成读档后10秒内每帧开销，交易时叠加卡顿）
        }
        catch (Exception ex) { Core.LogMsg("[虚空珠] PostfixLoadGame异常: " + ex.Message); }
    }

    private static bool RestoreAllBeadsInPlayerInventories()
    {
        int restored = 0;

        // 收集玩家所有背包（后背包+计数器+展示柜+主存储）
        var allInvs = new System.Collections.Generic.List<GameInventory>();
        try
        {
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium != null)
            {
                try { var v = emporium.backInvinvElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.backInvinvElementCounter as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.showcaseElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }
                try { var v = emporium.invElement as GameInventory; if (v != null) allInvs.Add(v); } catch { }

                // 递归容器内容库存：虚空珠可能放在容器里（用户反馈：容器回档需拖垃圾升级才恢复）
                // 根因：容器内的虚空珠不在 4 个主背包里，LoadGame 恢复遍历漏掉 → 升级操作碰一下才 ApplyLockedShape
                var visited = new HashSet<IntPtr>();
                var stack = new Stack<GameInventory>(allInvs);
                while (stack.Count > 0)
                {
                    var inv = stack.Pop();
                    if (inv == null || inv.childItems == null) continue;
                    for (int i = 0; i < inv.childItems.Count; i++)
                    {
                        var it = inv.childItems[i];
                        if (it == null || !visited.Add(it.Pointer)) continue;
                        try
                        {
                            var cw = it.contentWindow;
                            if (cw == null || cw.childElement == null) continue;
                            var inner = cw.childElement.Cast<GameGridInventory>();
                            if (inner != null && !allInvs.Contains(inner)) { allInvs.Add(inner); stack.Push(inner); }
                        }
                        catch { }
                    }
                }
            }
        } catch { }

        foreach (var inv in allInvs)
        {
            if (inv == null || inv.childItems == null) continue;
            foreach (var item in inv.childItems)
            {
                if (item == null) continue;
                try
                {
                    if (!item.IsTag(BACKPACK_TAG)) continue;
                    int slots = GetTagInt(item, SLOTS_TAG);
                    if (slots <= 0) slots = 1;
                    var cw = item.contentWindow;
                    if (cw == null || cw.childElement == null) continue;
                    var gridInv = cw.childElement.Cast<GameGridInventory>();
                    if (gridInv == null) continue;
                    ApplyLockedShape(gridInv, slots);
                    try { cw.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + slots + LangHelper.T("/200格)", "/200 slots)"); } catch { }
                    restored++;
                }
                catch (Exception exi) { Core.LogMsg("[虚空珠] LoadGame恢复单个虚空珠异常: " + exi.Message); }
            }
        }
        return restored > 0;
    }




    // ===== 创建虚空珠 =====
    // enqueuePending：新档发放(true)入队 pending + 启动 600 帧轮询（开局容器未就绪保护）；
    // 读档工厂重建(false)不挂 pending（读档恢复由 PostfixLoadGame/RestoreAllBeads 负责，挂 pending 会把存档 slots 覆盖回 1）
    public static GameItem CreateScrollableScavBackpack(bool enqueuePending = true)

    {

        try

        {

            // 用 GameGridInventory（有SetShape原生锁格子），20列x10行=200格

            var gridInv = new GameGridInventory(GRID_WIDTH, GRID_HEIGHT);



            // 包装到 PixelWindow（标题显示升级进度）

            var contentWin = new PixelWindow(true, LangHelper.T("虚空珠 (1/200格)", "Void Bead (1/200 slots)"));

            // PixelWindow背景改成深色

            try

            {

                IntPtr winPtr = contentWin.Pointer;

                IntPtr winBgPtr = (IntPtr)(winPtr.ToInt64() + 0x50);

                IntPtr winBgObjPtr = Marshal.ReadIntPtr(winBgPtr);

                if (winBgObjPtr != IntPtr.Zero)

                {

                    var winBgObj = new Il2CppSystem.Object(winBgObjPtr);

                    var winBgMono = winBgObj.Cast<MonoBehaviour>();

                    if (winBgMono != null)

                    {

                        var winImg = winBgMono.GetComponent<UnityEngine.UI.Image>();

                        if (winImg != null) winImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);


                    }

                }

            } catch (Exception exw) { Core.LogMsg("[虚空珠] PixelWindow背景改色异常: " + exw.Message); }

            contentWin.Attach(gridInv.Cast<PixelElement>());



            // 创建物品

            var item = ItemDirectory.CreateEmptyItem(null);

            if (item == null) return null;

            item.identifier = "void_bead_storage";

            item.SetContentWindow(contentWin);

            ContainerHelper.InitBackpackItem(gridInv, item);



            // 标签

            item.EnableTag("backpack");

            item.EnableTag("equippable");

            item.EnableTag("CONTAINER_TAG");

            item.EnableTag(BACKPACK_TAG);



            // 初始1格解锁（SetShape原生锁格子）
            SetTagInt(item, SLOTS_TAG, 1);
            ApplyLockedShape(gridInv, 1);
            // 开局容器未就绪时 SetShape 会被初始化覆盖（用户反馈新档显示 3/4）→ 入队，下帧容器就绪后强制应用
            // 09-13：仅新档发放时入队；读档工厂重建不入队（否则把存档 slots 覆盖回 1）
            if (enqueuePending)
            {
                try { _pendingShapes.Add(System.Tuple.Create(item, 1)); } catch { }
                // 并启动 600 帧恢复轮询（每帧 RestoreAllBeads 强制 ApplyLockedShape，覆盖创建后任意时点的初始化覆盖）
                try { _restoreFramesLeft = 600; } catch { }
            }



            // 注册到列表

            try { if (!_beadItems.Contains(item)) _beadItems.Add(item); } catch { }



            // 名称、描述和外观

            item.SetName(LangHelper.T("虚空珠", "Void Bead"));

            try { item.shortDescription = LangHelper.T("可升级便携储物。拖垃圾(junk)到珠上逐格解锁，上限200格。占地1×1，可带外出拾荒。", "Upgradeable portable storage. Drag junk onto the bead to unlock slots one by one, up to 200. 1x1 footprint, can be taken scavenging."); } catch { }

            try { item.flavorText = LangHelper.T("深紫虚空珠，内部折叠微型次元空间。", "A deep-purple void bead, folding a miniature pocket dimension inside."); } catch { }

            try { item.SetSprite(CUSTOM_ATLAS, CUSTOM_SPRITE_KEY); }

            catch (Exception exs) { Core.LogMsg("[虚空珠] SetSprite失败: " + exs.Message); try { item.SetSprite("Items/items_backpack2", "simple_backpack"); } catch { } }




            return item;

        }

        catch (Exception ex)

        {

            Core.LogMsg("[虚空珠] 创建失败: " + ex.Message);

            return null;

        }

    }



    // ===== 注册到 DirectoryMaster 工厂表（读档原生恢复 contentWindow） =====

    private static Il2CppSystem.Func<GameItem> _beadFactory = null;



    public static void RegisterToDirectory(ItemDirectory dir)

    {

        try

        {

            if (dir == null) return;

            if (((Directory<GameItem>)(object)dir).Has("void_bead_storage")) return;

            if (_beadFactory == null)

            {

                System.Func<GameItem> systemFactory = () => CreateRegisteredBead();

                _beadFactory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)systemFactory);

            }

            bool ok = ((Directory<GameItem>)(object)dir).Add("void_bead_storage", _beadFactory);

            Core.LogMsg("[虚空珠] " + (ok ? "★ 已注册" : "⚠️ 注册失败") + " void_bead_storage 到 " + dir.GetType().Name + "（读档原生恢复窗口）");

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 注册到目录异常: " + ex.Message); }

    }



    private static GameItem CreateRegisteredBead()

    {

        try

        {

            GameItem item = CreateScrollableScavBackpack(false);

            if (item != null) return item;

            try { return DirectoryMaster.Item("simple_backpack", true); } catch { }

            return null;

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 工厂创建异常: " + ex.Message); return null; }

    }



    // F9：创建并放入后背包

    private static void GiveScrollableBackpackToPlayer()

    {

        try

        {

            var item = CreateScrollableScavBackpack();

            if (item == null) return;



            var emporium = EmporiumEntry.Instance;

            if (emporium != null && emporium.backInvinvElement != null)

            {

                emporium.backInvinvElement.TryFindOneValidInventorySlot(item, false);

                bool accepted = ((GameInventory)emporium.backInvinvElement).UncheckedAccept(item);

                if (accepted)

                {

                    emporium.TransferOwnershipBackInv();


                }

            }

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 给玩家失败: " + ex.Message); }

    }



    // ===== 升级：拖垃圾到虚空珠 =====

    public static bool PrefixMayHaveValidInventorySlot(GameItem __instance, GameItem item, ref bool __result)

    {

        try

        {

            // 升级用：垃圾拖到虚空珠不放入（走升级流程）

            if (IsJunk(item) && IsBead(__instance)) { __result = false; return false; }

            if (IsJunk(__instance) && IsBead(item)) { __result = false; return false; }

            // 格子限制由 SetShape 原生处理，不需要Patch

        } catch { }

        return true;

    }



    public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)

    {

        if ((IsBead(__instance) && IsJunk(targetItem)) || (IsBead(targetItem) && IsJunk(__instance)))

        { __result = true; return false; }

        return true;

    }



    public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)

    {

        return PrefixMayTarget(__instance, targetItem, ref __result);

    }



    public static bool PrefixTarget(GameItem __instance, GameItem targetItem)

    {

        GameItem bead = null, junk = null;

        if (IsBead(__instance) && IsJunk(targetItem)) { bead = __instance; junk = targetItem; }

        else if (IsBead(targetItem) && IsJunk(__instance)) { bead = targetItem; junk = __instance; }

        if (bead != null) { DoUpgrade(junk, bead); return false; }

        return true;

    }



    private static bool DoUpgrade(GameItem junk, GameItem bead)

    {

        if (junk == null || bead == null) return false;

        if ((DateTime.UtcNow - _lastConsume).TotalSeconds < 0.5) return false;

        try

        {

            int slots = GetTagInt(bead, SLOTS_TAG);


            if (slots >= MAX_SLOTS)
            {
                // 满级：不能升级，但"碰一下"也恢复形状（读档后容器内虚空珠可能漏恢复）
                try
                {
                    var cw = bead.contentWindow;
                    if (cw != null && cw.childElement != null)
                    {
                        var inv = cw.childElement.Cast<GameGridInventory>();
                        if (inv != null) ApplyLockedShape(inv, slots);
                    }
                } catch { }
                return false;
            }

            int newSlots = slots + 1;

            SetTagInt(bead, SLOTS_TAG, newSlots);



            // 更新窗口标题（升级面板）

            try { if (bead.contentWindow != null) bead.contentWindow.titleString = LangHelper.T("虚空珠 (", "Void Bead (") + newSlots + LangHelper.T("/200格)", "/200 slots)"); } catch { }



            // 升级后用 SetShape 开放新格子

            try

            {

                var cw = bead.contentWindow;

                if (cw != null && cw.childElement != null)

                {

                    var inv = cw.childElement.Cast<GameGridInventory>();

                    if (inv != null) ApplyLockedShape(inv, newSlots);

                }

            } catch { }



            int count = junk.unitCount - 1;

            if (count <= 0) junk.Destroy();

            else junk.SetUnitCount(count);

            _lastConsume = DateTime.UtcNow;


            return true;

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 升级异常: " + ex.Message); return false; }

    }



    // ===== 工具方法 =====

    private static bool IsBead(GameItem item)

    {

        try { return item != null && item.IsTag(BACKPACK_TAG); }

        catch { return false; }

    }



    private static bool IsJunk(GameItem item)

    {

        try { return item != null && item.identifier == MATERIAL_ID; }

        catch { return false; }

    }



    private static void SetTagInt(GameItem item, string tag, int value)

    {

        try

        {

            item.EnableTag(tag, true);

            System.Action<TagState> del = st => st.SetInt(value);

            var converted = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(del);

            item.ModifyTag(tag, converted, false);

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] 写标签失败: " + ex.Message); }

    }



    private static int GetTagInt(GameItem item, string tag)

    {

        try

        {

            if (item == null || !item.IsTag(tag)) return 0;

            var ts = item.GetTagReadonly(tag);

            return (ts != null) ? ts.GetInt() : 0;

        }

        catch { return 0; }

    }

}

