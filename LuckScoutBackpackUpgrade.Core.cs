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
partial class LuckScoutBackpackUpgrade


{
    // ===== Core =====



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
}
