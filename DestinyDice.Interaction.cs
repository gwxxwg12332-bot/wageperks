using System;

using System.Collections.Generic;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using MelonLoader;

using UnityEngine;

namespace JacksonPerks
{
    // ============================================================

    // 命运骰子 - 无内部空间版

    // 直接"吃掉"物品：拖物品到命运骰子上 → 销毁物品 + 累计价值

    // 每满400价值触发1个随机事件

    // ============================================================

    public static partial class DestinyDice

    {

        // ===== 工具：读写物品int标签 =====

        public static int GetTagInt(GameItem item, string tag)

        {

            try

            {

                if (item == null || !item.IsTag(tag)) return 0;

                var ts = item.GetTagReadonly(tag);

                return (ts != null) ? ts.GetInt() : 0;

            }

            catch { return 0; }

        }



        public static void SetTagInt(GameItem item, string tag, int value)

        {

            try

            {

                if (item == null) return;

                if (!item.IsTag(tag))

                {

                    item.EnableTag(tag, true);

                }

                // 根因修复：GetTagReadonly 是只读引用，SetInt 写不进去 → 累计永远0

                // 正解：ModifyTag（WineAppraisalMaster 验证过的写标签标准方式）

                System.Action<TagState> sysAct = delegate(TagState state) { state.SetInt(value); };

                var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);

                item.ModifyTag(tag, il2cppAct, false);

            }

            catch { }

        }



        // ===== 注册到DirectoryMaster（读档原生恢复） =====

        public static void RegisterToDirectory(ItemDirectory dir)

        {

            try

            {

                if (dir == null) return;

                if (((Directory<GameItem>)(object)dir).Has(DICE_ID)) return;



                if (_diceFactory == null)

                {

                    System.Func<GameItem> systemFactory = () => CreateRegisteredDice();

                    _diceFactory = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)systemFactory);

                }



                bool ok = ((Directory<GameItem>)(object)dir).Add(DICE_ID, _diceFactory);

                Core.LogMsg("[命运骰子] " + (ok ? "★ 已注册到DirectoryMaster" : "⚠️ 注册失败"));

            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 注册异常: " + ex.Message);

            }

        }



        // ===== 创建并放入玩家后背包（虚空珠模式，供测试快捷键反射调用） =====

        public static void GiveDestinyDiceToPlayer()

        {

            try

            {

                GameItem dice = CreateDestinyDice();

                if (dice == null) { Core.LogMsg("[命运骰子] GiveDestinyDiceToPlayer: 创建失败"); return; }



                var emporium = EmporiumEntry.Instance;

                if (emporium != null && emporium.backInvinvElement != null)

                {

                    try { emporium.backInvinvElement.TryFindOneValidInventorySlot(dice, false); } catch { }

                    bool accepted = ((GameInventory)emporium.backInvinvElement).UncheckedAccept(dice);

                    if (accepted)

                    {

                        try { emporium.TransferOwnershipBackInv(); } catch { }

                        // 清除未拥有标签 + 打已拥有标签

                        try { dice.DisableTag("not_purchased", true); } catch { }

                        try { dice.DisableTag("TAG_NOT_PURCHASED", true); } catch { }

                        try { dice.EnableTag("IS_OWNED_TAG", true); } catch { }


                    }



                }



            }

            catch (Exception ex) { Core.LogMsg("[命运骰子] GiveDestinyDiceToPlayer异常: " + ex.Message); }

        }



        // ===== 拖放拦截（照抄虚空珠模式）：拖物品到骰子上 → 吸收 =====

        public static bool PrefixMayHaveValidInventorySlot(GameItem __instance, GameItem item, ref bool __result)

        {

            try

            {

                // 物品拖到骰子(容器)上 → 拒绝放入（走吸收流程）

                if (IsDice(__instance) && item != null) { __result = false; return false; }

                // 注意：骰子放入后背包等容器 → 放行（骰子可正常放背包）

            }

            catch { }

            return true;

        }



        public static bool PrefixMayTarget(GameItem __instance, GameItem targetItem, ref bool __result)
        {
            // ===== 方案C：装备/使用链一律不吸收 =====
            // ItemSelectHandler = 原生"装备/使用物品"系统（右键装备→光标→点击目标交互）。
            // 用户拍板：这种"使用工具箱内的物品交互对应交互物"的操作，不管骰子在哪、目标是谁，都不应吸收。
            try
            {
                var selHandler = Il2Cpp.ItemSelectHandler.current;
                if (selHandler != null && selHandler.IsEquipped) return true; // 放行，不吸收
            }
            catch { }

            // 09-15 根因修复（拆包实锤）：吸收唯一触发点 = PrefixTarget（松手放置）。
            // MayTarget 只做"能否放置"判定——返回 true → 原生继续到 Target（松手才触发吸收）；悬停预览正常，不销毁物品。
            // 删除旧版"MayTarget 直接吸收"（悬停即销毁 → 未松开就吸收 + 四格全绿异常）。
            if (IsDice(targetItem) && __instance != null && !IsDice(__instance))
            {
                __result = true; // 可放置——吸收在 Target（松手放置）执行
                return false;
            }
            return true;
        }
        internal static bool TryGroupDragging(GameItem item)
        {
            try
            {
                var t = Il2CppSystem.Type.GetType("ItemMultiSelectHandler, Assembly-CSharp");
                if (t == null) return false;
                var curProp = t.GetProperty("current");
                if (curProp == null) return false;
                var handler = curProp.GetGetMethod().Invoke(null, null);
                if (handler == null) return false;
                var m = t.GetMethod("IsGroupDraggingItem");
                if (m == null) return false;
                var args = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(1);
                args[0] = item;
                var ret = m.Invoke(handler, args);
                if (ret == null) return false;
                return ret.Unbox<bool>();
            }
            catch { return false; }
        }

        public static bool PrefixCanTarget(GameItem __instance, GameItem targetItem, ref bool __result)

        {

            return PrefixMayTarget(__instance, targetItem, ref __result);

        }



        public static bool PrefixTarget(GameItem __instance, GameItem targetItem)

        {
            // ===== 方案C：装备/使用链一律不吸收 =====
            // ItemSelectHandler = 原生"装备/使用物品"系统（右键装备→光标→点击目标交互）。
            // 用户拍板：这种"使用工具箱内的物品交互对应交互物"的操作，不管骰子在哪、目标是谁，都不应吸收。
            try
            {
                var selHandler = Il2Cpp.ItemSelectHandler.current;
                if (selHandler != null && selHandler.IsEquipped) return true; // 放行，不吸收
            }
            catch { }

            GameItem dice = null, food = null;

            // 只处理：物品拖到骰子上（骰子是目标）。骰子拖到别的物品上 → 不拦截

                // 根因修复：Target 同样只在拖拽链触发（高亮/选择链调 MayTarget 不经过这里，但保险起见同样校验）
                var dragHandler = Il2Cpp.ItemMouseDragHandler.current;
                bool dragging = (dragHandler != null && dragHandler.IsDraggingItem);
                // 2.5.40 多选批量吸收：组拖判定补充（0.46D 兼容：反射探测 ItemMultiSelectHandler）
                if (!dragging)
                {
                    dragging = TryGroupDragging(__instance);
                }
                if (!dragging) return true;
            if (IsDice(targetItem) && __instance != null && !IsDice(__instance)) { dice = targetItem; food = __instance; }

            if (dice != null && food != null)

            {

                // 防误触：同样要求Shift

                DoAbsorb(food, dice);

                return false;

            }

            return true;

        }



        private static bool DoAbsorb(GameItem item, GameItem dice)

        {

            if (item == null || dice == null) return false;

            // 2.5.40 多选批量吸收：时间冷却会挡掉第2件起 → 按物品指针去重（防 MayTarget 对同一物品重复调用）
            long ptr = 0;
            try { ptr = item.Pointer.ToInt64(); } catch { }
            if (ptr != 0)
            {
                if (!_absorbedPointers.Add(ptr)) return false;
                if (_absorbedPointers.Count > 512) { try { _absorbedPointers.Clear(); } catch { } }
            }

            // 归属校验：未拥有的物品（博士夜晚商店等非玩家库存）拒绝吸收销毁（用户反馈"可以把未拥有的物品拖进去"）
            // 与双击拦截 IsInDoctorNightInventory 同源：afterhourInventory 的物品未购买，不属于玩家
            if (IsNonPlayerOwned(item))
            {
                return false;
            }

            try

            {

                // 获取被吸收物品的价值

                int itemValue = 0;

                try { itemValue = (int)item.GetCurrentValue(); } catch { }

                if (itemValue <= 0) itemValue = 1; // 最低1价值



                // 累计价值

                int currentValue = GetTagInt(dice, DICE_VALUE_TAG);

                int newValue = currentValue + itemValue;






                // v2 激活制（09-12）：吸收只累加价值，双击掷骰才触发事件
                SetTagInt(dice, DICE_VALUE_TAG, newValue);


                // 更新计数面板标题

                UpdateDicePanelTitle(dice, newValue);



                // 销毁被吸收的物品（吃掉）

                try { item.Destroy(); }

                catch { try { item.parentInventory?.Expel(item); } catch { } }



                return true;

            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 吸收异常: " + ex.Message);

                return false;

            }

        }



        // ===== 归属校验：物品是否属于非玩家所有（未购买/他人库存 → 拒绝吸收）=====
        //
        // 09-23 修复「夜晚拾荒时骰子无法吸收物品」：
        //   原实现只按"物品是否在 afterhourInventory（夜晚商店/夜拾库存）里"判定，
        //   但**该库存里的物品可能是玩家已购买的**（夜拾/博士夜买下后仍留在原库存里），
        //   于是合法物品也被判为"非玩家所有"而拒绝吸收。
        //   改为**优先使用游戏自带归属 API** `GeneralHelper.IsItemOwned(item)` 判真实归属
        //   （与 RobinCrusoePerk.IsItemOwned 同源，同一反射查找方式）；
        //   该 API 不可用时退回原容器判定，**原有的防误吸保护不丢**。
        private static System.Reflection.MethodInfo _gameIsItemOwned;
        private static bool _gameIsItemOwnedInited;

        /// <summary>尝试用游戏自带 API 判定归属。返回 false = API 不可用（调用方自行兜底）。</summary>
        private static bool TryGameIsItemOwned(GameItem item, out bool owned)
        {
            owned = false;
            try
            {
                if (!_gameIsItemOwnedInited)
                {
                    _gameIsItemOwnedInited = true;
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name != "Assembly-CSharp") continue;
                        var t = asm.GetType("GeneralHelper");
                        if (t != null)
                        {
                            foreach (var mi in t.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
                            {
                                if (mi.Name == "IsItemOwned" && mi.GetParameters().Length == 1) { _gameIsItemOwned = mi; break; }
                            }
                        }
                        break;
                    }
                }
                if (_gameIsItemOwned == null) return false;
                owned = (bool)_gameIsItemOwned.Invoke(null, new object[] { item });
                return true;
            }
            catch { return false; }
        }

        private static bool IsNonPlayerOwned(GameItem item)
        {
            try
            {
                if (item == null) return false;

                // ① 首选：游戏自带归属判定（能正确区分"已购买"与"未购买"）
                if (TryGameIsItemOwned(item, out bool owned)) return !owned;

                // ② 兜底：API 不可用时退回原逻辑（物品在夜晚库存里 → 视为非玩家所有）
                var em = Il2Cpp.EmporiumEntry.Instance;
                if (em == null || em.afterhourInventory == null) return false; // 实例不可用 → 保守放行

                long ptr = (long)item.Pointer;
                if (em.afterhourInventory.childItems != null)
                {
                    for (int i = 0; i < em.afterhourInventory.childItems.Count; i++)
                    {
                        var it = em.afterhourInventory.childItems[i];
                        if (it != null && (long)it.Pointer == ptr) return true;
                    }
                }
                return false; // 其余（玩家背包/柜台/容器/地板/未知）放行，防误伤
            }
            catch { return false; }
        }

        private static string GetItemIdSafe(GameItem item)
        {
            try { return (item.identifier ?? "").ToLowerInvariant(); } catch { return "?"; }
        }

        private static bool IsDice(GameItem item)

        {

            try { return item != null && item.IsTag("destiny_dice_tag"); }

            catch { return false; }

        }



        // 09-23 新增：最小骰子兜底（只带骰子身份 + 必需标签，不带任何容器/背包标签）
        // 仅在 CreateDestinyDice() 失败时启用，保证 DirectoryMaster 工厂永不返回非骰子物品
        private static GameItem CreateMinimalDice()

        {

            try

            {

                var it = ItemDirectory.CreateEmptyItem(null);

                if (it == null) return null;

                it.identifier = DICE_ID;

                try { it.EnableTag("destiny_dice_tag"); } catch { }

                try { SetTagInt(it, DICE_VALUE_TAG, 0); } catch { }

                try { SetTagInt(it, DICE_TRIGGER_TAG, 0); } catch { }

                try { SetTagInt(it, DICE_THRESHOLD_TAG, DICE_BASE_THRESHOLD); } catch { }

                try { SetTagInt(it, DICE_LAST_COST_TAG, 0); } catch { }

                try { it.SetName(LangHelper.T("命运骰子（累计0价值 / 触发0事件）", "Dice of Fate (value 0 / triggers 0)")); } catch { }

                try { it.SetSprite(DICE_ATLAS, DICE_SPRITE_KEY); } catch { }

                return it;

            }

            catch { return null; }

        }


        private static GameItem CreateRegisteredDice()

        {

            try

            {

                GameItem item = CreateDestinyDice();

                if (item != null) return item;

                // 09-23 修复「骰子有可能会出现骰子带有很多标签」：
                // 旧兜底返回 DirectoryMaster.Item("simple_backpack") —— 背包是容器，自带
                // CONTAINER_TAG / 容量 / 背包专属等一堆原生标签。本方法是 destiny_dice 的
                // DirectoryMaster 工厂（:2705/:2713），读档反序列化骰子时也走这里：
                // 拿到背包后再叠加存档里的骰子标签 → 骰子变成"带一堆标签的容器"。
                // 改为最小骰子兜底（只带 DICE_ID + destiny_dice_tag），绝不返回别的物品。
                item = CreateMinimalDice();

                if (item != null) return item;

                return null;

            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 工厂创建异常: " + ex.Message);

                return null;

            }

        }
}

}
