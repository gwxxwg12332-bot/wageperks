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

    public static class DestinyDice

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



        // ===== 事件汉化（identifier → 中文名/新闻标题/新闻描述） =====

        public static readonly Dictionary<string, string> EventZhName = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "违禁品严打",

            ["upperLevelParty"] = "上层派对",

            ["foodRecall"] = "食品召回",

            ["terroristAttack"] = "恐怖袭击",

            ["goldenTicketHunt"] = "金票搜寻",

            ["factoryFarmRenovation"] = "工厂农场翻新",

            ["hospitalRenovation"] = "医院翻新",

            ["commandLeak"] = "命令泄露",

            ["robbedTrain"] = "火车被劫",

            ["securityBreach"] = "安全漏洞",

            ["expiredImmunivaxDumping"] = "过期疫苗倾倒",



            // ===== v1.1.2 报纸汉化补全（EventListNormal/Special/Unique/Innate 全部事件） =====

            ["badNutrifruitHarvest"] = "营养果歉收",

            ["seedShortage"] = "种子短缺",

            ["medicalSuppliesShortage"] = "医疗用品短缺",

            ["water_treatment_breakdown"] = "水处理故障",

            ["scheduled_treatment_maintenance"] = "水处理维护",

            ["miner_return_ice_asteroid"] = "冰小行星运抵",

            ["lower_level_mob_justice"] = "下层区骚乱",

            ["storeroom_excavated"] = "仓库重见天日",

            ["miningExpeditionReturn"] = "采矿远征归来",

            ["powerPlantMaintenance"] = "电厂维护",

            ["stationWideBlackout"] = "全站停电",

            ["cruiseShipDocked"] = "游轮靠岸",

            ["oxymoreWave"] = "氧潮来袭",

            ["evidenceLockerBreakIn"] = "证据柜被闯",

            ["wanted3"] = "三级通缉",

            ["rent_day"] = "房租日",

            ["mortgage_payment"] = "房贷还款",

            ["cartel_visit"] = "卡特尔来访",

            ["lower_level_alcohol_shortage"] = "下层区酒荒",

            ["construction_project"] = "建筑项目",

            ["counterfeitMedicalSupplies"] = "假药被查获",

            ["meteorShower"] = "陨石雨",

            ["nutrifruitHarvest"] = "营养果丰收",

            ["powerShortage"] = "电力短缺",

            ["seedHeist"] = "种子被劫",



            ["lotteryDraw"] = "彩票开奖",

            ["lottery_draw"] = "彩票开奖",



            // ===== v1.2.0 新增：MoreEvents 补给/装饰事件 + 蛙哥自制事件 =====

            ["factoryFarmRenovationResupply"] = "工厂农场翻新补给",

            ["hospitalRenovationResupply"] = "医院翻新补给",

            ["crazyManYellingAtTheSky"] = "太空狂人呐喊",

            ["security_undercover_patrol"] = "安保部换装巡逻",

            ["nutrifruit_new_release"] = "营养果新品发布会",

            ["dumping_grounds_cleanup"] = "垃圾场大清理",

            ["black_market_sweep"] = "黑市码头大扫荡",

            ["upper_level_luxury_expo"] = "上层区奢华展",



            // ===== v1.2.1 复合型事件 =====

            ["black_market_arms_flow"] = "黑市军火流入",

            ["upper_level_auction_week"] = "上层区拍卖周",

            ["nutrifruit_crisis_hoard"] = "营养果危机囤积",

            ["train_heist_aftermath"] = "火车劫案余波",

            ["wandering_merchant_legacy"] = "流浪商人遗赠",

            ["drunk_riot"] = "酒鬼闹事",

        };

        public static readonly Dictionary<string, string> EventZhNews = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "社区动态",

            ["upperLevelParty"] = "上层区消息",

            ["foodRecall"] = "社区动态",

            ["terroristAttack"] = "紧急通报",

            ["goldenTicketHunt"] = "社区动态",

            ["factoryFarmRenovation"] = "商业消息",

            ["hospitalRenovation"] = "商业消息",

            ["commandLeak"] = "机密消息",

            ["robbedTrain"] = "突发新闻",

            ["securityBreach"] = "警报",

            ["expiredImmunivaxDumping"] = "社区动态",



            ["badNutrifruitHarvest"] = "社区动态",

            ["seedShortage"] = "社区动态",

            ["medicalSuppliesShortage"] = "社区动态",

            ["water_treatment_breakdown"] = "警报",

            ["scheduled_treatment_maintenance"] = "社区动态",

            ["miner_return_ice_asteroid"] = "商业消息",

            ["lower_level_mob_justice"] = "紧急通报",

            ["storeroom_excavated"] = "社区动态",

            ["miningExpeditionReturn"] = "商业消息",

            ["powerPlantMaintenance"] = "商业消息",

            ["stationWideBlackout"] = "警报",

            ["cruiseShipDocked"] = "上层区消息",

            ["oxymoreWave"] = "紧急通报",

            ["evidenceLockerBreakIn"] = "警报",

            ["wanted3"] = "紧急通报",

            ["rent_day"] = "财务消息",

            ["mortgage_payment"] = "财务消息",

            ["cartel_visit"] = "警报",

            ["lower_level_alcohol_shortage"] = "社区动态",

            ["construction_project"] = "商业消息",

            ["counterfeitMedicalSupplies"] = "警报",

            ["meteorShower"] = "突发新闻",

            ["nutrifruitHarvest"] = "社区动态",

            ["powerShortage"] = "警报",

            ["seedHeist"] = "社区动态",



            ["lotteryDraw"] = "社区动态",

            ["lottery_draw"] = "社区动态",



            ["factoryFarmRenovationResupply"] = "商业消息",

            ["hospitalRenovationResupply"] = "商业消息",

            ["crazyManYellingAtTheSky"] = "社区动态",

            ["security_undercover_patrol"] = "社区动态",

            ["nutrifruit_new_release"] = "商业消息",

            ["dumping_grounds_cleanup"] = "社区动态",

            ["black_market_sweep"] = "警报",

            ["upper_level_luxury_expo"] = "上层区消息",



            ["black_market_arms_flow"] = "警报",

            ["upper_level_auction_week"] = "上层区消息",

            ["nutrifruit_crisis_hoard"] = "警报",

            ["train_heist_aftermath"] = "突发新闻",

            ["wandering_merchant_legacy"] = "社区动态",

            ["drunk_riot"] = "紧急通报",

        };

        public static readonly Dictionary<string, string> EventZhDesc = new Dictionary<string, string>

        {

            ["contrabandCrackdown"] = "安保部门对辖区开展违禁品突击清查，风声很紧：违禁类商品难以出手，黑市渠道暂时收缩，相关货品价格波动剧烈。",

            ["upperLevelParty"] = "上层区举办盛大派对，阔佬们出手阔绰：奢侈品、宴会用品需求大增，高价商品更容易卖出好价钱。",

            ["foodRecall"] = "一批食品被检出问题并紧急召回：涉事食品价格大跌，可趁机低价囤货，其余食品类商品销量也会受影响。",

            ["terroristAttack"] = "辖区发生恐怖袭击，安保等级全面升级：安检严格、巡逻加强，治安类用品需求上升。",

            ["goldenTicketHunt"] = "有人在社区里藏了金票，居民疯狂翻找：杂物、箱柜类商品被大量抢购，出手容易。",

            ["factoryFarmRenovation"] = "工厂农场翻新工程启动：建材、机械和种植物资需求大涨，相关商品价格水涨船高。",

            ["hospitalRenovation"] = "医院翻新扩建工程开工：医疗物资、药品和设备需求激增，医用商品价格上涨。",

            ["commandLeak"] = "上层机密命令泄露，引发不小的混乱：居民情绪紧张，部分物资被抢购，供需关系被打乱。",

            ["robbedTrain"] = "一列运货火车被劫，大量货物流入黑市：黑市商品供应充足，倒卖利润可观。",

            ["securityBreach"] = "安保系统出现漏洞，巡逻力度减弱：治安类商品需求下降，出手价格走低。",

            ["expiredImmunivaxDumping"] = "过期疫苗被随意倾倒，引发民众不满：医疗类商品需求上升，卫生用品走俏。",



            ["badNutrifruitHarvest"] = "营养果大面积歉收：水果类商品价格看涨，供应紧张，进货渠道收窄。",

            ["seedShortage"] = "种子供应紧张：种植物资价格上涨，农业相关商品需求上升。",

            ["medicalSuppliesShortage"] = "医疗物资告急：药品和医疗用品价格飙升，供应缺口明显。",

            ["water_treatment_breakdown"] = "水处理站发生故障：净水设备、滤芯等商品需求大增，水相关物资价格上涨。",

            ["scheduled_treatment_maintenance"] = "水处理站例行维护：相关设备供应短期波动，维护类物资需求增加。",

            ["miner_return_ice_asteroid"] = "矿工从冰小行星满载而归：冰块、冷饮类物资供应增加，价格回落。",

            ["lower_level_mob_justice"] = "下层区爆发骚乱，治安形势紧张：安保、防身类商品需求上升，下层区交易风险增加。",

            ["storeroom_excavated"] = "一间废弃储藏室被挖开：里面发现不少存货，旧货和杂物类商品流入市场。",

            ["miningExpeditionReturn"] = "采矿队满载归来：矿石供应充足，矿类商品价格回落，进货机会多。",

            ["powerPlantMaintenance"] = "发电厂进入维护期：电力供应趋紧，电池、发电机类商品需求上升。",

            ["stationWideBlackout"] = "空间站发生大范围停电，一片漆黑：照明、电池类商品被抢购，价格飙升。",

            ["cruiseShipDocked"] = "豪华游轮靠站补给：阔佬们出手大方，奢侈品和高价商品容易脱手。",

            ["oxymoreWave"] = "罕见氧潮袭击空间站：氧气储备告急，氧气罐、呼吸面罩类商品价格大涨。",

            ["evidenceLockerBreakIn"] = "治安部证据柜被人闯入，重要证物失踪：安保加强，证据相关任务变多。",

            ["wanted3"] = "三级通缉犯在社区现身：安保高度戒备，防身类商品需求上升。",

            ["rent_day"] = "今天是交租日：租金已从账户扣除，今天现金会紧张，卖货回款更显重要。",

            ["mortgage_payment"] = "房贷还款日到了：一笔款项已扣除，注意留足现金周转。",

            ["cartel_visit"] = "卡特尔派人上门，来者不善：黑市生意可能受影响，违禁类交易要小心。",

            ["lower_level_alcohol_shortage"] = "一场大火烧毁了底层区的酿酒厂，库存损失惨重。酒鬼们开始躁动不安，有人已经在黑市门口排队。预计酒类价格将大幅上涨。",

            ["construction_project"] = "空间站启动新的建筑项目：建材、工具和机械类商品需求大增，相关价格水涨船高。",

            ["counterfeitMedicalSupplies"] = "一批假医疗用品被查出并追缴：药品市场动荡，真品医疗物资需求上升、价格走高。",

            ["meteorShower"] = "罕见陨石雨掠过空间站：陨石碎片散落各处，稀有矿物和纪念品市场火热。",

            ["nutrifruitHarvest"] = "营养果迎来大丰收：水果供应充足，价格回落，进货机会多。",

            ["powerShortage"] = "空间站电力短缺：电池、发电机和照明类商品被抢购，价格大涨。",

            ["seedHeist"] = "一批珍贵种子在运输途中被劫走：种子供应紧张，种植物资价格上涨。",



            ["lotteryDraw"] = "本周彩票开奖，有人一夜暴富：居民现金变多，消费意愿上升，好货容易出手。",

            ["lottery_draw"] = "本周彩票开奖，有人一夜暴富：居民现金变多，消费意愿上升，好货容易出手。",



            ["factoryFarmRenovationResupply"] = "应指挥部请求，补给船大批抵达，运载大量建材与物资，工厂翻新得以推进，材料与食品价格小幅回落。",

            ["hospitalRenovationResupply"] = "应指挥部请求，补给船大批抵达，运载大量建材与物资，医院翻新得以推进，医疗物资供应充足、价格回落。",

            ["crazyManYellingAtTheSky"] = "一位老人对着舷窗外面的飞船大喊大叫，还没弄明白太空里根本听不见声音。今日无事，唯有此新闻充数。",

            ["security_undercover_patrol"] = "安保部派出便衣巡逻员，暗中盯梢可疑交易。风声鹤唳之下，违禁品与武器买卖风险陡增，安保类装备需求上升、价格走高。",

            ["nutrifruit_new_release"] = "营养果公司举办盛大新品发布会，上层区的阔佬们纷纷抢购新口味零食，食品与零食类商品价格看涨。",

            ["dumping_grounds_cleanup"] = "空间站下令对垃圾场进行大规模清理，拾荒者们翻出大量废料与零件，市场上材料供应充足，价格回落。",

            ["black_market_sweep"] = "安保部突袭黑市码头，缴获大批走私货物并当众销毁。走私贩子纷纷蛰伏，货源紧缺，违禁品价格飙升。",

            ["upper_level_luxury_expo"] = "上层区正在举办奢华展会，享乐主义者们挥金如土，抢购珍稀好货，奢侈品价格一路走高。",



            ["black_market_arms_flow"] = "近期劫案中缴获的武器大量流入黑市，走私贩子急于低价出货，武器与违禁品价格回落。",

            ["upper_level_auction_week"] = "上层区举办为期一周的拍卖会，收藏家们争相竞拍珍稀好货，奢侈品价格走高，阔佬客户也更容易上门。",

            ["nutrifruit_crisis_hoard"] = "又一批营养果被召回引发恐慌，黑市投机者趁机囤积居奇，食品价格回落，违禁品价格飙升。",

            ["train_heist_aftermath"] = "火车劫案后，被劫的补给箱低价流入市场，安保部则加强了对门禁卡的管控，补给箱价格回落、门禁卡价格上涨。",

            ["wandering_merchant_legacy"] = "一位老流浪商人倒在了空间站门口，临终前把全部家当托付给当年帮助过他的当铺——一份礼物已送到你的柜台上。",

            ["drunk_riot"] = "下层区酒鬼群殴闹事，砸毁大量酒水，还伤及无辜路人，酒类库存受损、医疗物资需求上升。",

        };



        // 英文标题兜底：identifier 未命中/为空时，按 newsName 英文原文翻译（覆盖 TRANSPORT SHIP MISSING 等填充新闻）

        public static readonly Dictionary<string, string> TitleZhName = new Dictionary<string, string>

        {

            ["TRANSPORT SHIP MISSING"] = "运输船失踪",

            ["Attack On Our Soil!"] = "本土遇袭！",

            ["Command Database Leaked!"] = "命令数据库泄露！",

            ["Crazy Man Yelling At Space"] = "太空狂人呐喊",

            ["Golden Ticket Hunt"] = "金票搜寻",

            ["High Stakes Robbery!"] = "高额劫案！",

            ["Hospital Renovation Resupply Arrives"] = "医院翻新补给抵达",

            ["Immunivax™ Dumping"] = "过期疫苗倾倒",

            ["Lower Level Hospital Renovation"] = "下层区医院翻新",

            ["New Virus Found In Nutrifruit!"] = "营养果中发现新病毒！",

            ["Nutrifruit Co. Factory Farm Renovation"] = "营养果公司工厂翻新",

            ["Nutrifruit Renovation Resupply Arrives"] = "营养果翻新补给抵达",

            ["Security Starts War On Contraband"] = "安保部严打违禁品",

            ["Upcoming Upper Level Party!"] = "上层区派对在即！",

            ["LOWER LEVEL CELLARS RUN DRY!"] = "下层区酒窖告急！",

            ["SECURITY GOES UNDERCOVER!"] = "安保部换装巡逻！",

            ["NUTRIFRUIT™ NEW FLAVOR LAUNCH!"] = "营养果新品发布会！",

            ["DUMPING GROUNDS CLEANUP BEGINS!"] = "垃圾场大清理开始！",

            ["BLACK MARKET PORT SWEPT!"] = "黑市码头被扫荡！",

            ["UPPER LEVEL LUXURY EXPO!"] = "上层区奢华展！",

            ["BLACK MARKET ARMS FLOW!"] = "黑市军火流入！",

            ["UPPER LEVEL AUCTION WEEK!"] = "上层区拍卖周！",

            ["NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!"] = "营养果危机与黑市囤积！",

            ["TRAIN HEIST AFTERMATH!"] = "火车劫案余波！",

            ["WANDERING MERCHANT'S LEGACY!"] = "流浪商人遗赠！",

            ["DRUNKS RIOT IN THE LOWER LEVEL!"] = "下层区酒鬼闹事！",

        };

        public static readonly Dictionary<string, string> TitleZhDesc = new Dictionary<string, string>

        {

            ["TRANSPORT SHIP MISSING"] = "一艘运输船在航线中失去联系，货运班次被迫调整，市场供应预期收紧，相关物资价格或现波动。",

            ["Attack On Our Soil!"] = "一次蓄意袭击威胁到我们的土地与家园，安保部门已全面戒备，防身与安保类物资需求上升。",

            ["Command Database Leaked!"] = "指挥部的机密数据库遭到泄露，大量内部情报流入黑市，局势紧张，相关交易风险升高。",

            ["Crazy Man Yelling At Space"] = "一个疯子在空间站里对着太空大喊大叫，保安把他拖走了。今天没什么大事，也许这能给你的巡逻日添点乐子。",

            ["Golden Ticket Hunt"] = "皮莉·蓬卡糖果公司宣布在糖果里藏了金票，居民们疯狂翻找糖果包装，零食类商品被抢购一空。",

            ["High Stakes Robbery!"] = "一列运输列车遭遇高额劫案，大量补给箱与门禁卡被洗劫一空，黑市上突然多出不少来路不明的货。",

            ["Hospital Renovation Resupply Arrives"] = "应指挥部请求，补给船大批抵达，载着医院翻新所需的大量物资，建材与医疗物资供应充足。",

            ["Immunivax™ Dumping"] = "医疗部在一次仓储检查中扔掉了数百支过期的免疫疫苗，卫生隐患引发民众不满，医疗物资需求上升。",

            ["Lower Level Hospital Renovation"] = "下层区医院正在进行大规模翻新，两名客户被分流到附近诊所，医疗类物资需求上升。",

            ["New Virus Found In Nutrifruit!"] = "多名营养果™顾客报告食物中毒症状。若你持有受影响的营养果，建议立即处理——营养果类商品价格大跌。",

            ["Nutrifruit Co. Factory Farm Renovation"] = "著名水培公司营养果公司正在翻新其主力工厂农场！本地不安情绪蔓延，营养果供应短期波动。",

            ["Nutrifruit Renovation Resupply Arrives"] = "应指挥部请求，补给船大批抵达，载着营养果工厂翻新所需物资，种植物资供应充足。",

            ["Security Starts War On Contraband"] = "空间站指挥部发布新指令，命令安保部尽可能收缴违禁品，风声很紧，违禁类交易风险大增。",

            ["Upcoming Upper Level Party!"] = "一位本地享乐主义者开始为俱乐部的大型派对发送邀请函。专家表示这会让奢侈品消费迎来一波高峰。",

            ["LOWER LEVEL CELLARS RUN DRY!"] = "一场大火烧毁了底层区的酿酒厂，库存损失惨重。酒鬼们开始躁动不安，有人已经在黑市门口排队。预计酒类价格将大幅上涨。",

            ["SECURITY GOES UNDERCOVER!"] = "安保部派出便衣巡逻员，暗中盯梢可疑交易。风声鹤唳之下，违禁品与武器买卖风险陡增，安保类装备需求上升、价格走高。",

            ["NUTRIFRUIT™ NEW FLAVOR LAUNCH!"] = "营养果公司举办盛大新品发布会，上层区的阔佬们纷纷抢购新口味零食，食品与零食类商品价格看涨。",

            ["DUMPING GROUNDS CLEANUP BEGINS!"] = "空间站下令对垃圾场进行大规模清理，拾荒者们翻出大量废料与零件，市场上材料供应充足，价格回落。",

            ["BLACK MARKET PORT SWEPT!"] = "安保部突袭黑市码头，缴获大批走私货物并当众销毁。走私贩子纷纷蛰伏，货源紧缺，违禁品价格飙升。",

            ["UPPER LEVEL LUXURY EXPO!"] = "上层区正在举办奢华展会，享乐主义者们挥金如土，抢购珍稀好货，奢侈品价格一路走高。",

            ["BLACK MARKET ARMS FLOW!"] = "近期劫案中缴获的武器大量流入黑市，走私贩子急于低价出货，武器与违禁品价格回落。",

            ["UPPER LEVEL AUCTION WEEK!"] = "上层区举办为期一周的拍卖会，收藏家们争相竞拍珍稀好货，奢侈品价格走高，阔佬客户也更容易上门。",

            ["NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!"] = "又一批营养果被召回引发恐慌，黑市投机者趁机囤积居奇，食品价格回落，违禁品价格飙升。",

            ["TRAIN HEIST AFTERMATH!"] = "火车劫案后，被劫的补给箱低价流入市场，安保部则加强了对门禁卡的管控，补给箱价格回落、门禁卡价格上涨。",

            ["WANDERING MERCHANT'S LEGACY!"] = "一位老流浪商人倒在了空间站门口，临终前把全部家当托付给当年帮助过他的当铺——一份礼物已送到你的柜台上。",

            ["DRUNKS RIOT IN THE LOWER LEVEL!"] = "下层区酒鬼群殴闹事，砸毁大量酒水，还伤及无辜路人，酒类库存受损、医疗物资需求上升。",

        };



        public static string GetEventZh(string id)

        {

            string name;

            if (id != null && EventZhName.TryGetValue(id, out name)) return name;

            return null;

        }



        // 全局汉化兜底：任何走 GetDisplayName 的事件都按identifier汉化（骰子触发+游戏原生事件全覆盖）

        public static void PostfixGetDisplayName(StoreEvent __instance, ref string __result)

        {

            try

            {

                if (__instance == null || string.IsNullOrEmpty(__result)) return;

                string zh = GetEventZh(__instance.identifier);

                if (zh != null) __result = zh;

            }

            catch { }

        }



        // 全局汉化：任何事件排期（QueueFuturEvent）时改写字段（报纸 newsName/newsDescription 全覆盖）

        // 游戏原生事件+骰子触发+其他mod事件，只要走排期就汉化

        public static void PostfixQueueFuturEvent(StoreEvent storeEvent)

        {

            try

            {

                if (storeEvent != null && !string.IsNullOrEmpty(storeEvent.identifier))

                {

                    LocalizeEvent(storeEvent);

                }

            }

            catch { }

        }





        // ===== 报纸翻页（v1.1.2：最多4条/页，方向键翻页） =====

        // Il2Cpp方法List参数必须用 Il2CppSystem.Collections.Generic.List（编译类型匹配）

        public static bool _newsOpen;                       // 报纸是否打开

        public static Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent> _newsAllEvents; // 完整事件列表

        public static int _newsPage;                        // 当前页（0基）

        public static bool _newsPaging;                     // 翻页中（防止覆盖完整列表）


        private static UnityEngine.GameObject _newsPrevBtn; // 上一页按钮（UGUI）

        private static UnityEngine.GameObject _newsNextBtn; // 下一页按钮（UGUI）

        private static bool _newsBtnCreated;                // 按钮已创建标志



        // 报纸翻页可见按钮（Core.OnGUI 调用）：◀上一页 / 页码 / 下一页▶

        public static void NewsOnGUI()

        {

            try

            {

                if (!_newsOpen || _newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                if (pages <= 1) return;

                float w = 110f, h = 34f;

                float y = UnityEngine.Screen.height - 60f;

                float cx = UnityEngine.Screen.width / 2f;

                if (UnityEngine.GUI.Button(new UnityEngine.Rect(cx - 130f, y, w, h), LangHelper.T("◀ 上一页", "◀ Prev")))

                {

                    _newsPage--;

                    if (_newsPage < 0) _newsPage = pages - 1;

                    ApplyNewsPage();

                }

                UnityEngine.GUI.Label(new UnityEngine.Rect(cx - 15f, y, 30f, h), (_newsPage + 1) + "/" + pages);

                if (UnityEngine.GUI.Button(new UnityEngine.Rect(cx + 20f, y, w, h), LangHelper.T("下一页 ▶", "Next ▶")))

                {

                    _newsPage++;

                    if (_newsPage >= pages) _newsPage = 0;

                    ApplyNewsPage();

                }

            }

            catch { }

        }



        // 创建/显示翻页按钮（UGUI）：挂报纸面板下，跟随显隐

        public static void EnsureNewsButtons()

        {

            try

            {

                var mgr = Il2Cpp.NewsUIManager.Instance;

                if (mgr == null || mgr.uiGameObject == null) return;

                if (!_newsBtnCreated)

                {

                    var parent = mgr.uiGameObject.transform;

                    _newsPrevBtn = CreateNewsButton(parent, "WageNewsPrev", LangHelper.T("◀ 上一页", "◀ Prev"), new UnityEngine.Vector2(20f, 20f), new UnityEngine.Vector2(110f, 36f), PrevNewsPage);

                    _newsNextBtn = CreateNewsButton(parent, "WageNewsNext", LangHelper.T("下一页 ▶", "Next ▶"), new UnityEngine.Vector2(140f, 20f), new UnityEngine.Vector2(110f, 36f), NextNewsPage);

                    _newsBtnCreated = true;


                }

                SetNewsButtonsActive(true);

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] 创建按钮异常: " + ex.Message); }

        }



        static UnityEngine.GameObject CreateNewsButton(UnityEngine.Transform parent, string name, string text, UnityEngine.Vector2 pos, UnityEngine.Vector2 size, System.Action onClick)

        {

            var go = new UnityEngine.GameObject(name);

            var rt = go.AddComponent<UnityEngine.RectTransform>();

            rt.SetParent(parent, false);

            rt.anchorMin = new UnityEngine.Vector2(0f, 0f);

            rt.anchorMax = new UnityEngine.Vector2(0f, 0f);

            rt.pivot = new UnityEngine.Vector2(0f, 0f);

            rt.anchoredPosition = pos;

            rt.sizeDelta = size;

            var img = go.AddComponent<UnityEngine.UI.Image>();

            img.color = new UnityEngine.Color(0.08f, 0.08f, 0.12f, 0.92f);

            var btn = go.AddComponent<UnityEngine.UI.Button>();

            var txtGo = new UnityEngine.GameObject("Text");

            txtGo.transform.SetParent(go.transform, false);

            var txtRt = txtGo.AddComponent<UnityEngine.RectTransform>();

            txtRt.anchorMin = UnityEngine.Vector2.zero;

            txtRt.anchorMax = UnityEngine.Vector2.one;

            txtRt.offsetMin = UnityEngine.Vector2.zero;

            txtRt.offsetMax = UnityEngine.Vector2.zero;

            var tmp = txtGo.AddComponent<UnityEngine.UI.Text>();

            tmp.text = text;

            tmp.fontSize = 20;

            tmp.alignment = UnityEngine.TextAnchor.MiddleCenter;

            tmp.color = UnityEngine.Color.white;

            btn.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityEngine.Events.UnityAction>(onClick));

            return go;

        }



        public static void SetNewsButtonsActive(bool active)

        {

            try

            {

                if (_newsPrevBtn != null) _newsPrevBtn.SetActive(active);

                if (_newsNextBtn != null) _newsNextBtn.SetActive(active);

            }

            catch { }

        }



        public static void PrevNewsPage()

        {

            try

            {

                if (_newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage--;

                if (_newsPage < 0) _newsPage = pages - 1;

                ApplyNewsPage();

            }

            catch { }

        }



        public static void NextNewsPage()

        {

            try

            {

                if (_newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage++;

                if (_newsPage >= pages) _newsPage = 0;

                ApplyNewsPage();

            }

            catch { }

        }



        // 应用当前页（最终版）：完全复刻原生 PopulateUI 六步流程（ISIL NewsUIManager.txt 实锤）

        // PopulateUI = GetActiveEvents→过滤→隐藏4layout→按count选layout→ActivateLayout(激活layout+elements全隐藏+ClearElement)→填充循环(SetActive(true)+PopulateUI)

        // 之前只调SelectLayout→element全隐藏且无填充循环→空白。这里六步全部手动复刻。

        public static void ApplyNewsPage()

        {

            _newsPaging = true;

            try

            {

                var mgr = Il2Cpp.NewsUIManager.Instance;

                if (mgr == null || _newsAllEvents == null) return;

                var slice = SlicePage(_newsPage);

                // ① 隐藏全部4个layout（对齐PopulateUI）

                if (mgr.layout1 != null) mgr.layout1.SetActive(false);

                if (mgr.layout2 != null) mgr.layout2.SetActive(false);

                if (mgr.layout3 != null) mgr.layout3.SetActive(false);

                if (mgr.layout4 != null) mgr.layout4.SetActive(false);

                // ② 按count选layout+elements（对齐PopulateUI分支：>3→layout4, ==3→layout3, ==2→layout2, else→layout1）

                UnityEngine.GameObject layout;

                Il2CppSystem.Collections.Generic.List<Il2Cpp.NewsUIElement> elements;

                int cnt = slice.Count;

                if (cnt > 3) { layout = mgr.layout4; elements = mgr.layout4Elements; }

                else if (cnt == 3) { layout = mgr.layout3; elements = mgr.layout3Elements; }

                else if (cnt == 2) { layout = mgr.layout2; elements = mgr.layout2Elements; }

                else { layout = mgr.layout1; elements = mgr.layout1Elements; }

                // ③ 激活选中的layout（对齐ActivateLayout）

                if (layout != null) layout.SetActive(true);

                // ④ elements全部 SetActive(false) + ClearElement（对齐ActivateLayout遍历）

                if (elements != null)

                {

                    for (int j = 0; j < elements.Count; j++)

                    {

                        try

                        {

                            if (elements[j] != null)

                            {

                                if (elements[j].gameObject != null) elements[j].gameObject.SetActive(false);

                                elements[j].ClearElement();

                            }

                        }

                        catch { }

                    }

                }

                // ⑤ 填充循环：SetActive(true) + PopulateUI（对齐PopulateUI填充循环）

                if (elements != null)

                {

                    int n = UnityEngine.Mathf.Min(cnt, elements.Count);

                    for (int i = 0; i < n; i++)

                    {

                        try

                        {

                            if (elements[i] == null || elements[i].gameObject == null) continue;

                            elements[i].gameObject.SetActive(true);

                            elements[i].PopulateUI(slice[i]);

                        }

                        catch { }

                    }

                }


            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] ApplyPage异常: " + ex.Message); }

            _newsPaging = false;

        }



        // Patch NewsUIManager.PopulateUI Postfix：报纸填充时直接调 GetActiveEvents 取全量事件列表

        // （GetActiveEvents 是报纸数据源，返回全量；原生在填充层截断到4条——这里存全量供翻页）



        // 报纸连载注入钩子（独立mod SerialNewsMod 注册）：在报纸渲染前向事件列表注入连载内容

        public static System.Action<Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent>> NewsSerialInjector = null;



        // ===== 自造事件：下层区酒荒（进通用事件池 normalEventBlueprints，所有玩家随机遇到） =====

        private static bool _moddedEventsRegistered = false;



        public static void PostfixStoreEventOnDayStart()

        {

            try { EnsureModdedEventBlueprints(); }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] OnDayStart注入异常: " + ex.Message); }

            try { ModdedEventLinks(); }
            catch (Exception ex2) { Core.LogMsg("[Wage's Perks] 事件联动异常: " + ex2.Message); }
            // v2 激活制：每日打烊门槛回落一档（threshold>400 → -400，直到400）；value 不受影响
            try { RecedeDiceThreshold(); }
            catch (Exception ex3) { Core.LogMsg("[命运骰子] 门槛回落异常: " + ex3.Message); }

        }



        // 复合事件联动：活跃事件 → 安排客户/发放奖励（v1.2.1）

        private static bool _legacyGranted = false;

        public static void ModdedEventLinks()

        {

            var evtMgr = (Il2Cpp.StoreStation.instance != null) ? Il2Cpp.StoreStation.instance.storeEventManager : null;

            if (evtMgr == null) return;

            try

            {

                // 酒荒联动：收酒商提前到访（收购酒类）

                if (evtMgr.IsEventActive("lower_level_alcohol_shortage"))

                {

                    Il2Cpp.PlayerStore.Instance.QueueFuturClient("retired_winemaker", 0);


                }

                // 拍卖周联动：上层区阔佬客户到访

                if (evtMgr.IsEventActive("upper_level_auction_week"))

                {

                    Il2Cpp.PlayerStore.Instance.QueueFuturClient("ul_gun_buyer", 0);


                }

                // 流浪商人遗赠：送上战利品箱（每档只发一次）

                if (evtMgr.IsEventActive("wandering_merchant_legacy") && !_legacyGranted)

                {

                    _legacyGranted = true;

                    try

                    {

                        var box = Il2Cpp.DirectoryMaster.Item("sec_box", true);

                        if (box != null)

                        {

                            Il2Cpp.PlayerStore.Instance.AddDirectSellingItemToTable(box, false, false, false, 0);


                        }

                    }

                    catch (Exception exb) { Core.LogMsg("[Wage's Perks] 遗赠发箱失败: " + exb.Message); }

                }

            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件联动内部异常: " + ex.Message); }

        }



        // 幂等注册：OnDayStart 时确保蓝图已注入（normalEventBlueprints 此时已初始化）

        public static void EnsureModdedEventBlueprints()

        {

            try

            {

                if (_moddedEventsRegistered) return;

                var blueprints = Il2Cpp.StoreEventManager.normalEventBlueprints;

                if (blueprints == null) return;

                for (int i = 0; i < blueprints.Count; i++)

                {

                    try

                    {

                        if (blueprints[i] != null && blueprints[i].identifier == "lower_level_alcohol_shortage")

                        { _moddedEventsRegistered = true; return; }

                    }

                    catch { }

                }

                RegisterModdedEvent(blueprints, "lower_level_alcohol_shortage", () => CreateLowerLevelAlcoholShortage(), 10);

                RegisterModdedEvent(blueprints, "security_undercover_patrol", () => CreateSecurityUndercoverPatrol(), 8);

                RegisterModdedEvent(blueprints, "nutrifruit_new_release", () => CreateNutrifruitNewRelease(), 6);

                RegisterModdedEvent(blueprints, "dumping_grounds_cleanup", () => CreateDumpingGroundsCleanup(), 7);

                RegisterModdedEvent(blueprints, "black_market_sweep", () => CreateBlackMarketSweep(), 6);

                RegisterModdedEvent(blueprints, "upper_level_luxury_expo", () => CreateUpperLevelLuxuryExpo(), 5);

                // v1.2.1 复合型事件

                RegisterModdedEvent(blueprints, "black_market_arms_flow", () => CreateBlackMarketArmsFlow(), 5);

                RegisterModdedEvent(blueprints, "upper_level_auction_week", () => CreateUpperLevelAuctionWeek(), 5);

                RegisterModdedEvent(blueprints, "nutrifruit_crisis_hoard", () => CreateNutrifruitCrisisHoard(), 4);

                RegisterModdedEvent(blueprints, "train_heist_aftermath", () => CreateTrainHeistAftermath(), 5);

                RegisterModdedEvent(blueprints, "wandering_merchant_legacy", () => CreateWanderingMerchantLegacy(), 2);

                RegisterModdedEvent(blueprints, "drunk_riot", () => CreateDrunkRiot(), 5);

                _moddedEventsRegistered = true;

                Core.LogMsg("[Wage's Perks] 事件池注入: 12个mod事件已注册");

            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件池注入失败: " + ex.Message); }

        }



        private static void RegisterModdedEvent(Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEventBlueprint> blueprints, string id, System.Func<Il2Cpp.StoreEvent> factory, int weight)

        {

            try

            {

                System.Func<Il2Cpp.StoreEvent> sysFunc = factory;

                var il2cppFunc = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<Il2Cpp.StoreEvent>>((System.Delegate)sysFunc);

                var bp = new Il2Cpp.StoreEventBlueprint(il2cppFunc, weight, id);

                blueprints.Add(bp);


            }

            catch (Exception ex) { Core.LogMsg("[Wage's Perks] 事件注册失败 " + id + ": " + ex.Message); }

        }



        // 事件蓝图：下层区酒荒（用户样板文案）

        public static Il2Cpp.StoreEvent CreateLowerLevelAlcoholShortage()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "lower_level_alcohol_shortage";

            evt.newsName = "LOWER LEVEL CELLARS RUN DRY!";

            evt.newsDescription = "A fire gutted the lower-level brewery, wiping out months of stock. The drunks are restless, and a queue is already forming at the black market door. Alcohol prices are expected to surge.";

            evt.displayName = "Lower Level Alcohol Shortage";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ALCOHOL", 60, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：安保部换装巡逻（便衣盯梢，武器/违禁品风险升）

        public static Il2Cpp.StoreEvent CreateSecurityUndercoverPatrol()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "security_undercover_patrol";

            evt.newsName = "SECURITY GOES UNDERCOVER!";

            evt.newsDescription = "Security officers are patrolling in plain clothes to catch shady deals. Everyone is on edge - contraband and weapon trades are getting risky. Expect security gear and weapon prices to rise.";

            evt.displayName = "Undercover Patrol";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("WEAPON", 30, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 30, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：营养果新品发布会（食品/零食热销）

        public static Il2Cpp.StoreEvent CreateNutrifruitNewRelease()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "nutrifruit_new_release";

            evt.newsName = "NUTRIFRUIT™ NEW FLAVOR LAUNCH!";

            evt.newsDescription = "Nutrifruit Co. unveils its new flavor at a grand launch party. Upper-level folks are stocking up on snacks for the hype. Expect food and treat prices to rise.";

            evt.displayName = "Nutrifruit New Release";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("FOOD", 25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("TREAT", 25, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：垃圾场大清理（材料供应充足降价）

        public static Il2Cpp.StoreEvent CreateDumpingGroundsCleanup()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "dumping_grounds_cleanup";

            evt.newsName = "DUMPING GROUNDS CLEANUP BEGINS!";

            evt.newsDescription = "Station Command has ordered a massive cleanup of the dumping grounds. Scavengers are hauling out loads of scrap and parts - a golden opportunity for bargain hunters. Expect material prices to fall.";

            evt.displayName = "Dumping Grounds Cleanup";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MATERIAL", -25, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：黑市码头大扫荡（违禁品价格飙升）

        public static Il2Cpp.StoreEvent CreateBlackMarketSweep()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "black_market_sweep";

            evt.newsName = "BLACK MARKET PORT SWEPT!";

            evt.newsDescription = "Security raided the black market docks, seizing shipments and burning contraband. Smugglers are laying low and goods are scarce. Expect contraband prices to surge.";

            evt.displayName = "Black Market Sweep";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 100, evt.newsName, evt.displayName));

            return evt;

        }



        // 事件蓝图：上层区奢华展（奢侈品价格走高）

        public static Il2Cpp.StoreEvent CreateUpperLevelLuxuryExpo()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "upper_level_luxury_expo";

            evt.newsName = "UPPER LEVEL LUXURY EXPO!";

            evt.newsDescription = "The upper level is hosting a grand luxury expo. Hedonists are splurging on fine goods and rare treasures. Expect luxury prices to soar.";

            evt.displayName = "Luxury Expo";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("LUXURY_ITEM", 40, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：黑市军火流入（赃物流入，武器/违禁品降价）

        public static Il2Cpp.StoreEvent CreateBlackMarketArmsFlow()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "black_market_arms_flow";

            evt.newsName = "BLACK MARKET ARMS FLOW!";

            evt.newsDescription = "Stolen weapons from the recent heists are flooding the black market. Smugglers are dumping stock at bargain prices. Expect weapon and contraband prices to fall.";

            evt.displayName = "Black Market Arms Flow";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("WEAPON", -25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", -20, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：上层区拍卖周（奢侈品涨 + 上层客户到访）

        public static Il2Cpp.StoreEvent CreateUpperLevelAuctionWeek()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "upper_level_auction_week";

            evt.newsName = "UPPER LEVEL AUCTION WEEK!";

            evt.newsDescription = "The upper level is hosting a week-long auction. Collectors are bidding fiercely on rare goods and luxury items. Expect luxury prices to soar - and wealthy customers to visit your shop.";

            evt.displayName = "Upper Level Auction Week";

            evt.duration = 4;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.UPPER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("LUXURY_ITEM", 40, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("SUBSTANCE", 20, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：营养果危机囤积（食品跌 + 违禁品涨）

        public static Il2Cpp.StoreEvent CreateNutrifruitCrisisHoard()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "nutrifruit_crisis_hoard";

            evt.newsName = "NUTRIFRUIT CRISIS AND BLACK MARKET HOARDING!";

            evt.newsDescription = "Another Nutrifruit recall has sparked panic, while black marketeers hoard supplies to drive prices up. Expect food prices to fall and contraband prices to surge.";

            evt.displayName = "Nutrifruit Crisis Hoarding";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("FOOD", -30, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("CONTRABAND", 40, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：火车劫案余波（补给箱跌 + 门禁卡涨）

        public static Il2Cpp.StoreEvent CreateTrainHeistAftermath()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "train_heist_aftermath";

            evt.newsName = "TRAIN HEIST AFTERMATH!";

            evt.newsDescription = "Following the recent train heist, stolen supply crates are circulating at low prices, while Security has tightened control over access cards. Expect crate prices to fall and card prices to rise.";

            evt.displayName = "Train Heist Aftermath";

            evt.duration = 3;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("SUPPLY_CRATE", -25, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ACCESS_CARD", 25, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：流浪商人遗赠（奖励：治安战利品箱送上柜台）

        public static Il2Cpp.StoreEvent CreateWanderingMerchantLegacy()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "wandering_merchant_legacy";

            evt.newsName = "WANDERING MERCHANT'S LEGACY!";

            evt.newsDescription = "An old wandering merchant passed away at the station gates, leaving his belongings to the first pawnshop that helped him in hard times. A gift has been delivered to your shop.";

            evt.displayName = "Wandering Merchant's Legacy";

            evt.duration = 2;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.ALL;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MATERIAL", 5, evt.newsName, evt.displayName));

            return evt;

        }



        // 复合事件：酒鬼闹事（酒跌 + 医疗涨）

        public static Il2Cpp.StoreEvent CreateDrunkRiot()

        {

            var evt = new Il2Cpp.StoreEvent();

            evt.identifier = "drunk_riot";

            evt.newsName = "DRUNKS RIOT IN THE LOWER LEVEL!";

            evt.newsDescription = "A brawl between drunks spilled onto the streets, smashing crates of booze and injuring bystanders. Alcohol stocks are damaged, while medical demand rises.";

            evt.displayName = "Drunk Riot";

            evt.duration = 2;

            evt.eventType = Il2Cpp.StoreEvent.EventType.NORMALE;

            evt.eventArea = Il2Cpp.StoreEvent.EventArea.LOWER;

            if (evt.negociationDatas == null)

                evt.negociationDatas = new Il2CppSystem.Collections.Generic.List<Il2Cpp.NegociationData>();

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("ALCOHOL", -20, evt.newsName, evt.displayName));

            evt.negociationDatas.Add(new Il2Cpp.NegociationData("MEDICAL", 15, evt.newsName, evt.displayName));

            return evt;

        }



        public static void PostfixNewsPopulateUI()

        {

            try

            {

                _newsOpen = true;

                var ss = Il2Cpp.StoreStation.Instance;

                if (ss == null || ss.storeEventManager == null)

                {

                    Core.LogMsg("[报纸翻页] StoreStation/EventManager为空");

                    return;

                }

                try

                {

                    _newsAllEvents = ss.storeEventManager.GetActiveEvents();

                    // 同原生 b__25_0 过滤：移除 isHiddenFromNewsPaper（报纸隐藏）事件

                    if (_newsAllEvents != null)

                    {

                        for (int i = _newsAllEvents.Count - 1; i >= 0; i--)

                        {

                            try { if (_newsAllEvents[i].isHiddenFromNewsPaper) _newsAllEvents.RemoveAt(i); } catch { }

                        }

                    }

                    // 统一汉化所有事件（覆盖所有来源路径，不依赖QueueFuturEvent——启动预置等事件也能汉化）

                    if (_newsAllEvents != null)

                    {

                        for (int i = 0; i < _newsAllEvents.Count; i++)

                        {

                            try { LocalizeEvent(_newsAllEvents[i]); } catch { }

                        }

                    }

                    // 报纸连载注入钩子：独立mod（SerialNewsMod）注册后在此注入连载内容（配置外挂，不硬编码）

                    try { if (NewsSerialInjector != null) NewsSerialInjector(_newsAllEvents); } catch { }

                    _newsPage = 0;


                    // 初始即渲染当前页（含连载注入）：原生PopulateUI在Postfix之前已渲染（无连载），必须重渲染

                    ApplyNewsPage();

                    // 创建/显示翻页按钮（UGUI，挂报纸面板下）

                    EnsureNewsButtons();

                    // 不扩layout（克隆element会破坏原生布局导致空白）：原生4条布局，翻页按钮切换数据源

                    // 翻页时 SelectLayout(4条切片) 由按钮/方向键 触发

                }

                catch (Exception ex2)

                {

                    Core.LogMsg("[报纸翻页] GetActiveEvents异常: " + ex2.Message);

                }

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] PopulateUI异常: " + ex.Message); }

        }



        // 取第page页切片（页大小4——原生layout4只支持4条，翻页切换数据源）

        public static Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent> SlicePage(int page)

        {

            var slice = new Il2CppSystem.Collections.Generic.List<Il2Cpp.StoreEvent>();

            if (_newsAllEvents == null) return slice;

            int start = page * 4;

            for (int i = start; i < start + 4 && i < _newsAllEvents.Count; i++)

            {

                try { slice.Add(_newsAllEvents[i]); } catch { }

            }

            return slice;

        }







        // Patch NewsUIManager.ToggleUI Postfix：记录打开/关闭状态

        public static void PostfixNewsToggleUI()

        {

            try { _newsOpen = !_newsOpen; } catch { }

        }



        // Patch NewsUIManager.CloseUI Postfix：强制关闭状态

        public static void PostfixNewsCloseUI()

        {

            try { _newsOpen = false; } catch { }

            try { SetNewsButtonsActive(false); } catch { }

        }



        // Patch InputActionManager.Update Postfix：方向键翻页（轻量检查，报纸打开才动作）

        public static void PostfixNewsInputUpdate()

        {

            try

            {

                if (!_newsOpen || _newsAllEvents == null || _newsAllEvents.Count <= 4) return;

                bool left = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.LeftArrow);

                bool right = UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.RightArrow);

                if (!left && !right) return;


                int pages = (_newsAllEvents.Count + 3) / 4;

                _newsPage += right ? 1 : -1;

                if (_newsPage < 0) _newsPage = pages - 1;

                if (_newsPage >= pages) _newsPage = 0;

                ApplyNewsPage();

            }

            catch (Exception ex) { Core.LogMsg("[报纸翻页] 异常: " + ex.Message); }

        }

        public static void LocalizeEvent(StoreEvent evt)

        {

            try

            {

                if (evt == null || string.IsNullOrEmpty(evt.identifier)) return;

                // 英文模式：跳过汉化覆写，显示原生英文事件（游戏设英文即英文）

                if (LangHelper.IsEnglish()) return;

                string id = evt.identifier;

                string zhName, zhNews, zhDesc;

                if (EventZhName.TryGetValue(id, out zhName))

                {

                    evt.displayName = zhName;

                    if (EventZhNews.TryGetValue(id, out zhNews)) evt.newsName = zhNews;

                    if (EventZhDesc.TryGetValue(id, out zhDesc)) evt.newsDescription = zhDesc;


                }

                else if (!string.IsNullOrEmpty(evt.newsName) && TitleZhName.TryGetValue(evt.newsName, out zhName))

                {

                    // 英文标题兜底：无 identifier 匹配（如填充新闻 TRANSPORT SHIP MISSING）

                    evt.displayName = zhName;

                    evt.newsName = zhName;

                    string zhDesc2;

                    if (TitleZhDesc.TryGetValue(evt.newsName, out zhDesc2)) evt.newsDescription = zhDesc2;


                }

                else

                { }

            }

            catch (Exception exl) { Core.LogMsg("[命运骰子] 汉化事件失败: " + exl.Message); }

        }



        public static string TriggerRandomEvent(GameItem dice)
        {

            try

            {

                // 从游戏原生事件蓝图池随机挑一个，明天触发（参照MoreEvents模式）

                var evtMgr = (StoreStation.instance != null) ? StoreStation.instance.storeEventManager : null;

                if (evtMgr == null)

                {


                    return null;

                }

                var blueprints = StoreEventManager.normalEventBlueprints;

                if (blueprints == null || blueprints.Count == 0)

                {


                    return null;

                }



                // 随机挑一个蓝图（排除当前正在进行的，避免重复）

                StoreEventBlueprint bp = null;

                for (int attempt = 0; attempt < 5; attempt++)

                {

                    int idx = UnityEngine.Random.Range(0, blueprints.Count);

                    var candidate = blueprints[idx];

                    if (candidate == null || candidate.storeEventFunc == null) continue;

                    try

                    {

                        if (!evtMgr.IsEventActive(candidate.identifier)) { bp = candidate; break; }

                    }

                    catch { bp = candidate; break; }

                }

                if (bp == null)

                {

                    for (int i = 0; i < blueprints.Count; i++)

                    {

                        var candidate = blueprints[i];

                        if (candidate != null && candidate.storeEventFunc != null) { bp = candidate; break; }

                    }

                }

                if (bp == null) { Core.LogMsg("[命运骰子] 事件蓝图全为空"); return null; }



                StoreEvent evt = bp.storeEventFunc.Invoke();

                if (evt == null) { Core.LogMsg("[命运骰子] 事件实例化失败"); return null; }



                // 汉化事件文本（显示名/新闻标题/新闻描述）

                LocalizeEvent(evt);



                // 明天触发（IsKnown=true，玩家能看到预告）

                evtMgr.QueueFuturEvent(evt, 1, true);




                // 触发计数（写标签 + 更新面板标题）

                if (dice != null)

                {

                    int trig = GetTagInt(dice, DICE_TRIGGER_TAG);

                    SetTagInt(dice, DICE_TRIGGER_TAG, trig + 1);
                    int cur = GetTagInt(dice, DICE_VALUE_TAG);

                    UpdateDicePanelTitle(dice, cur);

                }
                return evt.identifier;
            }

            catch (Exception ex)

            {

                Core.LogMsg("[命运骰子] 触发事件异常: " + ex.Message);

            
                return null;
            }
                    }

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

            // 只允许：物品拖到骰子上（骰子是目标）。骰子拖到别的物品上 → 不拦截（走原版逻辑）

            if (IsDice(targetItem) && __instance != null && !IsDice(__instance))

            {
                // 根因修复：MayTarget 不止拖拽链调用（hover高亮/选择链也会调且鼠标未按住）——只有拖拽中才允许吸收
                var dragHandler = Il2Cpp.ItemMouseDragHandler.current;
                bool dragging = (dragHandler != null && dragHandler.IsDraggingItem);
                // 2.5.40 多选批量吸收：游戏原生框选组拖拽（ItemMultiSelectHandler）不走单拖 handler，补组拖判定
                if (!dragging)
                {
                    var multiHandler = Il2Cpp.ItemMultiSelectHandler.current;
                    if (multiHandler != null)
                    {
                        try { dragging = multiHandler.IsGroupDraggingItem(__instance); } catch { }
                    }
                }
                if (!dragging) return true;

                // 拖动中（鼠标左键按住）不吸收——MayTarget在拖动过程中也会被调用，松手才触发吸收

                if (UnityEngine.Input.GetMouseButton(0)) { return true; }

                // 防误触修复：只有按住Shift拖到骰子才吸收（普通拖放落点在骰子上→不拦截→走原版148被拒→物品弹回）

                // 直接吸收：游戏拖放链(ItemMouseDragHandler)在MayTarget=true后走148——

                // 148要求目标.inventory(+400)非空，非容器骰子没有→流程放弃→Target永不触发

                // 所以在MayTarget这一步直接执行吸收（DoAbsorb有0.5s冷却+销毁物品防重复）

                DoAbsorb(__instance, targetItem);

                __result = true;

                return false;

            }

            return true;

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
                // 2.5.40 多选批量吸收：组拖判定补充
                if (!dragging)
                {
                    var multiHandler = Il2Cpp.ItemMultiSelectHandler.current;
                    if (multiHandler != null)
                    {
                        try { dragging = multiHandler.IsGroupDraggingItem(__instance); } catch { }
                    }
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



        // ===== 归属校验：物品是否属于非玩家库存（未购买/他人库存 → 拒绝吸收）=====
        // 判定依据：物品是否在博士夜晚商店库存（afterhourInventory）里——该库存的物品未购买、不属于玩家。
        // 玩家体系（背包/柜台/隐藏库存/玩家容器/地板）一律放行，防误伤。
        private static bool IsNonPlayerOwned(GameItem item)
        {
            try
            {
                if (item == null) return false;
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



        private static GameItem CreateRegisteredDice()

        {

            try

            {

                GameItem item = CreateDestinyDice();

                if (item != null) return item;

                try { return DirectoryMaster.Item("simple_backpack", true); } catch { }

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

