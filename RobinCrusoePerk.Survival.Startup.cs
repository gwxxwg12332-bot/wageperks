using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
internal static partial class RobinCrusoePerk
{

    // ===== 开局（HandleInitialItem Postfix 调用）=====
    internal static void TrySetupNewRun()
    {
        try
        {
            if (!IsActive()) return;
            ClearMemBlood(); // 09-26 修：新档清血量内存缓存（GetBlood 短路缓存防上一档血量串档；下方 SetInt blood=6000 后回读正常满血）
            WandererPerk.ClearBackpack(); // 09-20 设计稿：清原版发放（invElement+dossier）——先清后发，防误清自己物资
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 原生困难模式开关（开局界面）
            WageSaveStore.SetInt(PERK_ID, "robinson_hard", hard ? 1 : 0); // 随档（原生 hardMode 退出重进重置，存 mod 状态）
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null)
            {
                // 09-20 设计稿：金钱随机——hard 清零；普通 50%→0 / 50%→1~600（原固定 360）
                if (hard) ps.playerCash = 0;
                else if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
                else ps.playerCash = Core.Rng.Next(1, 601);
            }

            // 09-21 物资发放移到 PostfixStartNewGame（StartNewGame 在原版发放之后触发：先清原版 4 件+文档再发，根治清太早）
            // HardMode：无开局物资

            WageSaveStore.SetInt(PERK_ID, "sat", 100);        // v5.7 三状态初始
            WageSaveStore.SetInt(PERK_ID, "thirst", 100);
            WageSaveStore.SetInt(PERK_ID, "health", 100);
            WageSaveStore.SetInt(PERK_ID, "blood", BLOOD_MAX); // 卖血：开局满血 6000ml（09-17）
            WageSaveStore.SetInt(PERK_ID, "mood", MOOD_START);
            WageSaveStore.SetInt(PERK_ID, "granary", 0);
            WageSaveStore.SetInt(PERK_ID, "elevStreak", 0);
            WageSaveStore.SetInt(PERK_ID, "elevCount", 0);
            WageSaveStore.SetInt(PERK_ID, "starveDays", 0);
            WageSaveStore.SetInt(PERK_ID, "thirstDeath", 0);
            WageSaveStore.SetInt(PERK_ID, "critDays", 0);
            WageSaveStore.SetInt(PERK_ID, "clean", CLEAN_START);   // 新三状态初始（用户拍板）
            WageSaveStore.SetInt(PERK_ID, "sleep", SLEEP_START);
            WageSaveStore.SetInt(PERK_ID, "social", SOCIAL_START);
            WageSaveStore.SetString(PERK_ID, "nodeKey", "");       // v5.8-8 节点池：开局清空，首次打烊抽取
            WageSaveStore.SetInt(PERK_ID, "nodeFxIdx", -1);
            WageSaveStore.SetInt(PERK_ID, "deals", 0);             // 接待计数
            SyncRentDisplay(); // 开局第一天就同步租金显示字段（拆包：日历读 dayUntilRent+rentValue）
            RefreshStatusPanel(); // 开局建常驻面板
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] TrySetupNewRun 异常: " + ex.Message); }
    }

    // ===== 09-21 开局物资：PlayerStore.StartNewGame Postfix（发放后清——原版 4 件+文档在 StartNewGame 发放，HandleInitialItem 清太早白清）=====
    internal static void PostfixStartNewGame()  // PlayerStore.StartNewGame Postfix（Core.cs 注册）
    {
        try
        {
            if (!IsActive()) return;
            WandererPerk.ClearBackpack(); // 清原版发放（magnifier/labeler/topical_bandage_item/fanny_pack + dossier 文档）
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 09-20 修：不依赖PerkStatePersistence时序——直接读原生开局开关（偶尔StartNewGame先跑导致读旧档残留0）
            if (!hard) GiveStartingGoods();
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixStartNewGame 异常: " + ex.Message); }
    }

    private static void GiveStartingGoods()
    {
        GiveToBackpack("processed_meat", 3);      // 口粮×3（三天量）
        GiveToBackpack("raw_meat", 2);            // 大肉×2（用户拍板 09-09：另加生肉）
        GivePureWaterToBackpack(3);               // 大瓶纯水×3（三天量）
        GiveToBackpack("bandage_item", 5);        // 绷带×5（bandage_item 正确 id）
    }

    private static void GiveToBackpack(string id, int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) continue;
                // 重叠bug修复（用户拍板 09-09：参考 QuickItemSpawner F3 先例）：
                // 正确链 = TryFindOneValidInventorySlot(item) → slot.TryAcceptOnce()（slot 持有格子坐标，真正落格）；
                // 之前丢弃 slot 直接 UncheckedAccept → 不设坐标 → 同格重叠。TryAcceptOnce 失败才 UncheckedAccept 兜底。
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(item);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
    }

    private static void GivePureWaterToBackpack(int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            var inv = (GameInventory)em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 09-20 设计稿：直接生成带水大瓶（删 DirectoryMaster.Item+AddWater 链——工厂产物 AddWater 静默失败 → 空瓶）
                GameItem spawn = item;
                try { spawn.DisableTag("stolen", true); } catch { }
                // 同 GiveToBackpack：slot.TryAcceptOnce 真正落格，防重叠
                var slot = em.backInvinvElement.TryFindOneValidInventorySlot(spawn, false);
                if (slot != null) { try { slot.TryAcceptOnce(); continue; } catch { } }
                inv.UncheckedAccept(spawn);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
    }

    // 09-11 用户确认：取消"前3天无随机客户"设定（PrefixHandleNormalClient/AugClient/AnyClient 已删，只保留次要客户永久拦截）
    public static bool PrefixHandleMinorClient()
    {
        // 09-10 用户拍板：次要客户（拾荒客/上层医生等）永久删掉，不限前3天
        try { if (IsActive()) return false; }
        catch (System.Exception ex) { Core.LogMsg("[RobinCrusoePerk.Survival] 异常: " + ex.Message); }
        return true;
    }

}
