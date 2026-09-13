using System;

using System.Reflection;

using Il2Cpp;

using Il2CppInterop.Runtime;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;



// ============================================================

// 捡漏直觉（重做版）：拾荒者工具箱 + 加强金属探测器

// 机制（全部复用原版拾荒链路，不自造新系统）：

//   1. 开局给"拾荒者工具箱"（5格便携容器，内含"加强金属探测器"）

//   2. 打烊外出拾荒次数 +2（Patch ScavHelper.GetMaxScavAttempts，与原版 ProficientScavenger 叠加）

//   3. 持有加强探测器 → 稀有物掉落概率提升（SCAV_SCANNER_DOUBLE_CHANCE 标签，

//      原版 ScavengeDumpingGrounds 自动识别，无需 Patch 掉落逻辑）

//   4. 拾荒累计升级 → 稀有率逐步提升（每 3 次拾荒升 1 级，标签值 +5/级，上限 100）

// 持久化：PerkStatePersistence（带 runID，读档不丢）

// ============================================================

internal sealed class LuckScoutPerk : CustomStartingPerk

{

    internal const string PerkId = "捡漏直觉";



    internal override string Id => PerkId;

    internal override string DisplayName => LangHelper.T("捡漏直觉", "Luck Scout");

    internal override string Description => LangHelper.T(

        "你天生对破烂里的宝贝有直觉。开局获得拾荒者工具箱、加强金属探测器和虚空珠（可升级便携储物）；稀有物掉落概率提升，随拾荒次数增长而逐步增强。",

        "Innate intuition for treasures. Start with a scavenger toolbox, enhanced metal detector and Void Bead (upgradable storage); improved rare drops that grow with scavenging.");

    internal override int Cost => 5;

    internal override int Type => 0;



    private const string NS = "luck_scout";



    // 计数方案：静态变量（全场景可读，不依赖探测器是否在当前场景）

    // + PlayerPrefs 固定 key（持久化，读档/过天后懒恢复）

    // + 回主菜单/开新档时清除（QuitToMenu/OnMainMenu/NewGame Patch）

    private const string PK_SCAV = "WAGES_LUCK_SCAV_COUNT";

    private const string PK_LEVEL = "WAGES_LUCK_SCAV_LEVEL";

    private const string PK_RARE = "WAGES_LUCK_RARE_COUNT";

    private const string PK_FIRST = "WAGES_LUCK_FIRST_RARE_DONE";



    private static int _scavCount = -1;  // -1 表示未从 PlayerPrefs 恢复

    private static int _scavLevel = -1;

    private static int _rareCount = -1;

    private static int _firstRareDone = -1;

    private static bool _initialItemHandled = false;  // HandleInitialItem 首次触发标记（区分新档vs过天）



    // 懒恢复：第一次读取时从 PlayerPrefs 恢复

    private static int GetCount(string key, ref int cache)

    {

        if (cache < 0)

        {

            try { cache = PlayerPrefs.GetInt(key, 0); } catch { cache = 0; }

        }

        return cache;

    }



    // 只更新静态变量缓存，不写 PlayerPrefs——避免未保存的拾荒被持久化

    // 真正持久化在 SaveGame Postfix 里调用 SaveCountToPlayerPrefs()

    private static void SetCount(string key, int val, ref int cache)

    {

        cache = val;

    }



    // 游戏保存时调用：把静态变量写入 PlayerPrefs 并持久化

    private static void SaveCountToPlayerPrefs()

    {

        try

        {

            if (_scavCount >= 0) PlayerPrefs.SetInt(PK_SCAV, _scavCount);

            if (_scavLevel >= 0) PlayerPrefs.SetInt(PK_LEVEL, _scavLevel);

            if (_rareCount >= 0) PlayerPrefs.SetInt(PK_RARE, _rareCount);

            if (_firstRareDone >= 0) PlayerPrefs.SetInt(PK_FIRST, _firstRareDone);

            PlayerPrefs.Save();


        } catch (Exception ex) { Core.LogMsg("[捡漏直觉] SaveCount持久化失败: " + ex.Message); }

    }



    // SaveGame Postfix：游戏保存时持久化计数

    public static void PostfixSaveGame()

    {

        try { if (IsActive()) SaveCountToPlayerPrefs(); } catch { }

    }



    private static int GetScavCount() { return GetCount(PK_SCAV, ref _scavCount); }

    private static void SetScavCount(int v) { SetCount(PK_SCAV, v, ref _scavCount); }

    private static int GetScavLevel() { return GetCount(PK_LEVEL, ref _scavLevel); }

    private static void SetScavLevel(int v) { SetCount(PK_LEVEL, v, ref _scavLevel); }

    private static int GetRareCount() { return GetCount(PK_RARE, ref _rareCount); }

    private static void SetRareCount(int v) { SetCount(PK_RARE, v, ref _rareCount); }

    private static int GetFirstRare() { return GetCount(PK_FIRST, ref _firstRareDone); }

    private static void SetFirstRare(int v) { SetCount(PK_FIRST, v, ref _firstRareDone); }

    private const string KEY_GIVEN = "kit_given";    // 工具箱已给（读档防重）

    private const string KEY_SCAVS = "scav_count";   // 累计拾荒次数

    private const string KEY_LEVEL = "level";        // 探测器等级

    private const string KEY_RARE = "rare_count";    // 累计稀有物掉落数（探测器面板展示）


    private const string SCANNER_TAG = "SCAV_SCANNER_DOUBLE_CHANCE";

    private const int BASE_CHANCE = 0;               // 初始稀有率（%）从0开始

    private const int UPGRADE_CHANCE = 2;            // 每级 +2（放慢升级节奏）

    private const int MAX_CHANCE = BuildConfig.HARD_MODE ? 50 : 25;   // 稀有物发现几率上限（标准版25%；硬爽版50%，09-12 用户拍板）

    private const int LEVELUP_EVERY = 15;             // 每拾荒 15 次升 1 级（目标更漫长）



    internal static bool IsActive() => Core.PerkActive(PerkId);



    // 本次运行是否已发工具箱（参照蛙哥妙妙箱：静态标志防进程内重复）

    // 不用 PlayerPrefs 持久化防重——之前 KEY_GIVEN 残留 true 会把新档工具箱误拦（根因之一）。

    // HandleInitialItem 只在开新档调用（读档走 LoadGame 分支），天然不会多刷。

    private static bool _kitGiven = false;

    // 09-13 修复：新档开始时重置发放标记（Patches.HandleInitialItemPostfix 调用）
    internal static void ResetGiveFlag()
    {
        _kitGiven = false;
    }
    // ===== 开局发放重试（09-12）：emporium 未就绪/部分失败 → 帧轮询补发 =====
    private static bool _pendingGive = false;
    private static int _pendingGiveFrames = 0;
    private static bool _kitOk = false;      // 工具箱发放成功
    private static bool _scannerOk = false;  // 探测器发放成功
    private static bool _beadOk = false;     // 虚空珠发放成功

    public static void OnUpdateGiveRetry()
    {
        try
        {
            if (!_pendingGive) return;
            if (_pendingGiveFrames-- <= 0) { _pendingGive = false; return; } // 超时放弃
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null) return; // 未就绪继续等
            TryGiveKit();
        }
        catch { }
    }



    internal override void OnNewGame()

    {

        ResetState();

    }



    // 静态重置：开新档时清所有计数（HandleInitialItemPostfix / OnNewGame 双保险调用）

    // 回主菜单时调用：只重置静态变量缓存为-1（触发下次懒恢复），不清除 PlayerPrefs

    // 这样加载存档后计数从 PlayerPrefs 恢复，不会丢失

    internal static void ResetState()

    {

        _kitGiven = false;

        _scavCount = -1; _scavLevel = -1; _rareCount = -1; _firstRareDone = -1;

        _initialItemHandled = false;


    }



    // 开新档时调用：清除 PlayerPrefs + 重置缓存，计数从0开始

    internal static void FullReset()

    {

        _kitGiven = false;

        _scavCount = 0; _scavLevel = 0; _rareCount = 0; _firstRareDone = 0;

        try { PlayerPrefs.DeleteKey(PK_SCAV); PlayerPrefs.DeleteKey(PK_LEVEL);

              PlayerPrefs.DeleteKey(PK_RARE); PlayerPrefs.DeleteKey(PK_FIRST); PlayerPrefs.Save(); } catch { }


    }







    // ============ 开局给物（Patches.HandleInitialItemPostfix 调用，与蛙哥妙妙箱/拾荒者信物同机制） ============

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



    // ============ 工具箱创建（5×1=5格便携容器，参考 satchel + CustomStorageContainer） ============

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



    // ============ 大背包创建（第三样，6×3=18格自定义尺寸容器，总指挥方案） ============

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



    // ============ 加强探测器：原版 metal_scanner + SCAV_SCANNER_DOUBLE_CHANCE 标签 ============

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



    // 【09-10 已废弃】mod 稀有率改纯等级制，不再写原版标签；原版双倍只看标签存在
    private static void SetScannerChance(GameItem scanner, int chance)

    {

    }



    // ============ Patch: 拾荒次数 +2（与原版叠加） ============

    // 【拆包结论】实际剩余次数由 GetScavTimeLeft 决定（GetMaxScavAttempts 的逻辑复制 + 减去

    // PlayerStore 0x218 已用次数），它不调用 GetMaxScavAttempts——所以两个都必须 Patch，

    // 否则 +2 只影响"最大次数"不影响"实际可拾荒次数"。

    public static void PostfixGetMaxScavAttempts(ref int __result)

    {

        try

        {

            // 09-10 用户拍板：捡漏直觉不再加拾荒次数（任何职业；此前 +10 有缩减 bug）
            // 09-12 硬爽版：加回 +10（仅非鲁滨逊职业；鲁滨逊走 GetScavCap 已含，防双 Postfix 叠加）
            if (BuildConfig.HARD_MODE && IsActive() && !RobinCrusoePerk.IsActive())
                __result += 10;

        }

        catch { }

    }



    public static void PostfixGetScavTimeLeft(ref int __result)

    {

        try

        {

            // 09-10 用户拍板：捡漏直觉不再加拾荒次数（任何职业）
            // 09-12 硬爽版：加回 +10（GetScavTimeLeft=实际可拾荒次数读口，须同步；非鲁滨逊防双加）
            if (BuildConfig.HARD_MODE && IsActive() && !RobinCrusoePerk.IsActive())
                __result += 10;
            if (false) { }

        }

        catch { }

    }



    // ============ Patch: CanScavenge Prefix → 实际拾荒闸门（根因修复） ============

    // 【拆包结论】ScavengeDumpingGrounds 开头调用 CanScavenge 做实际判定，

    // 它内联复刻 GetMaxScavAttempts 逻辑（基础5/7 - 周数，下限3），不调用

    // GetMaxScavAttempts/GetScavTimeLeft。只 patch 显示层不改 CanScavenge →

    // "显示14次但实际只能5次"。此处 IsActive 时短路直接用 GetScavTimeLeft（已被+14），

    // 让显示=实际判定。

    private static bool _scavengeAllowedThisCall = false;



    // 记录本次 CanScavenge 调用前玩家是否受伤（Prefix 记录，Postfix 用）

    // 用于区分原版返回 false 的原因：受伤 vs 次数不足



    // Prefix：记录受伤状态，不短路（让原版执行受伤判定+次数判定）

    // 原实现短路原版只检查次数，丢掉了受伤判定 → 受伤后仍可拾荒（bug）

    public static void PrefixCanScavenge()

    {

        if (!IsActive()) return;

        // 不再额外判定受伤——原版 CanScavenge 已基于受伤免疫次数处理，

        // 额外加 woundState/isWoundedFresh 判定会导致治疗后仍无法拾荒。

        try

        {

            var ps = Il2Cpp.PlayerStore.Instance;

            if (ps != null && ps.healthData != null)

            {

                try { } catch { }

            }

        }

        catch { }

    }



    // Postfix：修改次数判定（受伤判定由原版保留）

    // 如果没受伤且原版因次数不足返回 false，但 GetScavTimeLeft() > 0（已 Patch +14），改成 true

    public static void PostfixCanScavenge(ref bool __result)

    {

        if (!IsActive() || RobinCrusoePerk.IsActive()) return;

        try { } catch { }

        // 修正：无伤时原版可能因受伤免疫次数耗尽返回 false，改成 true

        // 受伤时完全交给原版判定（治疗后恢复免疫次数即可拾荒）

        if (!__result && ScavHelper.GetScavTimeLeft() > 0)

        {

            try

            {

                var ps = PlayerStore.Instance;

                if (ps != null)

                {

                    var health = ps.healthData;

                    bool isWounded = (health != null && health.woundState > 0);

                    bool isStable = (health != null && health.isWoundStable);

                    // 无伤 或 伤口已稳定（治疗过）都允许拾荒

                    if (!isWounded || isStable)

                    {

                        __result = true;


                    }

                    else

                    { }

                }

            } catch { }

        }

    }



    // 记录本次 ScavengeDumpingGrounds 调用是否真正允许拾荒（Prefix 先于方法体执行）

    public static void PrefixScavengeDumpingGrounds()

    {

        _scavengeAllowedThisCall = ScavHelper.CanScavenge();

    }



    // ============ Patch: ScavengeDumpingGrounds Postfix → 拾荒计数 + 升级 ============

    public static void PostfixScavengeDumpingGrounds()

    {

        try

        {

            if (!IsActive()) return;

            if (!_scavengeAllowedThisCall) return;  // 非真正拾荒（CanScavenge false 提前 return）不计数

            int count = GetScavCount() + 1;

            SetScavCount(count);

            int level = GetScavLevel();

            int newLevel = count / LEVELUP_EVERY;

            if (newLevel > level)

            {

                SetScavLevel(newLevel);


            }

        }

        catch (Exception ex) { Core.LogMsg("[捡漏直觉] 拾荒计数失败: " + ex.Message); }

    }



    // 回主菜单/开新档时重置计数（静态变量缓存 + PlayerPrefs）

    public static void PostfixQuitToMenu() { try { ResetState();  } catch { } }

    public static void PostfixOnMainMenu() { try { ResetState();  } catch { } }

    public static void PostfixNewGame() { try { FullReset(); } catch { } }



    // ============ Patch: GetRandomScavengedItem Postfix → 稀有物概率追加 ============

    // 【拆包结论 recvubNSdZ1JvE】原版拾荒掉物只走 dumpingGroundTG，SCAV_SCANNER_DOUBLE_CHANCE

    // 标签只是"双倍再送1件"，不是稀有率。稀有率需 Patch GetRandomScavengedItem 追加稀有物：

    // 判定持有加强探测器 + 读探测器标签概率 → 追加价值池稀有物（已解锁物品）。

    // v1.1.3 合规：移除废弃机器/rare_electronic（官方未发布内容，制作组要求）。

    public static void PostfixGetRandomScavengedItem(Il2CppSystem.Collections.Generic.List<GameItem> __result)

    {

        try

        {

            if (!IsActive()) return;

            if (__result == null) return;

            GameItem scanner = FindScannerInOwned();

            if (scanner == null) return;

            int chance = Math.Min(MAX_CHANCE, GetScannerChance(scanner)); // 旧档标签可能>10，读取处clamp

            if (chance <= 0) return;



            // 首次拾荒必出标记（KEY_FIRST_RARE=0 且未触发过 → 必出；触发后置 1）

            bool rolled = (Core.Rng.NextDouble() * 100.0 < chance);
            if (!rolled) return;
            // 稀有物（价值池，已解锁物品）——不设首趟必出
            GameItem rare = CreateRareItem();
            if (rare != null)
            {
                __result.Add(rare);
                try { RecordRareDrop(1); } catch { }
                string rid = "?"; try { rid = rare.identifier ?? "?"; } catch { }
            }


        }

        catch { }

    }



    // v1.1.3 合规：废弃机器（CreateBrokenAlarm/Furnace/MoistureFarm）已移除，

    // 属官方未发布内容（制作组要求）。



    // 读探测器 SCAV_SCANNER_DOUBLE_CHANCE 标签值（双倍概率 + 稀有率共用概率载体）

    // 读探测器稀有率：完全由拾荒等级决定，不再读共享标签 SCAV_SCANNER_DOUBLE_CHANCE
    // 【09-10 用户拍板】原版电子零件升级不再影响 mod 稀有率（原版升级只走原版双倍机制）
    private static int GetScannerChance(GameItem scanner)

    {

        return GetCurrentChance();

    }



    // 【探测器面板统计】记录累计稀有物掉落数（FlowScavenge 检测到稀有物时调用，持久化）

    internal static void RecordRareDrop(int count)

    {

        try

        {

            if (!IsActive() || count <= 0) return;

            int total = GetRareCount() + count;

            SetRareCount(total);


        }

        catch { }

    }



    // 【探测器面板展示】Patch ScavHelper.CreateTooltip(RichTextBuilder, GameItem) Postfix

    // 在金属探测器 Tooltip 上追加：当前稀有率 + 累计稀有物掉落数（方案C：概率+统计都显示）

    public static void PostfixCreateTooltip(RichTextBuilder builder, GameItem item)

    {

        try

        {

            if (!IsActive()) return;

            if (builder == null || item == null) return;

            string id = "?"; try { id = item.identifier ?? "?"; } catch { }

            if (id != "metal_scanner") return;

            if (!item.IsTag(SCANNER_TAG)) return; // 非加强探测器不加



            int chance = GetScannerChance(item);

            int total = GetRareCount();

            int scavs = GetScavCount();

            // RichTextBuilder 无 Append：用 AddLine 追加（cheatsheet 1186）

            builder.AddLine(LangHelper.T("◆ 捡漏直觉（稀有物强化）", "◆ Luck Scout (Rare Loot Boost)"), bold: true);

            builder.AddLine(LangHelper.T("稀有掉落率：", "Rare drop rate: ") + chance + "%" + (chance >= MAX_CHANCE ? LangHelper.T("（已满级）", " (MAX)") : ""));

            builder.AddLine(LangHelper.T("累计拾荒：", "Total scavenges: ") + scavs + LangHelper.T(" 次 | 累计稀有物：", " | Rare finds: ") + total + LangHelper.T(" 件", ""));

        }

        catch { }

    }



    // 稀有物价值池（用户确认：价值≥200，排除容器/机器/家具）

    // 数据源 all_item_values_359_dump.txt，筛选后 28 个（材料/模块/武器/高级消耗品）
    // 09-12 用户拍板：硬爽版把神经模组加回稀有均匀池（标准版保持移出，防叠加）
    private static readonly string[] RARE_VALUE_POOL = BuildRarePool();

    private static string[] BuildRarePool()
    {
        var list = new System.Collections.Generic.List<string> {
            // 500
            "skincare_cream",
            // 350
            "black_injector", "desequencer", "crypto_module_cmd", "module_extractor_advanced",
            // 325
            "bottled_water_premium",
            // 310
            "shotgun",
            // 300
            "turbo_booster_adv", "advanced_flux_agent", "crypto_module_sec",
            // 280
            "smg",
            // 250
            "crypto_module_med", "crypto_module_eng", "chem_module",
            "c4", "stun_gun", "blue_blood_bag", "wine_yeast_infinite", "metal_scanner", "c4_set",
            // 200
            "surgery_tool", "pheromone_perfume", "crypto_module_sup", "crypto_module_ser", "glock_receiver"
        };
        if (BuildConfig.HARD_MODE)
        {
            list.Add("system_capped_neural_core");     // 受限神经模组（硬爽版加回均匀池；09-13 用户拍板：未受限全删）
        }
        return list.ToArray();
    }



    // 稀有物：从价值池随机（价值≥200，排除容器/机器/家具），创建失败自动换下一个
    // 09-12 用户拍板：神经模组独立概率——未受限 0.5% / 受限 2%（标准版，已移出均匀池，防叠加）
    // 09-12 硬爽版：神经模组加回均匀池，删独立 roll（单渠道）
    private static GameItem CreateRareItem()
    {
        try
        {
            // 神经模组独立 roll（仅标准版；未命中/创建失败回落原池；09-13 用户拍板：未受限已全删，仅剩受限 2%）
            if (!BuildConfig.HARD_MODE)
            {
                double nr = Core.Rng.NextDouble();
                string neuralId = null;
                if (nr < 0.02) neuralId = "system_capped_neural_core";
                if (neuralId != null)
                {
                    try
                    {
                        GameItem neural = DirectoryMaster.Item(neuralId, true);
                        if (neural != null)
                        {
                            try { neural.DisableTag("not_purchased", true); neural.DisableTag("TAG_NOT_PURCHASED", true); } catch { }
                            return neural;
                        }
                    }
                    catch { }
                }
            }

            // 从随机起点尝试最多 5 个候选，避免个别物品创建失败导致掉落为空
            int start = Core.Rng.Next(RARE_VALUE_POOL.Length);

            for (int attempt = 0; attempt < Math.Min(5, RARE_VALUE_POOL.Length); attempt++)

            {

                string id = RARE_VALUE_POOL[(start + attempt) % RARE_VALUE_POOL.Length];

                GameItem item = null;

                try { item = DirectoryMaster.Item(id, true); } catch { }

                if (item == null) continue;

                try { item.DisableTag("not_purchased", true); item.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                return item;

            }

            // 全部失败兜底：原稀有物

            try

            {

                var fallback = DirectoryMaster.Item("skincare_cream", true);

                if (fallback != null) { try { fallback.DisableTag("not_purchased", true); } catch { } }

                return fallback;

            }

            catch { return null; }

        }

        catch { return null; }

    }



    private static int GetCurrentChance()

    {

        int level = GetScavLevel();

        return Math.Min(MAX_CHANCE, BASE_CHANCE + level * UPGRADE_CHANCE);

    }



    // 找到玩家的加强探测器（可能在工具箱内或直接放背包）

    private static GameItem FindPlayerScanner()

    {

        return FindScannerInOwned();

    }



    // 【防递归】FindScannerInOwned 内部调用被 Patch 的 GetAllAfterhourOwnedItems，

    // 而 Postfix 又调 FindScannerInOwned → 无保护会无限递归栈溢出。此标志切断递归链。

    private static bool _inAfterhourScan = false;



    // ============ Patch: GetAllAfterhourOwnedItems Postfix ============

    // 【拆包结论】原版 ScavengeDumpingGrounds 用 IsAfterhourHaveOwnedItems("metal_scanner")

    // + GetAfterhourItemById 检查探测器，二者都基于 GetAllAfterhourOwnedItems() 遍历

    // 打烊背包直接子物品（非递归）。探测器嵌套在工具箱里原版找不到 → 双倍掉落失效。

    // 此处把工具箱内的加强探测器补进返回列表，一处 Patch 让原版两个检查全通。

    public static void PostfixGetAllAfterhourOwnedItems(Il2CppSystem.Collections.Generic.List<GameItem> __result)

    {

        try

        {

            if (!IsActive()) return;

            if (__result == null) return;

            if (_inAfterhourScan) return; // 重入（FindScannerInOwned 内部调用）→ 跳过，防递归

            GameItem scanner = FindScannerInOwned(); // 标志在内部管理

            if (scanner == null) return;

            for (int i = 0; i < __result.Count; i++)

                if (__result[i] == scanner) return; // 已含，防重复

            __result.Add(scanner);

        }

        catch { }

    }



    // 在打烊持有物品里找加强探测器（工具箱内部 / 直接持有）

    // 标志在此方法内部管理：进入设置、finally 清除；内部调用 GetAllAfterhourOwnedItems

    // 会触发 Postfix → Postfix 见 _inAfterhourScan=true 直接跳过 → 递归被切断。

    private static GameItem FindScannerInOwned()

    {

        if (_inAfterhourScan) return null;

        _inAfterhourScan = true;

        try

        {

            foreach (GameItem it in EmporiumEntry.Instance.GetAllAfterhourOwnedItems())

            {

                if (it == null) continue;

                try { if (it.identifier == "luck_scout_toolbox") { var s = FindScannerInContainer(it); if (s != null) return s; } } catch { }

                try { if (it.identifier == "metal_scanner" && it.IsTag(SCANNER_TAG)) return it; } catch { }

            }

        }

        catch { }

        finally { _inAfterhourScan = false; }

        return null;

    }



    // 运行时从容器拿内部库存（仅查找场景用；创建场景用 LastCreatedInventory，不走这里）

    private static GameInventory GetToolboxInventory(GameItem kit)

    {

        try

        {

            var w = kit.contentWindow;

            if (w == null) return null;

            var p = w.GetType().GetProperty("inventory", BindingFlags.Public | BindingFlags.Instance);

            return p?.GetValue(w) as GameInventory;

        }

        catch { return null; }

    }



    private static GameItem FindScannerInContainer(GameItem container)

    {

        try

        {

            GameInventory inner = GetToolboxInventory(container);

            if (inner == null || inner.childItems == null) return null;

            for (int i = 0; i < inner.childItems.Count; i++)

            {

                GameItem c = inner.childItems[i];

                if (c == null) continue;

                try { if (c.identifier == "metal_scanner" && c.IsTag(SCANNER_TAG)) return c; } catch { }

            }

        }

        catch { }

        return null;

    }



}

