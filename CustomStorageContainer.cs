using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace JacksonPerks
{
    /// <summary>
    /// 自定义储物容器 - 2×2外部占用，内部大容量
    /// 创建新物品+自定义sprite，不影响其他storage_bay
    /// </summary>
    public static partial class CustomStorageContainer
    {
        public const string CONTAINER_ID = "custom_storage_box";
        public static string CONTAINER_NAME => LangHelper.T("蛙哥妙妙箱", "Wage Wonder Box");
        public static string CONTAINER_DESC => LangHelper.T("蛙哥传奇当铺的镇店之宝，占用2×2空间，内部52×10大容量储物箱，什么都能放（包括箱子和机器）。", "The treasure of Wage's legendary pawnshop. 2x2 footprint, 52x10 internal storage. Can hold anything (including crates and machines).");
        public const string CUSTOM_ATLAS = "custom_atlas";
        public const string CUSTOM_SPRITE_NAME = "custom_storage_box_sprite";

        // 用静态构造函数确保_customSprite在任何方法调用之前已创建
        private static Sprite _customSprite;
        private static GameInventory _lastCreatedInventory; // 保存最后创建的内部库存，供FillWithRandomLockedBoxes使用

    /// <summary>最近一次 CreateContainer 创建的内部库存（供工具箱等改容量用）</summary>
    internal static GameInventory LastCreatedInventory => _lastCreatedInventory;
        private static bool _spriteCreated = false;

        static CustomStorageContainer()
        {
            try
            {
                _customSprite = CreateCustomBoxSprite();
                _spriteCreated = (_customSprite != null);
                Core.LogMsg($"[自定义储物箱] 静态构造函数创建sprite: {(_spriteCreated ? "成功" : "失败")}");
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] 静态构造函数异常: {ex.Message}");
            }
        }



        /// <summary>
        /// 创建自定义箱子sprite（用用户设计图缩小到32×32像素，保留好看外观+像素风格）
        /// </summary>
        
        /// <summary>
        /// 游戏风格的像素箱子（保留设计图元素：紫色边框+青色发光条+金属灰+锁扣+铆钉，像素风暗色调）
        /// </summary>
        
        /// <summary>
        /// 备用sprite（代码生成的简单金属箱子）
        /// </summary>

        /// <summary>
        /// 创建自定义储物箱（完整工厂链路，参考MiniSmugglerBay）
        /// </summary>

        /// <summary>
        /// 获取箱子某个像素的颜色（全新设计，金属箱子风格）
        /// </summary>

        /// <summary>
        /// 设置私有字段
        /// </summary>
        
        /// <summary>
        /// 创建带原版内容的上锁箱子（用PreBuiltItemHelper.LootCrate*）
        /// </summary>
        
        /// <summary>
        /// 往蛙哥妙妙箱里放随机上锁的箱子 + 指挥卡（command_keycard）
        /// 需求：不要研发卡(sci_keycard)生成在箱子里；只要一张指挥卡(cmd_keycard)放随机箱子
        /// </summary>


        private static Il2CppSystem.Func<GameItem> _registeredFactory;

        /// <summary>
        /// 把 custom_storage_box 注册到游戏物品目录（DirectoryMaster.Item 的工厂表）。
        /// 这样读档时游戏走原生路径 SaveManager.DecodeNodes → DirectoryMaster.Item(identifier)
        /// → 我们的工厂创建带 contentWindow 的完整箱子 → 能打开；内部内容由 DecodeNodes 按存档恢复。
        /// 与 EmptyNukeBarrel 注册 empty_nuclear_waste_barrel 同一机制。
        /// </summary>

        /// <summary>
        /// DirectoryMaster 工厂创建：返回空箱子（含窗口/标签/类型/sprite，不带内容）。
        /// 注意：DirectoryMaster.Item 创建出带 childItems 的物品会被游戏警告并移除，
        /// 所以工厂只返回空箱子；内容由读档时 DecodeNodes 按存档 uuid 恢复。
        /// </summary>
    }
}