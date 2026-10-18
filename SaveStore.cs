using System;
using Il2Cpp;

namespace WageSurvival;

// ============================================================
// SaveStore 门面（2026-10-03 阶段C：三副本删除，转发 WageAPI.WageSaveStore，命名空间隔离 SurvivalGlobal）
// 现有调用点零改动——签名与原实现一致（2 参 key）。
// 落盘/读档统一归 WageAPI（SaveGame/EndDay Postfix + LoadGame Postfix + 帧轮询）。
// 独立 PERK_ID="SurvivalGlobal"（派活单要求；旧档 RobinCrusoe→SurvivalGlobal 迁移见 SurvivalMigrate）。
// ============================================================
internal static class SaveStore
{
    internal const string PERK_ID = "SurvivalGlobal";

    internal static int GetInt(string key, int defaultValue = 0)
        => WageAPI.WageSaveStore.GetInt(PERK_ID, key, defaultValue);

    internal static void SetInt(string key, int value)
        => WageAPI.WageSaveStore.SetInt(PERK_ID, key, value);

    internal static string GetString(string key, string defaultValue = "")
        => WageAPI.WageSaveStore.GetString(PERK_ID, key, defaultValue);

    internal static void SetString(string key, string value)
        => WageAPI.WageSaveStore.SetString(PERK_ID, key, value);

    internal static bool GetBool(string key, bool defaultValue = false)
        => WageAPI.WageSaveStore.GetBool(PERK_ID, key, defaultValue);

    internal static void SetBool(string key, bool value)
        => WageAPI.WageSaveStore.SetBool(PERK_ID, key, value);

    internal static float GetFloat(string key, float defaultValue = 0f)
        => WageAPI.WageSaveStore.GetFloat(PERK_ID, key, defaultValue);

    internal static void SetFloat(string key, float value)
        => WageAPI.WageSaveStore.SetFloat(PERK_ID, key, value);

    internal static bool HasKey(string key)
        => WageAPI.WageSaveStore.HasKey(PERK_ID, key);

    // 统一落盘归 WageAPI（priority=0 最后跑）；此处转发幂等（双调无害）
    internal static void Flush()
        => WageAPI.WageSaveStore.Flush();

    // 读档由 WageAPI 统一轮询承载；转发幂等（仅设挂起标志，不重复读文件）
    internal static void LoadGame()
        => WageAPI.WageSaveStore.LoadIfPending();

    // 只设默认值，不覆盖已有值
    internal static void InitDefaults()
    {
        if (!HasKey("sat")) SetInt("sat", 100);
        if (!HasKey("thirst")) SetInt("thirst", 100);
        if (!HasKey("health")) SetInt("health", 100);
        if (!HasKey("mood")) SetInt("mood", 50);
        if (!HasKey("clean")) SetInt("clean", 100);
        if (!HasKey("sleep")) SetInt("sleep", 100);
        if (!HasKey("social")) SetInt("social", 50);
        // 10-18 审阅 R2（《审阅_血量虚值与显示不一致》§4 改动1）：blood 从未初始化 → GetInt 兜底 6000 与"满血"不可区分
        //   （WP 直读字典=虚值满血、WS 缓存=真值=双权威源）。**不加 HasKey**：HasKey 会回退旧层 PlayerPrefs
        //   （WageAPI/WageSaveStore.cs:222-231），旧档 default_run 残留会让判断 true 跳过写入→改动静默失效。
        //   10-18 审阅对照修正（M20/M21）：无条件写会在"读档完成后再调 InitDefaults"时覆盖真实血量（其余 7 键有 HasKey 守卫、
        //   blood 无守卫=唯一覆盖风险点）。对齐 SurvivalBlood.cs:83 模式加 LoadComplete 门控：读档未完成=真实值未就绪，跳过写，
        //   由 GetBlood 物化兜底（:83 !HasKey && LoadComplete）；读档完成=补缺省语义不变。仍不加 HasKey（防旧层回退坑）。
        if (WageAPI.WageSaveStore.LoadComplete) SetInt("blood", SurvivalFood.BLOOD_MAX); // =6000（同程序集 internal const，见 SurvivalBlood.cs:75）
    }

    // 新档：只清自己的命名空间 + 设默认（不碰其他 mod 数据；WagePerks 的 ResetForNewRun 清全量，顺序竞态由读默认兜底）
    // 10-07 #5 修复：鲁滨逊开局逻辑（难度/现金/物资）归 WS 迁移时整体丢失——WP 旧注册死代码（PatchRegistry.cs:76 实锤）+新表无 StartNewGame 条目+WS 未承接
    //   → 鲁滨逊档普通/困难无分别 + 无开局物资。本方法补迁（对齐 WP RobinCrusoePerk.Survival.Startup.cs TrySetupNewRun/PostfixStartNewGame）：
    //   hard 分支（现金 0/50%随机）+ robinson_hard 随档标记 + !hard 发开局物资（3肉+2生肉+3纯水+5绷带）；非鲁滨逊档只清缓存（行为不变）
    internal static void PostfixStartNewGame()
    {
        try
        {
            Core.LogMsg("[WageSurvival] StartNewGame——清 SurvivalGlobal + 设默认值");
            WageAPI.WageSaveStore.ClearNamespace(PERK_ID);
            SurvivalFood.ClearMemBlood(); // 10-05 拆包实锤：血量跨档串值=内存缓存残留（档2继承档1 _memBlood），新档清缓存
            SurvivalFood._pendingSkipDays = 0;
            SurvivalFood._jumping = false;
            SurvivalFood._jumpStartDay = -1;
            InitDefaults();
            // ===== 10-07 #5：鲁滨逊开局难度/现金/物资（非鲁滨逊档只清缓存，行为不变）=====
            if (!SurvivalTrade.IsRobinsonRun()) return;
            bool hard = Il2Cpp.NewGameData.Instance != null && Il2Cpp.NewGameData.Instance.hardMode; // 原生困难模式开关（开局界面）
            WageAPI.WageSaveStore.SetInt(PERK_ID, "robinson_hard", hard ? 1 : 0); // 随档（原生 hardMode 退出重进重置，存 mod 状态）
            PlayerStore ps = PlayerStore.Instance;
            if (ps != null)
            {
                // 金钱随机——hard 清零；普通 50%→0 / 50%→1~600（原固定 360）
                if (hard) ps.playerCash = 0;
                else if (Core.Rng.Next(2) == 0) ps.playerCash = 0;
                else ps.playerCash = Core.Rng.Next(1, 601);
            }
            if (!hard) GiveStartingGoods(); // HardMode：无开局物资
            Core.LogMsg("[WageSurvival] 鲁滨逊开局：hard=" + hard + " 现金=" + (ps != null ? ps.playerCash : -1));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] PostfixStartNewGame 异常: " + ex.Message); }
    }

    // ===== 10-07 #5：鲁滨逊开局物资（对齐 WP GiveStartingGoods——WageItemGrant 统一发放防重叠）=====
    private static void GiveStartingGoods()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            GiveToBackpack("processed_meat", 3);   // 口粮×3（三天量）
            GiveToBackpack("raw_meat", 2);         // 大肉×2
            GivePureWaterToBackpack(3);            // 大瓶纯水×3（三天量）
            GiveToBackpack("bandage_item", 5);     // 绷带×5
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] GiveStartingGoods 异常: " + ex.Message); }
    }
    private static void GiveToBackpack(string id, int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            GameGridInventory inv = em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(id, true);
                if (item == null) continue;
                try { Il2Cpp.GeneralHelper.SetItemOwned(item, true); } catch { }
                WageAPI.WageItemGrant.GrantToInventory(item, inv); // 防重叠契约内置（TryAcceptOnce 优先+UncheckedAcceptAll 兜底）
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] GiveToBackpack 异常: " + ex.Message); }
    }
    private static void GivePureWaterToBackpack(int count)
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null || em.backInvinvElement == null) return;
            GameGridInventory inv = em.backInvinvElement;
            for (int i = 0; i < count; i++)
            {
                GameItem item = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water"); // 直接生成带水大瓶
                if (item == null) continue;
                try { Il2Cpp.GeneralHelper.SetItemOwned(item, true); } catch { }
                try { item.DisableTag("stolen", true); } catch { }
                WageAPI.WageItemGrant.GrantToInventory(item, inv);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival] GivePureWaterToBackpack 异常: " + ex.Message); }
    }
}
