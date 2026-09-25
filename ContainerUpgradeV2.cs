using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
public static int MAX_STAGE => BuildConfig.ContainerMaxStage;

    // 蛙哥箱子段位网格表（段 k → 宽×高）——09-14 CFG 化（BuildConfig.BoxWidths/BoxHeights）
    public static int[] WAGE_BOX_W => BuildConfig.BoxWidthsArr;
    public static int[] WAGE_BOX_H => BuildConfig.BoxHeightsArr;

    // 升级消耗（段 k → 段 k+1 需材料数）——09-14 CFG 化（BuildConfig.UpgradeCosts）
    public static int[] UPGRADE_COSTS => BuildConfig.UpgradeCostsArr;

    // ===================== tag 工具 =====================
    // 09-22 门面模式：内部调 TagHelper
    public static int GetTagIntSafe(GameItem item, string tag) => TagHelper.GetInt(item, tag);
    public static bool HasTag(GameItem item, string tag) => TagHelper.Has(item, tag);
    public static void SetTagIntValue(GameItem item, string tag, int value) => TagHelper.SetInt(item, tag, value);
    public static void AddTagInt(GameItem item, string tag, int delta) => TagHelper.AddInt(item, tag, delta);





    // ===================== 同帧防重 =====================
    // 松手时 MayHaveValidInventorySlot 与 Target 两个挂点都会触发升级链（旧版"一次扣N个"被数量不足挡掉，
    // 逐颗版暴露双计数）。同一容器同一帧只处理一次，第二次调用视为"已消耗"直接拦截。
    private static readonly Dictionary<IntPtr, int> _lastUpgradeFrame = new Dictionary<IntPtr, int>();


    // ===================== 可升级容器判定 =====================
    // 建筑容器白名单：storage_bay/machine_bay 等是 ItemCategory.Container 建筑模块，
    // 没有 CONTAINER_TAG（减半遍历靠 CONTAINER_TAG OR IsMachine 双通道，升级判定原本只有
    // CONTAINER_TAG → 拖 junk 到 storage_bay 完全不触发，用户实测"面板都没有"）。
    // 背包类（backpack_*）一律排除，不参与升级。
    private static readonly HashSet<string> BUILDING_CONTAINER_IDS = new HashSet<string>(new string[] {
        "storage_bay", "storage_bay_large", "machine_bay", "machine_bay_ext",
        "mini_smuggler_bay", "smuggler_bay", "smuggler_bay_mod", "smuggler_bay_mini",
        "chemist_storage_bay", "gunsmith_storage_bay", "makeshift_storage_bay",
        // 09-20 新增：物资箱子（蛙娘带回）
        "evidence_box", "med_box", "sec_box", "service_box", "eng_box",
        // 09-20 新增：电池充电器（走容器升级链）
        "recharger_base"
    });


    // ===================== 升级系统排除名单（09-13 用户拍板） =====================
    // 文档箱(dossier)/工具箱(toolbox) 已拖 junk 实证命中升级；收音机=cassette_player（磁带播放器，用户提供 id）。
    // 排除后：不参与拖 junk 升级、不开局减半、不读档恢复 shape——完全回原版。
    private static readonly HashSet<string> EXCLUDED_CONTAINER_IDS = new HashSet<string>(new string[] {
        "toolbox", "dossier", "cassette_player", "water_bottle_printer", "bottle_printer"
    });








    // 09-20 优化：打烊批量消耗螺丝（SaveGame Postfix）
    public static void PrefixSaveGame(PlayerStore __instance) { try { ConsumeNutsAtClose(); } catch { } }




    // 满级奖励第二个妙妙箱（09-12 用户拍板两个箱子方案：替代翻页，天然存档/读档/睡眠零冲突）
    private static readonly System.Collections.Generic.HashSet<IntPtr> _secondGiven = new System.Collections.Generic.HashSet<IntPtr>();
}
