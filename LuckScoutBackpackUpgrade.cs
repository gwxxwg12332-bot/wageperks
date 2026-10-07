using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace WagePerks;
partial class LuckScoutBackpackUpgrade


{
// ===== 自定义sprite =====

    private const string CUSTOM_ATLAS = "custom_atlas";

    private const string CUSTOM_SPRITE_KEY = "void_bead_sprite";

    private static Sprite _customSprite;

    // 嵌入的PNG字节数组（191字节，16x16深紫色虚空珠图标），发给别人也能正常显示，不依赖外部文件

// 10-07 统一载入：虚空珠贴图改走 WagePixelSprites.VoidBead（妙妙箱式 Color[]），byte[] 字段已移除



    // ===== 升级相关 =====

    private const string BACKPACK_TAG = "VOID_BEAD_TAG";

    private const string SLOTS_TAG = "VOID_BEAD_SLOTS_TAG";

    private const string MATERIAL_ID = "junk";

    private static DateTime _lastConsume = DateTime.MinValue;
    // 09-19 修复：防抖改 per-bead（原 static 全局共享——珠 A 升级后 0.5s 内拖 junk 到珠 B 被误挡 → "虚空珠有时升级不动"）
    // 10-05 拆包：Pointer 可能被新对象复用→防抖误判，改 identifier 键
    private static readonly Dictionary<string, DateTime> _lastConsumeByBead = new Dictionary<string, DateTime>();
    // 10-05 拆包修复：批量拖拽崩溃——已入库存物品延迟销毁（帧尾 Flush，防原版刷新链悬垂）
    private static readonly List<GameItem> _pendingDestroy = new List<GameItem>();



    // 虚空珠物品列表（读档恢复时用）

    private static readonly List<GameItem> _beadItems = new List<GameItem>();
    // 开局延迟应用锁格（容器就绪后强制 SetShape，防开局创建时被初始化覆盖）
    private static readonly List<Tuple<GameItem, int>> _pendingShapes = new List<Tuple<GameItem, int>>();

    // 内部网格尺寸

    private const int GRID_WIDTH = 20;

    private const int GRID_HEIGHT = 10;

    private const int MAX_SLOTS = GRID_WIDTH * GRID_HEIGHT; // 200








    // ===== 读档恢复：LoadGame完成后遍历玩家所有背包找虚空珠并恢复SetShape（根本方案） =====
    // 不依赖_beadItems列表（读档后列表为空），直接遍历玩家4个背包
    // 加固（2.5.39）：容器内容读档后延迟加载 → 立即恢复可能漏掉容器内虚空珠（尤其满级无法"碰一下自愈"）
    // → 立即尝试 + 未找到则挂 Core.OnUpdate 轮询 600 帧重试；满级 DoUpgrade 也触发形状恢复

    private static int _restoreFramesLeft = 0;







    // ===== 注册到 DirectoryMaster 工厂表（读档原生恢复 contentWindow） =====

    private static Il2CppSystem.Func<GameItem> _beadFactory = null;
}
