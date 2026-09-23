using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static partial class WageGirlSystem
{

    // ===================== 实体注册（Patches.PostfixInitDirectory 调） =====================



    // 像素数组 → Texture2D → Sprite（照 GuMachineSystem.SpriteFromPixels；ppu=100）

    // 反射设字段（照 GuMachineSystem.SetField）



    // ===================== 照顾指南窗口（09-23 用户拍板） =====================


    // ===================== 阶段 3：在场增益-议价 +50（GetDealMakerBonus Postfix——拆包 09-21 二次实锤：GetBargainSuccessChance 只被 tooltip 调用=纯显示；GetDealMakerBonus 是显示+实际判定共用唯一加成项，7 调用点覆盖 OfferMarkup/OfferBuyingMarkup/Blackmail） =====================

    // ===================== 阶段 3：在场增益-预算（09-23 用户拍板：原生预算×好感分档倍率，但绝不导致 0） =====================
    // 防御设计（依据拆包 [L1]：原生 ApplyBudgetModifier 只加不减，0 只能来自 mod 写回）：
    //   ① 原生算完预算 ≤0 → 跳过覆盖（绝不把 0/负值写回，让原生自己处理）
    //   ② 不再强制覆盖 clientCash（之前强制同步是归零事故的可疑点；clientCash 有独立语义）
    //   ③ 防重入 + OverrideBudget 只写一次（不触发原生 ApplyBudgetModifier 重算链）
    // 倍率分档（CFG：WageGirlBudgetMultLow/Mid/High × WageGirlBudgetAffLow/Mid）：
    //   好感 < AffLow → ×MultLow(1.5)；< AffMid → ×MultMid(2.5)；≥ AffMid → ×MultHigh(4)

    // ===================== 阶段 2：拖放喂食/喝水/照顾（照命运骰子拖放吸收链） =====================

    // 玩家归属判定（09-23 新增）：GeneralHelper.IsItemOwned ≡ item.IsTag("IS_OWNED_TAG")（原版归属标记 [L1]）
    // 交易模式下只许拿玩家自己的东西照顾蛙娘；读不到时按"非玩家所有"处理（保守，避免销毁客户的货）

    // ===================== 双击（全局——不依赖任何特性） =====================

    // 发放前全范围查重：5 网格 + 容器内部已有蛙娘 → true



    // ===================== 动画系统（09-22 蛙娘动画帧集成，用户拍板 B：真移动+走动帧） =====================
    // 12 帧 base64（WageGirlAnimFrames.cs）→ 运行时解码 Texture2D → Sprite[]（64×96 超采样，Point 缩回 32×48）
    private static Sprite[] _spIdle, _spHappy, _spHungry, _spThirsty, _spSick, _spDirty, _spSleepy, _spAngry, _spShy, _spFull, _spAway, _spReturn, _spWalk;
    private static string _curState = "idle";
    private static float _stateFrameSec = 0.375f;
    private static float _leavingTimer = 0f;
    private static float _returnTimer = 0f; // 回归动画倒计时（return帧播完切idle） // 外出动画延迟（walk帧播完再移除实体）
    private static Sprite[] _curAnimSprites;
    private static int _frameIndex = 0;
    private static float _frameTimer = 0f;
    private static int _animMode = 0; // 0=待机 1=走动 2=偷
    private static float _animModeTimer = 0f;
    private static float _moveTimer = 0f;
    private static float _lastDiagTime = 0f;
    private static float _lastMoveDiagTime = 0f; // 09-22 TryMoveStep 诊断独立节流（用完删）
    private static float _budgetDiagTime = 0f;   // 预算诊断节流（发布前删）
    private static bool _walking = false;    // 09-22 走停状态机：是否在走动
    private static int _stepsTaken = 0;      // 本轮已走步数
    private static int _walkSteps = 4;       // 本轮要走步数（随机 3-7）
    private static float _pauseTimer = 0f;   // 停顿计时
    private static float _pauseDuration = 4f;// 停顿时长（随机 3-6 秒）
    private static GridShape _girlShape; // 运行时初始化（Unity就绪后）
    private static Sprite _staticIconSprite; // 静态图标 32×48（modifiedShape=2×3 权威来源，09-20 拆包）
    private static readonly float[] _frameMs = { 0.5f, 0.2f, 0.15f }; // 待机/走动/偷（秒/帧）


    private static System.Reflection.MethodInfo _loadImageMethod; // ImageConversion.LoadImage（IL2CPP 反射查找，DestinyDice 先例）


    // 反射查找 ImageConversion.LoadImage（IL2CPP 不在标准命名空间——DestinyDice L175-213 先例；只找一次）

    // 静态图标（32×48，ppu100）：Idle0 帧 64×96 下采样 2:1 → SetSpriteAndShape 成功路径算 modifiedShape=2×3（09-20 拆包 L3484-3533）

    // 拦截 RenderHandler.LoadFromAtlas：wage_girl_icon → 32×48 静态图标——09-20 拆包实锤 modifiedShape 权威，SetSpriteAndShape 成功即算 2×3

    // 动作切换（0 待机 / 1 走动 / 2 偷）


    // 09-23 性能：实体存在性检查的节流缓存（**只用于动画驱动**）
    // 问题：Exists() → ExistsInScene() 要遍历 5 个背包的全部物品，且 FindGirlInInv 会向
    //       contentWindow 内部背包**递归下钻** → 开销随物品总数线性（甚至更高）增长。
    //       后期背包/货架塞满时，每帧全量扫描 = 明显卡顿。
    // 处理：动画启停不需要每帧精度 → 每 15 帧（60fps 下 0.25s）重算一次，其余帧读缓存。
    // ⚠️ 仅在**动画驱动**这一条路径上用缓存；偷拿/补发等业务判定仍调 Exists()，保证精确语义不受影响。
    private const int EXISTS_CACHE_FRAMES = 15;
    private static bool _existsCache;
    private static int _existsCacheFrame = -100000;

    // 每帧驱动（09-23 阶段0：Patches.FrameUpdate 调用 ← InputActionManager.Update Postfix，禁用 MelonLoader OnUpdate）
    // 轻量 + 全异常防护 + 交易模式暂停——用户规范：每帧逻辑不做重操作
    private static int _lastTickFrame = -1;

    // 09-22 帧应用：从网格找蛙娘实体 → Cast GameItemElement → 调 ApplyAnimationFrame（触发 Prefix 替换帧）
    // ApplyAnimationFrame 在原生无调用方（ISIL 全库 0 call），必须 mod 主动调用；实体每 2 秒重找（玩家可能移动/收起）
    private static GameItem _cachedGirlItem;
    private static int _cacheRefreshFrames = 0;
    // 09-23 性能：缓存 Cast 结果。原先每帧都做一次 `as` + `Cast<GameItemElement>()`（IL2CPP 互操作类型检查，
    // 每帧一次纯浪费）——元素引用只在 _cachedGirlItem 变化时才会变，故与它同步刷新。
    private static GameItemElement _cachedEl;

    // 网格中查找蛙娘实体（4 货架；同 TryMoveStep 遍历源）

    // 尝试移动一步：Expel + TryInventorySlot(目标格子编号) 落格（边界回弹 + 兜底放回，绝不丢实体）

    // ResolveSpriteByName Postfix：对蛙娘永远返回 mod 图标（拆包实锤：Validate 链从 spritePath 解析 sprite，拦截此入口根治旧图）

}
