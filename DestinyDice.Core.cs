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

        public const string DICE_ID = "destiny_dice";

        public const string DICE_VALUE_TAG = "destinyDiceValue"; // 累计价值标签

        public const int DICE_TRIGGER_VALUE = 400;

        public const string DICE_TRIGGER_TAG = "destinyDiceTriggers"; // 每400价值触发1个事件
        public const string DICE_THRESHOLD_TAG = "destinyDiceThreshold"; // 当前摇骰门槛（初始400，每次掷骰+400）
        public const string DICE_LAST_COST_TAG = "destinyDiceLastCost"; // 最近一次掷骰消耗的门槛（卸载返还50%）
        public const string DICE_PENDING_EVENT_TAG = "destinyDicePendingEventId"; // 已排队事件id（string，卸载用）
        public static int DICE_BASE_THRESHOLD => BuildConfig.DiceTriggerValue;



        // ===== 自定义sprite（PNG嵌入DLL，不依赖外部文件） =====

        public const string DICE_ATLAS = "custom_atlas";           // 与虚空珠共用atlas

        public const string DICE_SPRITE_KEY = "dice_of_fate_sprite"; // 骰子专用key

        private static Sprite _diceSprite = null;

        private static readonly byte[] EMBEDDED_DICE_PNG = new byte[] {

0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00,

0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x20,

0x00, 0x00, 0x00, 0x20, 0x08, 0x06, 0x00, 0x00, 0x00, 0x73,

0x7A, 0x7A, 0xF4, 0x00, 0x00, 0x00, 0xFD, 0x49, 0x44, 0x41,

0x54, 0x78, 0x9C, 0x63, 0x60, 0x18, 0x05, 0x23, 0x1D, 0x30,

0x12, 0x52, 0xB0, 0x60, 0xF6, 0xC4, 0xFF, 0x94, 0x58, 0x90,

0x90, 0x9A, 0x8F, 0xD7, 0x0E, 0x16, 0x42, 0x06, 0xC8, 0xC9,

0xC9, 0x31, 0x0C, 0x68, 0x08, 0xFC, 0xFF, 0xF3, 0x95, 0xA2,

0x10, 0x60, 0x64, 0xE1, 0xC6, 0x6B, 0x07, 0x23, 0xAE, 0x60,

0x07, 0xF9, 0x5C, 0x57, 0x57, 0x17, 0xCC, 0xE7, 0xE2, 0xE2,

0x21, 0xCB, 0xF2, 0x6F, 0xDF, 0xBE, 0x80, 0xE9, 0xCB, 0x97,

0x2F, 0x33, 0x3C, 0x7A, 0xF4, 0x08, 0x6B, 0x74, 0xB0, 0x90,

0x62, 0x10, 0x2D, 0x00, 0x13, 0x29, 0x8A, 0x8D, 0x2D, 0xDC,

0xA9, 0xAA, 0x8E, 0x24, 0x07, 0x80, 0x0C, 0x15, 0x11, 0x11,

0x21, 0x68, 0x38, 0x01, 0x75, 0x36, 0x48, 0x98, 0x34, 0x07,

0x9C, 0x3D, 0xB1, 0x93, 0xE1, 0xCD, 0x9B, 0x37, 0x60, 0x9A,

0x1A, 0xEA, 0x48, 0x76, 0x00, 0x08, 0x10, 0x6B, 0x28, 0xB1,

0xEA, 0x40, 0x80, 0x24, 0x07, 0xD0, 0x02, 0x30, 0x51, 0x6A,

0x00, 0x29, 0x09, 0x8E, 0xEA, 0x0E, 0x30, 0x26, 0x32, 0x61,

0xD2, 0xCC, 0x01, 0x67, 0x49, 0x4C, 0x70, 0x54, 0x77, 0x00,

0x08, 0x50, 0x62, 0x39, 0x08, 0x0C, 0xAD, 0x44, 0x68, 0x3C,

0x0C, 0x4A, 0x42, 0x0C, 0xC0, 0xC4, 0x30, 0xD2, 0x4B, 0x42,

0x16, 0x42, 0x0A, 0xEE, 0xDE, 0xBD, 0xCB, 0x40, 0x09, 0x50,

0x56, 0x56, 0xA6, 0xAC, 0x45, 0xB4, 0x6F, 0xE7, 0x7A, 0x8A,

0x5A, 0x44, 0x4E, 0xEE, 0x81, 0xB6, 0x38, 0xA4, 0x8E, 0x80,

0x08, 0x82, 0x21, 0x00, 0x6A, 0xC9, 0xD0, 0x12, 0x30, 0x12,

0xA1, 0x06, 0x5E, 0x77, 0x53, 0x19, 0x1C, 0x19, 0x14, 0x05,

0x11, 0x31, 0x21, 0x40, 0xCD, 0xD0, 0x00, 0xFB, 0x1A, 0x19,

0x30, 0x51, 0x68, 0xE0, 0x28, 0x60, 0xA0, 0x14, 0x00, 0x00,

0x07, 0x7C, 0x61, 0xCB, 0xBC, 0x31, 0x51, 0xE3, 0x00, 0x00,

0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82

};



        // 工厂委托（延迟初始化）

        private static Il2CppSystem.Func<GameItem> _diceFactory = null;



        // 吸收去重（2.5.40 多选批量吸收：按物品指针去重，防 MayTarget 对同一物品重复调用；吸收后物品销毁本防重）

        private static readonly System.Collections.Generic.HashSet<long> _absorbedPointers = new System.Collections.Generic.HashSet<long>();



        // ===== 静态构造函数：加载PNG sprite（嵌入数组优先，文件兜底，动态生成最后兜底） =====

        static DestinyDice()

        {

            try

            {

                byte[] pngBytes = EMBEDDED_DICE_PNG; // 嵌入DLL的字节（发给别人也能显示）

                try

                {

                    string pngPath = @"D:\DoubaoWork\Project_001_WagesPerks\04_产物\dice_of_fate_32x32.png";

                    if (System.IO.File.Exists(pngPath)) pngBytes = System.IO.File.ReadAllBytes(pngPath);

                }

                catch { }



                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)

                {

                    filterMode = FilterMode.Point,

                    wrapMode = (TextureWrapMode)1,

                    hideFlags = (HideFlags)61

                };

                Type icType = null;

                foreach (System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies())

                {

                    Type[] types;

                    try { types = a.GetTypes(); }

                    catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }

                    foreach (Type t in types)

                    {

                        if (t != null && t.Name == "ImageConversion") { icType = t; break; }

                    }

                    if (icType != null) break;

                }

                if (icType != null)

                {

                    var loadMethod = icType.GetMethod("LoadImage",

                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,

                        null, new Type[] { typeof(Texture2D), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>) }, null);

                    if (loadMethod != null)

                        loadMethod.Invoke(null, new object[] { tex, (Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)pngBytes });

                }

                _diceSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

                _diceSprite.hideFlags = HideFlags.HideAndDontSave;


            }

            catch (Exception ex) { Core.LogMsg("[命运骰子] 加载sprite失败: " + ex.Message); }

        }



        // ===== Patch RenderHandler.LoadFromAtlas：拦截自定义atlas返回骰子sprite =====

        public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)

        {

            try

            {

                if (atlasPath == DICE_ATLAS && name == DICE_SPRITE_KEY && _diceSprite != null)

                {

                    __result = _diceSprite;

                    return false; // 拦截

                }

            }

            catch { }

            return true; // 放行

        }



        // ===== 创建命运骰子物品（非容器，普通2x2物品） =====

        public static GameItem CreateDestinyDice()

        {

            try

            {

                // 非容器：普通物品，无内部空间（吸收=直接吃掉物品）

                var item = ItemDirectory.CreateEmptyItem(null);

                if (item == null)

                {


                    return null;

                }

                item.identifier = DICE_ID;



                // 标签（非容器，不需要backpack/CONTAINER_TAG）

                item.EnableTag("destiny_dice_tag"); // 自定义标签，用于识别



                // 初始累计价值=0、触发事件数=0

                SetTagInt(item, DICE_VALUE_TAG, 0);

                SetTagInt(item, DICE_TRIGGER_TAG, 0);
                SetTagInt(item, DICE_THRESHOLD_TAG, DICE_BASE_THRESHOLD);
                SetTagInt(item, DICE_LAST_COST_TAG, 0);
                // 名称和描述（计数显示在名称/描述上，吸收后实时更新）

                item.SetName(LangHelper.T("命运骰子（累计0价值 / 触发0事件）", "Dice of Fate (value 0 / triggers 0)"));

                try { item.shortDescription = LangHelper.T("拖物品到骰子上吸收其价值，双击掷骰触发随机事件（累计满400后打烊回落）。", "Drag items onto the die to absorb their value; double-click to roll a random event (threshold resets at closing after 400)."); } catch { }

                try { item.flavorText = LangHelper.T("古老的命运骰子，能吸收物品的价值并改写命运。", "An ancient die of fate that absorbs the value of items and rewrites destiny."); } catch { }



                // 外观（自定义骰子PNG，2x2占地）

                try

                {

                    item.SetSprite(DICE_ATLAS, DICE_SPRITE_KEY);

                }

                catch (Exception exs)

                {

                    Core.LogMsg("[命运骰子] SetSprite失败: " + exs.Message);

                    try { item.SetSprite("Items/items_backpack2", "simple_backpack"); } catch { }

                }

                // 2x2占地（GridShapeBuilder.SetDataFill(2,2) → Build → SetShape）

                try

                {

                    var gsb = new GridShapeBuilder();

                    gsb.SetDataFill(2, 2);

                    GridShape shape = gsb.Build();

                    item.SetShape(shape);


                }

                catch (Exception exshape) { Core.LogMsg("[命运骰子] SetShape失败: " + exshape.Message); }




                return item;

            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 创建失败: " + ex.Message);

                return null;

            }

        }



        // ===== 吸收物品（吃掉） =====

                // ===== 假货/贴标水价值（拆包 09-10 [L1]：水也有独立 feature 系统 WaterFeatureHelper；CheckSoldDirtyWater = 贴标判定）=====
        private static bool IsFakeOrTampered(GameItem item)
        {
            try
            {
                if (item == null) return false;
                if (item.IsAlreadyContainFeatureWithId("realCounterfeit")) return true;  // 香烟假货
                if (item.IsAlreadyContainFeatureWithId("fakeGenuine")) return true;      // 邮票假/高仿烟
                var inj = item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR");
                if (inj != null
                    && inj.GetBaseFakeDisplay() != inj.GetBaseRealDisplay()) return true; // 注射器非真品
                if (item.IsTag("ISSUE_LABEL")) return true;                              // 通用问题标记
                if (IsMislabeledWater(item)) return true;                                // 贴标水
                return false;
            }
            catch { return false; }
        }

        // 贴标水判定：水 feature 假显示 ≠ 真显示（照抄原生 CheckSoldDirtyWater 对比）
        private static bool IsMislabeledWater(GameItem item)
        {
            try
            {
                if (!item.IsTag("LIQUID_CONTAINER_TAG")) return false;
                var wf = Il2Cpp.WaterFeatureHelper.GetWaterFeature(item);
                if (wf == null) return false;
                return wf.GetBaseFakeDisplay() != wf.GetBaseRealDisplay();
            }
            catch { return false; }
        }

        // 假货价值：正品价 × 假货修正；无修正可读按一折（贴标水走这里 = 一折）
        private static int GetFakeItemValue(GameItem item)
        {
            try
            {
                int real = (int)item.GetCurrentValue();
                if (real <= 0) real = 1;
                var f = item.FindItemFeatureByID("realCounterfeit")
                     ?? item.FindItemFeatureByID("fakeGenuine")
                     ?? item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR");
                if (f != null)
                {
                    int v = real * (100 + f.GetFakeValueModifier()) / 100;
                    if (v > 0) return v;
                }
                return real / 10;   // 兜底一折（贴标水/无修正假货）
            }
            catch { return 1; }
        }

        public static void AbsorbItem(GameItem dice, GameItem absorbedItem)

        {

            try

            {

                if (dice == null || absorbedItem == null) return;



                // 获取被吸收物品的价值

                                int itemValue = 0;
                try
                {
                    itemValue = IsFakeOrTampered(absorbedItem)
                        ? GetFakeItemValue(absorbedItem)  // 假货/贴标水按假货价
                        : (int)absorbedItem.GetCurrentValue();
                }
                catch { }
                if (itemValue <= 0) itemValue = 1; // 最低1价值



                // 累计价值

                int currentValue = GetTagInt(dice, DICE_VALUE_TAG);

                int newValue = currentValue + itemValue;






                // v2 激活制（09-12）：吸收只累加价值，双击掷骰才触发事件
                SetTagInt(dice, DICE_VALUE_TAG, newValue);



                // 销毁被吸收的物品（吃掉）

                try

                {

                    absorbedItem.Destroy();

                }

                catch

                {

                    try { absorbedItem.parentInventory?.Expel(absorbedItem); } catch { }

                }

            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 吸收物品失败: " + ex.Message);

            }

        }



        // ===== v2 激活制（09-12 用户拍板）：吸收只累计价值，双击掷骰触发1个事件，门槛随掷骰上涨、打烊回落 =====
    private static GameItem _activeDice = null;   // 最近操作的骰子（双击/吸收时更新），OnGUI 卸载按钮目标
    private static bool _unloadConfirm = false;   // 卸载确认框模态

    // 双击掷骰：value >= threshold 才可摇；摇后 value-=threshold、threshold+=门槛、triggers+1、LastCost=本次门槛
    public static void RollDice(GameItem dice)
    {
        try
        {
            if (dice == null || !IsDice(dice)) return;
            int value = GetTagInt(dice, DICE_VALUE_TAG);
            int threshold = GetTagInt(dice, DICE_THRESHOLD_TAG);
            if (threshold < DICE_BASE_THRESHOLD) threshold = DICE_BASE_THRESHOLD;
            if (value < threshold)
            {
                try { StoreUIManager.Instance.Notify(LangHelper.T("价值不足，还需 " + (threshold - value) + " 才能掷骰", "Need " + (threshold - value) + " more value to roll"), "orange"); } catch { }
                return;
            }
            value -= threshold;
            SetTagInt(dice, DICE_VALUE_TAG, value);
            SetTagInt(dice, DICE_THRESHOLD_TAG, threshold + DICE_BASE_THRESHOLD);
            SetTagInt(dice, DICE_LAST_COST_TAG, threshold);
            int trig = GetTagInt(dice, DICE_TRIGGER_TAG);
            SetTagInt(dice, DICE_TRIGGER_TAG, trig + 1);
            string evtId = TriggerRandomEvent(dice); // 触发1个事件（明天来），返回事件id
            if (!string.IsNullOrEmpty(evtId))
            {
                SetTagString(dice, DICE_PENDING_EVENT_TAG, evtId);
                string zh = evtId;
                try { if (EventZhName.TryGetValue(evtId, out zh)) { } } catch { }
                try { StoreUIManager.Instance.Notify(LangHelper.T("掷骰成功！明日事件：" + zh, "Rolled! Tomorrow: " + evtId), "green"); } catch { }
            }
            _activeDice = dice;
            UpdateDicePanelTitle(dice, value);
        }
        catch { }
    }

    // 卸载已排队事件：RemoveAllEvent + 返还最近一次 LastCost × 50%（加回 value）
    public static void UnloadEvent(GameItem dice)
    {
        try
        {
            if (dice == null || !IsDice(dice)) return;
            string evtId = GetTagString(dice, DICE_PENDING_EVENT_TAG);
            if (string.IsNullOrEmpty(evtId)) return;
            try
            {
                var evtMgr = (StoreStation.instance != null) ? StoreStation.instance.storeEventManager : null;
                if (evtMgr != null) evtMgr.RemoveAllEvent(evtId); // 原生 API：清未来队列+触发日，已激活则停掉
            }
            catch { }
            int lastCost = GetTagInt(dice, DICE_LAST_COST_TAG);
            if (lastCost > 0)
            {
                int refund = (int)(lastCost * BuildConfig.DiceUninstallRefund / 100f);
                int value = GetTagInt(dice, DICE_VALUE_TAG);
                SetTagInt(dice, DICE_VALUE_TAG, value + refund);
                try { StoreUIManager.Instance.Notify(LangHelper.T("已卸载事件，返还 " + refund + " 价值", "Event unloaded, +" + refund + " value"), "green"); } catch { }
            }
            SetTagInt(dice, DICE_LAST_COST_TAG, 0);
            SetTagString(dice, DICE_PENDING_EVENT_TAG, "");
            _unloadConfirm = false;
            UpdateDicePanelTitle(dice, GetTagInt(dice, DICE_VALUE_TAG));
        }
        catch { }
    }

    // 双击拦截：物品是骰子 → 掷骰并拦掉原生分发（查看/食用/选中）
    public static bool PrefixDoubleClickAction(GameItem newItem, UnityEngine.Vector2 mousePosition)
    {
        try
        {
            if (newItem != null && IsDice(newItem))
            {
                RollDice(newItem);
                return false;
            }
        }
        catch { }
        return true;
    }

    // OnGUI 卸载按钮（Core.OnGUI 调用）：右下角，有排队事件才显示；点击弹确认框
    public static void DiceOnGUI()
    {
        try
        {
            GameItem dice = _activeDice;
            if (dice == null) return;
            string evtId = GetTagString(dice, DICE_PENDING_EVENT_TAG);
            if (string.IsNullOrEmpty(evtId)) return;
            float w = 170f, h = 30f;
            float x = 16f;
            float y = UnityEngine.Screen.height - h - 16f;
            if (!_unloadConfirm)
            {
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(x, y, w, h), LangHelper.T("卸载事件（返还50%）", "Unload Event (50% refund)")))
                {
                    _unloadConfirm = true;
                }
            }
            else
            {
                UnityEngine.GUI.Label(new UnityEngine.Rect(x, y - 24f, w, 22f), LangHelper.T("确认卸载明日事件？", "Unload tomorrow's event?"));
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(x, y, 80f, h), LangHelper.T("确认", "Yes"))) { UnloadEvent(dice); }
                if (UnityEngine.GUI.Button(new UnityEngine.Rect(x + 90f, y, 80f, h), LangHelper.T("取消", "No"))) { _unloadConfirm = false; }
            }
        }
        catch { }
    }

    // 每日打烊门槛回落：threshold>400 → -400（直到400为止）；value 永不受影响
    public static void RecedeDiceThreshold()
    {
        try
        {
            var emporium = EmporiumEntry.Instance;
            if (emporium == null) return;
            var invs = new GameInventory[] { emporium.backInvinvElement, emporium.backInvinvElementCounter, emporium.showcaseElement, emporium.invElement };
            foreach (var inv in invs)
            {
                if (inv == null) continue;
                foreach (var it in inv.childItems)
                {
                    if (it == null || !IsDice(it)) continue;
                    int tt = GetTagInt(it, DICE_THRESHOLD_TAG);
                    if (tt > DICE_BASE_THRESHOLD) SetTagInt(it, DICE_THRESHOLD_TAG, tt - DICE_BASE_THRESHOLD);
                }
            }
        }
        catch { }
    }

    // string 版 tag 读写（PENDING_EVENT 用）
    public static string GetTagString(GameItem item, string tag)
    {
        try
        {
            if (item == null || !item.IsTag(tag)) return "";
            var ts = item.GetTagReadonly(tag);
            return (ts != null) ? ts.GetString() : "";
        }
        catch { return ""; }
    }

    public static void SetTagString(GameItem item, string tag, string value)
    {
        try
        {
            if (item == null) return;
            if (!item.IsTag(tag)) item.EnableTag(tag, true);
            System.Action<TagState> sysAct = delegate (TagState state) { state.SetString(value); };
            var il2cppAct = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>((System.Delegate)sysAct);
            item.ModifyTag(tag, il2cppAct, false);
        }
        catch { }
    }

    // ===== 触发随机事件（暂时用日志占位，后续接入游戏原生事件） =====
    // 09-13 修复"吸收面板介绍"bug：原 shortDescription 首次备份，事件信息改为追加显示、事件清空后恢复原介绍（不再覆盖）
    private static readonly System.Collections.Generic.Dictionary<string, string> _diceOrigDesc = new System.Collections.Generic.Dictionary<string, string>();
    private static string DiceKey(GameItem dice) { try { return dice.identifier + "_" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dice).ToString(); } catch { return ""; } }

        private static void UpdateDicePanelTitle(GameItem dice, int value)

        {

            try

            {

                if (dice == null) return;

                int trig = GetTagInt(dice, DICE_TRIGGER_TAG);
                int threshold = GetTagInt(dice, DICE_THRESHOLD_TAG);
                if (threshold < DICE_BASE_THRESHOLD) threshold = DICE_BASE_THRESHOLD;
                string pendingEvt = GetTagString(dice, DICE_PENDING_EVENT_TAG);
                string evtZh = "";
                if (pendingEvt != "") { try { if (!EventZhName.TryGetValue(pendingEvt, out evtZh)) evtZh = pendingEvt; } catch { evtZh = pendingEvt; } }
                // 非容器：计数显示在物品名称上（悬停/列表可见）
                dice.SetName(LangHelper.T("命运骰子（价值" + value + " / 门槛" + threshold + " / 触发" + trig + "）", "Dice of Fate (value " + value + " / cost " + threshold + " / triggers " + trig + ")"));
                // 09-13 修复"吸收面板介绍"bug：事件说明追加显示、事件清空后恢复原介绍
                // 09-14 修复：说明文本每日累积（对象 HashCode 缓存失效 → 原描述被重复追加）——改用固定标记剥除上次事件文本
                const string EVT_MARKER = "【命运事件】";
                string curDesc = dice.shortDescription ?? "";
                int markerIdx = curDesc.IndexOf(EVT_MARKER);
                string baseDesc = markerIdx >= 0 ? curDesc.Substring(0, markerIdx) : curDesc;
                if (evtZh != "")
                    dice.shortDescription = (string.IsNullOrEmpty(baseDesc.Trim()) ? "" : baseDesc.TrimEnd('。', ' ') + "。") + EVT_MARKER + LangHelper.T("明日事件：" + evtZh + "。拖物品累积价值，双击掷骰。", "Tomorrow: " + evtZh + ". Drag to absorb, double-click to roll.");
                else
                    dice.shortDescription = baseDesc.TrimEnd('。', ' '); // 无事件恢复原介绍

                try { dice.SyncModifiedState(); } catch { }

            }

            catch { }

        }
}

}
