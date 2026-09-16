using System;
using System.Collections.Generic;

namespace JacksonPerks;

/// <summary>
/// 第三方 mod 冲突预防（09-19）：统一探测 + 已知冲突表 + 主动让路 + 启动告警。
/// 模式来源：RobinCrusoePerk.IsMoreUpdateLoaded（AppDomain 扫描程序集名，缓存 bool?）。
/// 策略：
///  - 主动让路（YieldOnLoad=true）：目标方法命中冲突表 且 对应 mod 已加载 → patch 前主动跳过。
///    防"我们先挂、对方后挂"的 Harmony 双 detour 进程级崩溃（被动 GetPatchInfo 管不到对方未挂场景）。
///  - 只告警（YieldOnLoad=false）：核心方法（SaveGame 等）不让路（让了=存档逻辑失效=静默 bug），
///    与对方共存（Harmony 链表多 patch），仅打告警日志提示测试注意。
/// 数据来源：已拆包实锤的第三方 mod 重叠点（EmptyNukeBarrel_Rare 拾荒域 / InventoryPages SaveGame /
///           MoreDeviceUpgrades 金属锭升级——后者已有独立业务让路，此处仅登记告警）。
/// </summary>
public static class ModCompat
{
    private sealed class ConflictEntry
    {
        public string ModAssembly;
        public string TypeName;      // 目标方法所在类型（FullName 宽松匹配 Contains）
        public string MethodName;
        public bool YieldOnLoad;     // true=主动让路；false=只告警共存

        public ConflictEntry(string modAssembly, string typeName, string methodName, bool yieldOnLoad)
        {
            ModAssembly = modAssembly;
            TypeName = typeName;
            MethodName = methodName;
            YieldOnLoad = yieldOnLoad;
        }

        public bool Matches(string typeFullName, string methodName)
        {
            if (string.IsNullOrEmpty(typeFullName) || string.IsNullOrEmpty(methodName)) return false;
            if (!typeFullName.Contains(TypeName)) return false;
            return string.Equals(methodName, MethodName, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static readonly Dictionary<string, bool?> _loadedCache = new Dictionary<string, bool?>();

    // ===== 已知安全共存白名单（09-19 B2 根因修复）=====
    // 背景：ManualPatcher.IsConflictOwned 无差别拦截"已被其他 mod patch"的方法——
    // 本机 Demo 环境常驻的调试工具 PS_DebugTool 先挂 PlayerStore.LoadGame（[HarmonyPatch] attribute，
    // Postfix 仅 LeftPanel.ClearLockedState() 纯 UI 清理，无状态修改）→ 我们的 LoadGame 恢复链全部被跳过
    // （B2 面板/吃喝失效、B1 房租 100 天制失效同根）。
    // 白名单放行条件：标准 Harmony patch（非 MonoMod detour 复制，无 CLR fatal 风险）+ 行为已实锤安全。
    private static readonly string[] SAFE_COEXIST_OWNERS = new string[]
    {
        "PS_DebugTool", // 我方调试工具：LoadGame Postfix = LeftPanel.ClearLockedState()（源码实锤）
    };

    /// <summary>冲突 owner 是否在白名单（安全共存）</summary>
    public static bool IsSafeCoexistOwner(string owner)
    {
        if (string.IsNullOrEmpty(owner)) return false;
        foreach (var s in SAFE_COEXIST_OWNERS)
            if (owner.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // ===== 已知冲突表（已拆包实锤；新重叠点拆包确认后追加）=====
    // 分级：主动让路（YieldOnLoad=true）= 功能可降级，检测到对方已加载即跳过（防双 detour 崩）
    //       共存告警（YieldOnLoad=false）= 数据/注册核心不让（让了=职业失效/存档错乱），靠被动 GetPatchInfo + 实测
    private static readonly List<ConflictEntry> KNOWN_CONFLICTS = new List<ConflictEntry>
    {
        // --- EmptyNukeBarrel_Rare：拾荒域（09-16 玩家 15 mods 崩溃实锤）——拾荒加成可降级回原生 ---
        new ConflictEntry("EmptyNukeBarrel_Rare", "ScavHelper", "ScavengeDumpingGrounds", true),
        new ConflictEntry("EmptyNukeBarrel_Rare", "ScavHelper", "GetRandomScavengedItem", true),
        new ConflictEntry("EmptyNukeBarrel_Rare", "ScavHelper", "GetMaxScavAttempts", true),
        new ConflictEntry("EmptyNukeBarrel_Rare", "ScavHelper", "GetScavTimeLeft", true),
        new ConflictEntry("EmptyNukeBarrel_Rare", "ScavHelper", "CanScavenge", true),
        // --- NestedStorage（QoL）：ContainerHelper.InitContainerItem 同方法双 Postfix（09-19 拆包实锤）---
        // 我方鲁滨逊容器初始化减半/段位标记让路（职业特性弱化，游戏正常）
        new ConflictEntry("NestedStorage", "ContainerHelper", "InitContainerItem", true),
        // --- PerkPointMod（QoL）：PerkUIController.OpenUI 同方法（我方特性面板增强让路——特性选择流程保原生更安全）---
        new ConflictEntry("PerkPointMod", "PerkUIController", "OpenUI", true),
        // --- XIAOWOTradePerks：交易/特性域 8 方法重叠（09-19 拆包实锤 28 文件）---
        // 可让（面板/图标/事件增强降级）：
        new ConflictEntry("XIAOWOTradePerks", "PerkUIController", "OpenUI", true),
        new ConflictEntry("XIAOWOTradePerks", "StartingPerkElement", "Start", true),
        new ConflictEntry("XIAOWOTradePerks", "StartingPerkIconLoader", "Start", true),
        new ConflictEntry("XIAOWOTradePerks", "StoreEventManager", "OnDayStart", true),
        // 不让（数据/注册核心，共存告警 + 被动检测兜底）：
        new ConflictEntry("XIAOWOTradePerks", "PlayerStore", "LoadGame", false),
        new ConflictEntry("XIAOWOTradePerks", "PlayerStore", "SellItem", false),
        new ConflictEntry("XIAOWOTradePerks", "GameItem", "GetNegociatedValue", false),
        new ConflictEntry("XIAOWOTradePerks", "StartingPerkList", "InitStartingPerk", false),
        // --- AugPresenceGuard：存档/开日核心，共存告警 ---
        new ConflictEntry("AugPresenceGuard", "PlayerStore", "LoadGame", false),
        new ConflictEntry("AugPresenceGuard", "PlayerStore", "BeginDay", false),
        // --- InventoryPages：SaveGame（MergeForSave 挂 PlayerStore.SaveGame，源码实锤 700 行已读）---
        // 核心存档方法不让路（让了 = 鲁滨逊存档标记失效 = 静默 bug）→ 共存 + 告警
        new ConflictEntry("InventoryPages", "PlayerStore", "SaveGame", false),
        // --- MoreDeviceUpgrades：金属锭升级（已独立业务让路 IsMoreUpdateLoaded，无需重复跳过）→ 仅告警 ---
        new ConflictEntry("MoreDeviceUpgrades", "RobinCrusoePerk", "PrefixTarget", false),
        // --- GoFishing 1.3.10（09-19 拆包实锤，rar 解压后 4 DLL 全分析）---
        // ItemForgeRuntime：ScavengeBonusPatch = Postfix __result.Clear() 强接管拾荒产出（源码实锤 L91/L95）
        // → 我方拾荒双倍/稀有物追加让路（共存=加载顺序依赖，结果不稳定）
        new ConflictEntry("ItemForgeRuntime", "ScavHelper", "GetRandomScavengedItem", true),
        // ItemForgeRuntime：加工台拖放/双击链（ProcessingToolCanTargetPatch/MayTargetPatch/TargetPatch + ProcessingContainerDoubleClickPatch）
        // → 我方虚空珠/机器金属锭升级/双击吃喝共用 GameItem.CanTarget/MayTarget/Target + DoubleClickAction——核心交互不让，共存告警 + 实测
        new ConflictEntry("ItemForgeRuntime", "GameItem", "CanTarget", false),
        new ConflictEntry("ItemForgeRuntime", "GameItem", "MayTarget", false),
        new ConflictEntry("ItemForgeRuntime", "GameItem", "Target", false),
        new ConflictEntry("ItemForgeRuntime", "ItemMouseDoubleClickHandler", "DoubleClickAction", false),
        new ConflictEntry("ItemForgeRuntime", "PlayerStore", "AddDirectSellingItemToTable", false),
        // CharacterForgeRuntime：钓鱼商人/渔民 NPC 排期（CharacterSchedule OnNewDay/ClientGenCheck/BeginDay + CharacterVisit 上货 + LoadGame）
        // → 我方供应商 NPC 调度/存档核心——共存告警 + 实测
        new ConflictEntry("CharacterForgeRuntime", "StoreClientManager", "OnNewDay", false),
        new ConflictEntry("CharacterForgeRuntime", "PlayerStore", "BeginDay", false),
        new ConflictEntry("CharacterForgeRuntime", "PlayerStore", "LoadGame", false),
        new ConflictEntry("CharacterForgeRuntime", "PlayerStore", "AddDirectSellingItemToTable", false),
        // --- BrewingExpansion 酿酒拓展 v1.0（09-19 拆包实锤，zip 解压后 36 cs 全分析）---
        // MachineBottlePrinter.TryPrint 闭包类方法（DisplayClass 内 (string,int)）——它 TargetMethod 动态选中我们 WaterMerchantPerk
        // 同挂的 __c__DisplayClass6_0 方法——同方法双 Prefix 顺序敏感 → 主动让路（水商之友瓶印机映射降级，避免打架）
        new ConflictEntry("BrewingExpansion", "MachineBottlePrinter", "TryPrint", true),
        // 核心数据/交易方法 → 共存告警（我方加价链 vs 它酿酒价格，同方法双 Postfix 顺序敏感，实测定）
        new ConflictEntry("BrewingExpansion", "PlayerStore", "BeginDay", false),
        new ConflictEntry("BrewingExpansion", "PlayerStore", "LoadGame", false),
        new ConflictEntry("BrewingExpansion", "PlayerStore", "SellItem", false),
        new ConflictEntry("BrewingExpansion", "GameItem", "GetCurrentValue", false),
    };

    /// <summary>按程序集名探测 mod 是否已加载（AppDomain 扫描，结果缓存）</summary>
    public static bool IsLoaded(string assemblyName)
    {
        try
        {
            if (_loadedCache.TryGetValue(assemblyName, out var cached) && cached.HasValue) return cached.Value;
            bool found = false;
            var asms = System.AppDomain.CurrentDomain.GetAssemblies();
            if (asms != null)
                foreach (var a in asms)
                {
                    if (a == null) continue;
                    string n = "";
                    try { n = a.GetName().Name ?? ""; } catch { }
                    if (n == assemblyName) { found = true; break; }
                }
            _loadedCache[assemblyName] = found;
            return found;
        }
        catch { return false; }
    }

    /// <summary>patch 前调用：目标方法是否命中"主动让路"条目且对应 mod 已加载</summary>
    public static bool ShouldYield(Type type, string methodName, out string modName)
    {
        modName = "";
        try
        {
            string tn = type?.FullName ?? "";
            foreach (var e in KNOWN_CONFLICTS)
            {
                if (e.YieldOnLoad && e.Matches(tn, methodName) && IsLoaded(e.ModAssembly))
                {
                    modName = e.ModAssembly;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>启动告警：列出已加载的已知冲突 mod（Core 初始化后调用一次）</summary>
    public static void LogLoadedConflicts()
    {
        try
        {
            var seen = new HashSet<string>();
            foreach (var e in KNOWN_CONFLICTS)
            {
                if (!seen.Add(e.ModAssembly)) continue;
                if (!IsLoaded(e.ModAssembly)) continue;
                string act = e.YieldOnLoad ? "对应重叠方法将主动让路（功能降级）" : "与对方共存（核心方法不让路，注意测试）";
                Core.LogMsg($"[兼容] 检测到第三方 mod「{e.ModAssembly}」已加载——{act}");
            }
        }
        catch { }
    }
}
