using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 养蛊机系统（09-15 开工）：养蛊机 / AI 生成器 / 保护器核心
// 注册 + 博士夜晚商店供货。图标 = 自定义像素 PNG（Mods\ 目录加载）。
// 机器窗口（模组舱）与吞噬/抽卡功能：后续步骤加入。
// ============================================================
public static partial class GuMachineSystem
{
    public const string GU_MACHINE_ID = "wage_gu_machine";
    public const string AI_GENERATOR_ID = "wage_ai_generator";
    public const string PROTECTOR_ID = "wage_protector_core";

    public const string ICON_ATLAS = "custom_atlas";
    public const string GU_ICON = "gu_machine";
    public const string AI_ICON = "ai_generator";
    public const string PROTECTOR_ICON = "protector_core";
    public const string AI_MODULE_ICON = "ai_module"; // 09-15 第4图标（不稳定AI模组）

    private static Sprite _guSprite;
    private static Sprite _aiSprite;
    private static Sprite _protectorSprite;
    private static Sprite _aiModuleSprite;

    private static Il2CppSystem.Func<GameItem> _guFactory;
    private static Il2CppSystem.Func<GameItem> _aiFactory;
    private static Il2CppSystem.Func<GameItem> _protectorFactory;

    static GuMachineSystem()
    {
        try
        {
            _guSprite = SpriteFromPixels(GuMachineIcons.GuMachinePixels(), 32, 32);
            _aiSprite = SpriteFromPixels(GuMachineIcons.AiGeneratorPixels(), 32, 32);
            _protectorSprite = SpriteFromPixels(GuMachineIcons.ProtectorPixels(), 32, 32);
            _aiModuleSprite = SpriteFromPixels(GuMachineIcons.AiModulePixels(), 32, 32);
            Core.LogMsg("[养蛊机] 图标加载: " + (_guSprite != null ? "养蛊机✓" : "养蛊机✗") + " " + (_aiSprite != null ? "生成器✓" : "生成器✗") + " " + (_protectorSprite != null ? "保护器✓" : "保护器✗") + " " + (_aiModuleSprite != null ? "AI模组✓" : "AI模组✗"));
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 图标加载异常: " + ex.Message); }
    }

    // 像素数组 → Texture2D → Sprite（照 StorageBoxPixels/CreateCustomBoxSprite 先例；pixelsPerUnit=100，32px=0.32单位）
    // 拦截 RenderHandler.LoadFromAtlas：custom_atlas + 我方图标名 → 返回自定义 sprite



    // 应用图标：自定义 sprite 可用 → custom_atlas；否则借用原生物品外观

    // 借用原生 sprite（运行时读原生物品 spriteAtlasPath/spritePath——public 属性，拆包 09-15 实锤）




    // 09-15 "不稳定AI模组"（生成器产出物——纯模组实物，无窗口；属性由抽卡写入）



    // ============================================================
    // 养蛊机功能（09-15 段3a）：打烊充能（wage_gu_charge 0-3）+ 满3自动炼蛊
    // 挂点：StoreEventManager.OnDayStart Postfix（吞噬季同挂点，多 postfix 顺序执行）
    // ============================================================
    public const string GU_CHARGE_TAG = "wage_gu_charge";
    // 09-19 P1 充能天数差分：LAST_DAY 记录上次充能日，同日打烊不重复 +1（根治读档后充能卡住/重复计）
    public const string GU_LAST_DAY_TAG = "wage_gu_last_day";


    // 全店找养蛊机（09-19 P4：FindAllItem(true) 全店扫描替代 EmporiumEntry 遍历——场景迁移后不丢机器）

    // 读养蛊机/生成器舱内网格（容器式单网格窗口：contentWindow.childElement 就是舱——09-15 实测 GetModuleInv 期待机器式双网格对容器式返回 null，功能全断，改回本读法）

    // 09-15 充能锁定：舱内所有 MODULE_TAG 模组打 MODULE_STUCK_TAG（充能中禁止拖出）

    // 09-19 修复：充能归零时摘除舱内全部 STUCK_TAG——原实现只打不摘，产出模组永久锁死在舱内（炼蛊器卡住根因）

    // 09-15 UI 兜底（拆包：GameItemElement : GameItem）：拖拽起点拦截——鼠标下物品带 MODULE_STUCK_TAG → 不开始拖



    // ============================================================
    // 不稳定 AI 生成器（09-15 段3b）：打烊自动抽卡（每天 1 次）
    // 装保护器 = 阉割版（消耗 1 个，不报废，上限 75%）
    // 不装 = 不稳定版（50% 成功上限 150% / 50% 失败=投入模组+生成器全报废）
    // 属性写机器 TOTAL_PERCENTAGE_{PERFORMANCE,POWER,QUALITY}_BONUS_INT（拆包：效率=POWER）
    // ============================================================
    public const string AI_LAST_DAY_TAG = "wage_ai_last_day";
    public const string AI_MODULE_ID = "wage_ai_module"; // 09-15 用户拍板：生成器产出"不稳定AI模组"实物（不是机器属性吸收）
    private static Il2CppSystem.Func<GameItem> _aiModuleFactory;


}

