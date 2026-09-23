using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class LuckScoutPerk : CustomStartingPerk
{
    internal static void TryGiveKit()

    {

        try

        {

            // 首次触发=新档：清零计数（区分过天——过天时 _initialItemHandled 已为 true）

            if (!_initialItemHandled)

            {

                _initialItemHandled = true;

                FullReset();


            }

            if (!IsActive()) return;

            if (_kitGiven) return; // 本次运行已给，防重复
            // 【09-13 多刷根治】玩家库存已有虚空珠储物袋（任意位置：背包/容器/柜台）→ 视为已发放，不再创建新珠
            if (LuckScoutBackpackUpgrade.HasAnyVoidBeadStorage())
            {
                _kitGiven = true;
                return;
            }



            EmporiumEntry emporium = EmporiumEntry.Instance;

            if (emporium == null || emporium.backInvinvElement == null)
            {
                Core.LogMsg("[捡漏直觉] EmporiumEntry未就绪，入队重试");
                _pendingGive = true;
                _pendingGiveFrames = 600;
                return;
            }



            // 【三样独立发放】工具箱(原版toolbox) + 加强探测器 + 虚空珠(可升级储物)

            // 都作为独立物品放入玩家后背包，探测器不再塞进工具箱（原版toolbox是槽位式，塞入不可靠）

            int given = 0;



            // 1. 原版工具箱

            GameItem kit = CreateToolbox(null);

            if (kit != null)

            {

                try { kit.DisableTag("not_purchased", true); kit.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                emporium.backInvinvElement.TryFindOneValidInventorySlot(kit, false);

                if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(kit))

                {

                    emporium.TransferOwnershipBackInv();

                    emporium.TransferOwnedItemBackToInv();

                    _kitOk = true;
                    given++;


                }

                else Core.LogMsg("[捡漏直觉] 工具箱添加到后背包失败");

            }

            else Core.LogMsg("[捡漏直觉] 工具箱创建失败（原版toolbox）");



            // 2. 加强探测器（独立发放）

            GameItem scanner = CreateEnhancedScanner();

            if (scanner != null)

            {

                try { scanner.DisableTag("not_purchased", true); scanner.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                emporium.backInvinvElement.TryFindOneValidInventorySlot(scanner, false);

                if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(scanner))

                {

                    emporium.TransferOwnershipBackInv();

                    emporium.TransferOwnedItemBackToInv();

                    _scannerOk = true;
                    given++;


                }

                else Core.LogMsg("[捡漏直觉] 探测器添加到后背包失败");

            }

            else Core.LogMsg("[捡漏直觉] 探测器创建失败");



            // 3. 虚空珠（可升级便携储物，20x10=200格，初始1格，拖垃圾升级）

            GameItem bag = LuckScoutBackpackUpgrade.CreateScrollableScavBackpack();

            if (bag != null)

            {

                try { bag.DisableTag("not_purchased", true); bag.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                emporium.backInvinvElement.TryFindOneValidInventorySlot(bag, false);

                if (((GameInventory)emporium.backInvinvElement).UncheckedAccept(bag))

                {

                    emporium.TransferOwnershipBackInv();

                    emporium.TransferOwnedItemBackToInv();

                    _beadOk = true;
                    given++;


                }

                else Core.LogMsg("[捡漏直觉] 虚空珠添加到后背包失败");

            }

            else Core.LogMsg("[捡漏直觉] 虚空珠创建失败");



            if (_kitOk && _scannerOk && _beadOk)
            {
                _kitGiven = true;
                _pendingGive = false;
            }
            else
            {
                _pendingGive = true;
                _pendingGiveFrames = 600;
                Core.LogMsg("[捡漏直觉] 发放未全成功（工具箱=" + _kitOk + " 探测器=" + _scannerOk + " 虚空珠=" + _beadOk + "），入队重试");
            }


        }

        catch (Exception ex)

        {

            var inner = ex.InnerException ?? ex;

            Core.LogMsg("[捡漏直觉] TryGiveKit异常: " + inner.Message + "\n" + inner.StackTrace);

        }

    }
    private static GameItem CreateToolbox(GameItem scanner)

    {

        // 【用户需求】用游戏原版 toolbox（拾荒者工具箱），不是自定义"蛙哥妙妙箱"。

        // 原版 toolbox 是槽位式工具容器（ToolboxHelper.InitToolbox），通过 DirectoryMaster.Item 创建。

        // 探测器不再放入工具箱（方案A：三样独立发放），scanner 参数保留仅用于签名兼容。

        try

        {

            GameItem kit = DirectoryMaster.Item("toolbox", true);

            if (kit == null)

            {

                Core.LogMsg("[捡漏直觉] 原版 toolbox 创建失败，退回自定义箱子");

                kit = CustomStorageContainer.CreateContainer();

            }

            if (kit == null) { Core.LogMsg("[捡漏直觉] 工具箱创建失败"); return null; }

            try { kit.DisableTag("not_purchased", true); kit.DisableTag("TAG_NOT_PURCHASED", true); } catch { }


            return kit;

        }

        catch (Exception ex)

        {

            Core.LogMsg("[捡漏直觉] CreateToolbox异常: " + ex.Message);

            return null;

        }

    }
    private static GameItem CreateBigBackpack()

    {

        // 【用户需求】用游戏原版普通大背包 backpack_large（不是军用背包 backpack_large_military）

        try

        {

            GameItem bag = DirectoryMaster.Item("backpack_large", true);

            if (bag == null)

            {

                Core.LogMsg("[捡漏直觉] 原版 backpack_large 创建失败，退回自定义背包");

                bag = CustomStorageContainer.CreateContainer();

                if (bag != null)

                {

                    try

                    {

                        if (CustomStorageContainer.LastCreatedInventory is GameGridInventory gridInv)

                        {

                            gridInv.SetShape(6, 3);

                            gridInv.Validate();

                            gridInv.identifier = "luck_scout_big_backpack";

                        }

                    }

                    catch (Exception ex) { Core.LogMsg("[捡漏直觉] 设置大背包容量失败: " + ex.Message); }

                }

            }

            if (bag == null) { Core.LogMsg("[捡漏直觉] 大背包创建失败"); return null; }

            try { bag.DisableTag("not_purchased", true); bag.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

            // 标记为捡漏直觉可升级背包（LuckScoutBackpackUpgrade 识别）

            try { bag.EnableTag("LUCK_SCOUT_BACKPACK_TAG", true); } catch { }


            return bag;

        }

        catch (Exception ex) { Core.LogMsg("[捡漏直觉] CreateBigBackpack异常: " + ex.Message); return null; }

    }
    private static GameItem CreateEnhancedScanner()

    {

        try

        {

            GameItem scanner = DirectoryMaster.Item("metal_scanner", true);

            if (scanner == null) { Core.LogMsg("[捡漏直觉] metal_scanner 创建失败"); return null; }

            scanner.EnableTag(SCANNER_TAG, true); // 原版双倍只看标签存在，无需写值

            return scanner;

        }

        catch (Exception ex) { Core.LogMsg("[捡漏直觉] CreateEnhancedScanner异常: " + ex.Message); return null; }

    }
}
