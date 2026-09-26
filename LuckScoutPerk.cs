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

internal sealed partial class LuckScoutPerk : CustomStartingPerk

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



    // 懒恢复：第一次读取时从统一存储层恢复（旧数据在裸 PlayerPrefs 全局键，无 runID 前缀本就串档，做一次性兼容回填）




    // 只更新静态变量缓存 + 统一存储层内存（零 I/O）——避免未保存的拾荒被持久化

    // 落盘由 WageSaveStore.PostfixSaveGame 全局门面在打烊时统一 Flush




    // 游戏保存时调用：把静态变量同步进统一存储层内存（兜底幂等；正常路径 SetCount 已同步）




    // SaveGame Postfix：游戏保存时持久化计数












    private const string KEY_GIVEN = "kit_given";    // 工具箱已给（读档防重）

    private const string KEY_SCAVS = "scav_count";   // 累计拾荒次数

    private const string KEY_LEVEL = "level";        // 探测器等级

    private const string KEY_RARE = "rare_count";    // 累计稀有物掉落数（探测器面板展示）


    private const string SCANNER_TAG = "SCAV_SCANNER_DOUBLE_CHANCE";

    private static int BASE_CHANCE => BuildConfig.BaseChance;               // 初始稀有率（%）（CFG 可调）

    private static int UPGRADE_CHANCE => BuildConfig.UpgradeChance;            // 每级 +%（CFG 可调）

    private static int MAX_CHANCE => BuildConfig.HardMode ? BuildConfig.LuckMaxChanceHard : BuildConfig.LuckMaxChance;   // 稀有物发现几率上限（CFG 可调：标准25%/硬爽50%）

    private static int LEVELUP_EVERY => BuildConfig.LevelupEvery;             // 每 N 次拾荒升 1 级（CFG 可调）






    // 本次运行是否已发工具箱（参照蛙哥妙妙箱：静态标志防进程内重复）

    // 不用 PlayerPrefs 持久化防重——之前 KEY_GIVEN 残留 true 会把新档工具箱误拦（根因之一）。

    // HandleInitialItem 只在开新档调用（读档走 LoadGame 分支），天然不会多刷。

    private static bool _kitGiven = false;

    // 09-13 修复：新档开始时重置发放标记（Patches.HandleInitialItemPostfix 调用）
    // ===== 开局发放重试（09-12）：emporium 未就绪/部分失败 → 帧轮询补发 =====
    private static bool _pendingGive = false;
    private static int _pendingGiveFrames = 0;
    private static bool _kitOk = false;      // 工具箱发放成功
    private static bool _scannerOk = false;  // 探测器发放成功
    private static bool _beadOk = false;     // 虚空珠发放成功







    // 静态重置：开新档时清所有计数（HandleInitialItemPostfix / OnNewGame 双保险调用）

    // 回主菜单时调用：只重置静态变量缓存为-1（触发下次懒恢复），不清除 PlayerPrefs

    // 这样加载存档后计数从 PlayerPrefs 恢复，不会丢失




    // 开新档时调用：清统一存储层命名空间 + 重置缓存，计数从0开始








    // ============ 开局给物（Patches.HandleInitialItemPostfix 调用，与蛙哥妙妙箱/拾荒者信物同机制） ============




    // ============ 工具箱创建（5×1=5格便携容器，参考 satchel + CustomStorageContainer） ============




    // ============ 大背包创建（第三样，6×3=18格自定义尺寸容器，总指挥方案） ============




    // ============ 加强探测器：原版 metal_scanner + SCAV_SCANNER_DOUBLE_CHANCE 标签 ============




    // 【09-10 已废弃】mod 稀有率改纯等级制，不再写原版标签；原版双倍只看标签存在



    // ============ Patch: 拾荒次数 +2（与原版叠加） ============

    // 【拆包结论】实际剩余次数由 GetScavTimeLeft 决定（GetMaxScavAttempts 的逻辑复制 + 减去

    // PlayerStore 0x218 已用次数），它不调用 GetMaxScavAttempts——所以两个都必须 Patch，

    // 否则 +2 只影响"最大次数"不影响"实际可拾荒次数"。







    // ============ Patch: CanScavenge Prefix → 实际拾荒闸门（根因修复） ============

    // 【拆包结论】ScavengeDumpingGrounds 开头调用 CanScavenge 做实际判定，

    // 它内联复刻 GetMaxScavAttempts 逻辑（基础5/7 - 周数，下限3），不调用

    // GetMaxScavAttempts/GetScavTimeLeft。只 patch 显示层不改 CanScavenge →

    // "显示14次但实际只能5次"。此处 IsActive 时短路直接用 GetScavTimeLeft（已被+14），

    // 让显示=实际判定。

    private static bool _scavengeAllowedThisCall = false;

    private static int _attemptsAtScavenge = -1; // 09-26 拾荒守卫：PrefixScavengeDumpingGrounds 记录的原生次数（第三方清零判定用）



    // 记录本次 CanScavenge 调用前玩家是否受伤（Prefix 记录，Postfix 用）

    // 用于区分原版返回 false 的原因：受伤 vs 次数不足



    // Prefix：记录受伤状态，不短路（让原版执行受伤判定+次数判定）

    // 原实现短路原版只检查次数，丢掉了受伤判定 → 受伤后仍可拾荒（bug）




    // Postfix：修改次数判定（受伤判定由原版保留）

    // 如果没受伤且原版因次数不足返回 false，但 GetScavTimeLeft() > 0（已 Patch +14），改成 true




    // 记录本次 ScavengeDumpingGrounds 调用是否真正允许拾荒（Prefix 先于方法体执行）




    // ============ Patch: ScavengeDumpingGrounds Postfix → 拾荒计数 + 升级 ============




    // 回主菜单/开新档时重置计数（静态变量缓存 + PlayerPrefs）






    // ============ Patch: GetRandomScavengedItem Postfix → 稀有物概率追加 ============

    // 【拆包结论 recvubNSdZ1JvE】原版拾荒掉物只走 dumpingGroundTG，SCAV_SCANNER_DOUBLE_CHANCE

    // 标签只是"双倍再送1件"，不是稀有率。稀有率需 Patch GetRandomScavengedItem 追加稀有物：

    // 判定持有加强探测器 + 读探测器标签概率 → 追加价值池稀有物（已解锁物品）。

    // v1.1.3 合规：移除废弃机器/rare_electronic（官方未发布内容，制作组要求）。




    // v1.1.3 合规：废弃机器（CreateBrokenAlarm/Furnace/MoistureFarm）已移除，

    // 属官方未发布内容（制作组要求）。



    // 读探测器 SCAV_SCANNER_DOUBLE_CHANCE 标签值（双倍概率 + 稀有率共用概率载体）

    // 读探测器稀有率：完全由拾荒等级决定，不再读共享标签 SCAV_SCANNER_DOUBLE_CHANCE
    // 【09-10 用户拍板】原版电子零件升级不再影响 mod 稀有率（原版升级只走原版双倍机制）



    // 【探测器面板统计】记录累计稀有物掉落数（FlowScavenge 检测到稀有物时调用，持久化）




    // 【探测器面板展示】Patch ScavHelper.CreateTooltip(RichTextBuilder, GameItem) Postfix

    // 在金属探测器 Tooltip 上追加：当前稀有率 + 累计稀有物掉落数（方案C：概率+统计都显示）




    // 稀有物价值池（用户确认：价值≥200，排除容器/机器/家具）

    // 数据源 all_item_values_359_dump.txt，筛选后 28 个（材料/模块/武器/高级消耗品）
    // 09-12 用户拍板：硬爽版把神经模组加回稀有均匀池（标准版保持移出，防叠加）
    private static readonly string[] RARE_VALUE_POOL = BuildRarePool();




    // 稀有物：从价值池随机（价值≥200，排除容器/机器/家具），创建失败自动换下一个
    // 09-12 用户拍板：神经模组独立概率——未受限 0.5% / 受限 2%（标准版，已移出均匀池，防叠加）
    // 09-12 硬爽版：神经模组加回均匀池，删独立 roll（单渠道）






    // 找到玩家的加强探测器（可能在工具箱内或直接放背包）




    // 【防递归】FindScannerInOwned 内部调用被 Patch 的 GetAllAfterhourOwnedItems，

    // 而 Postfix 又调 FindScannerInOwned → 无保护会无限递归栈溢出。此标志切断递归链。

    private static bool _inAfterhourScan = false;



    // ============ Patch: GetAllAfterhourOwnedItems Postfix ============

    // 【拆包结论】原版 ScavengeDumpingGrounds 用 IsAfterhourHaveOwnedItems("metal_scanner")

    // + GetAfterhourItemById 检查探测器，二者都基于 GetAllAfterhourOwnedItems() 遍历

    // 打烊背包直接子物品（非递归）。探测器嵌套在工具箱里原版找不到 → 双倍掉落失效。

    // 此处把工具箱内的加强探测器补进返回列表，一处 Patch 让原版两个检查全通。




    // 在打烊持有物品里找加强探测器（工具箱内部 / 直接持有）

    // 标志在此方法内部管理：进入设置、finally 清除；内部调用 GetAllAfterhourOwnedItems

    // 会触发 Postfix → Postfix 见 _inAfterhourScan=true 直接跳过 → 递归被切断。




    // 运行时从容器拿内部库存（仅查找场景用；创建场景用 LastCreatedInventory，不走这里）







}

