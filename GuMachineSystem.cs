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
public static class GuMachineSystem
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
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] SpriteFromPixels 异常: " + ex.Message); return null; }
    }
    // 拦截 RenderHandler.LoadFromAtlas：custom_atlas + 我方图标名 → 返回自定义 sprite
    public static bool PrefixLoadFromAtlas(string atlasPath, string name, ref Sprite __result)
    {
        try
        {
            if (atlasPath == ICON_ATLAS)
            {
                if (name == GU_ICON && _guSprite != null) { __result = _guSprite; return false; }
                if (name == AI_ICON && _aiSprite != null) { __result = _aiSprite; return false; }
                if (name == PROTECTOR_ICON && _protectorSprite != null) { __result = _protectorSprite; return false; }
                if (name == AI_MODULE_ICON && _aiModuleSprite != null) { __result = _aiModuleSprite; return false; }
            }
        }
        catch { }
        return true;
    }

    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            RegisterOne(dir, GU_MACHINE_ID, ref _guFactory, CreateGuMachine);
            RegisterOne(dir, AI_GENERATOR_ID, ref _aiFactory, CreateAiGenerator);
            RegisterOne(dir, PROTECTOR_ID, ref _protectorFactory, CreateProtectorCore);
            RegisterOne(dir, AI_MODULE_ID, ref _aiModuleFactory, CreateAiModule);
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 注册异常: " + ex.Message); }
    }

    private static void RegisterOne(ItemDirectory dir, string id, ref Il2CppSystem.Func<GameItem> cache, Func<GameItem> factory)
    {
        try
        {
            if (((Directory<GameItem>)(object)dir).Has(id)) return;
            if (cache == null)
            {
                System.Func<GameItem> sf = () => factory();
                cache = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItem>>((System.Delegate)sf);
            }
            bool ok = ((Directory<GameItem>)(object)dir).Add(id, cache);
            Core.LogMsg("[养蛊机] " + (ok ? "★ " + id + " 已注册" : "⚠️ " + id + " 注册失败"));
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 注册 " + id + " 异常: " + ex.Message); }
    }

    // 应用图标：自定义 sprite 可用 → custom_atlas；否则借用原生物品外观
    private static void ApplyIcon(GameItem it, string iconKey, string nativeId)
    {
        Sprite sp = null;
        try
        {
            if (iconKey == GU_ICON) sp = _guSprite;
            else if (iconKey == AI_ICON) sp = _aiSprite;
            else if (iconKey == PROTECTOR_ICON) sp = _protectorSprite;
            else if (iconKey == AI_MODULE_ICON) sp = _aiModuleSprite;
        }
        catch { }
        if (sp != null)
        {
            try { it.SetSpriteAndShape(ICON_ATLAS, iconKey); return; } catch { }
        }
        BorrowNativeSprite(it, nativeId);
    }

    // 借用原生 sprite（运行时读原生物品 spriteAtlasPath/spritePath——public 属性，拆包 09-15 实锤）
    private static void BorrowNativeSprite(GameItem it, string nativeId)
    {
        try
        {
            var tpl = DirectoryMaster.Item(nativeId, true);
            if (tpl != null)
            {
                string atlas = "";
                string sprite = "";
                try { atlas = tpl.spriteAtlasPath ?? ""; } catch { }
                try { sprite = tpl.spritePath ?? ""; } catch { }
                if (!string.IsNullOrEmpty(atlas) && !string.IsNullOrEmpty(sprite))
                {
                    it.SetSpriteAndShape(atlas, sprite);
                    return;
                }
            }
        }
        catch { }
        try { it.SetSpriteAndShape("custom_atlas", "custom_storage_box_sprite"); } catch { }
    }

    private static GameItem CreateGuMachine()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it, GU_ICON, "blender");
            // 窗口：模组舱 2×2（照 CustomStorageContainer.CreateContainer 容器式模板——已验证先例）
            try
            {
                var invWindow = DirectoryUtils.CreateInventoryWindow(8, 8, true); // 09-15 用户反馈：模组舱 2x2 太小，最少 8x8
                PixelWindow cw = invWindow.Item1;
                GameInventory inv = invWindow.Item2;
                if (cw != null && inv != null)
                {
                    it.SetContentWindow(cw);
                    inv.identifier = GU_MACHINE_ID;
                }
            }
            catch { }
            it.EnableTag("STANDARD_MACHINE_TAG");
            it.SetGameItemType("MACHINE");
            it.SetName(LangHelper.T("蛙哥养蛊机", "Wage's Swarm Forge"));
            SetField(it, "_identifier_k__BackingField", GU_MACHINE_ID);
            SetField(it, "_identifierName_k__BackingField", "TYPE-STRING_" + GU_MACHINE_ID);
            it.shortDescription = LangHelper.T("蛙哥养蛊机——传奇当铺主蛙哥（Wage）留下的炼蛊机器：打烊充能（3天）后自动炼蛊，舱内≥2模组 → 1强化模组（三属性之和×1.2，上限150%，产出在机器舱内，打开机器取出）；每次打烊舱内≥2电池自动互吞（高特性存活，电量/容量/自充/价值叠加）。违禁原因：以活体模组与电池互相吞噬炼制，安保部明令禁止。", "Wage's Swarm Forge - a forging machine left by Wage, the legendary pawnshop owner: after 3 days of charging (at close), auto-forges >=2 modules in bay -> 1 empowered module (sum of 3 stats x1.2, cap 150%, output stays in the machine bay - open the machine to take it); each close, >=2 batteries in bay auto-devour (higher stats survive, energy/capacity/recharge/value stack). Contraband: live-module & battery cannibalism is outlawed by Security.");
            it.longDescription = LangHelper.T("蛙哥养蛊机——传奇当铺主蛙哥（Wage）留下的炼蛊机器：打烊充能（3天）后自动炼蛊，舱内≥2模组 → 1强化模组（三属性之和×1.2，上限150%，产出在机器舱内，打开机器取出）；每次打烊舱内≥2电池自动互吞（高特性存活，电量/容量/自充/价值叠加）。违禁原因：以活体模组与电池互相吞噬炼制，安保部明令禁止。", "Wage's Swarm Forge - a forging machine left by Wage, the legendary pawnshop owner: after 3 days of charging (at close), auto-forges >=2 modules in bay -> 1 empowered module (sum of 3 stats x1.2, cap 150%, output stays in the machine bay - open the machine to take it); each close, >=2 batteries in bay auto-devour (higher stats survive, energy/capacity/recharge/value stack). Contraband: live-module & battery cannibalism is outlawed by Security.");
            it.unitValue = BuildConfig.GuMachinePrice; it.unitBaseValue = BuildConfig.GuMachinePrice; // 拆包实锤 09-15：原生属性（SetField 反射字段名错静默失败）
            try { Il2Cpp.ContrabandHelper.InitContrabandItem(it, 3); } catch { } // 09-16 高级违禁品(level 3)
            // 09-19 P1：充能差分基准日——创建时记当天（博士上货即创建；读档随 tag 保留）
            try { RobinCrusoePerk.SetTagIntValue(it, GU_LAST_DAY_TAG, DeterministicSchedule.CurrentDay); } catch { }
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 创建养蛊机失败: " + ex.Message); return null; }
    }

    private static GameItem CreateAiGenerator()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it, AI_ICON, "furnace");
            // 窗口：模组舱 8×8（照养蛊机容器式模板）
            try
            {
                var invWindow = DirectoryUtils.CreateInventoryWindow(8, 8, true);
                PixelWindow cw = invWindow.Item1;
                GameInventory inv = invWindow.Item2;
                if (cw != null && inv != null)
                {
                    it.SetContentWindow(cw);
                    inv.identifier = AI_GENERATOR_ID;
                }
            }
            catch { }
            it.EnableTag("STANDARD_MACHINE_TAG");
            it.SetGameItemType("MACHINE");
            it.SetName(LangHelper.T("蛙哥不稳定AI生成器", "Wage's Unstable AI Generator"));
            SetField(it, "_identifier_k__BackingField", AI_GENERATOR_ID);
            SetField(it, "_identifierName_k__BackingField", "TYPE-STRING_" + AI_GENERATOR_ID);
            it.shortDescription = LangHelper.T("蛙哥不稳定AI生成器——蛙哥（Wage）遗作：打烊自动抽卡（每天1次，需舱内≥2模组）：装保护器=阉割版（100%成功≤75%，消耗1个）/ 不装=不稳定版（50%成功≤150%，50%失败全报废）。违禁原因：私自合成自主AI模组，触犯空间站AI管制令。", "Wage's Unstable AI Generator - Wage's legacy: auto-draw at close (1/day, needs >=2 modules): with protector = Stable (100% success <=75%, consumes 1) / without = Unstable (50% success <=150%, 50% fail & total scrap). Contraband: unsanctioned AI synthesis violates station AI directives.");
            it.longDescription = LangHelper.T("蛙哥不稳定AI生成器——蛙哥（Wage）遗作：打烊自动抽卡（每天1次，需舱内≥2模组）：装保护器=阉割版（100%成功≤75%，消耗1个）/ 不装=不稳定版（50%成功≤150%，50%失败全报废）。违禁原因：私自合成自主AI模组，触犯空间站AI管制令。", "Wage's Unstable AI Generator - Wage's legacy: auto-draw at close (1/day, needs >=2 modules): with protector = Stable (100% success <=75%, consumes 1) / without = Unstable (50% success <=150%, 50% fail & total scrap). Contraband: unsanctioned AI synthesis violates station AI directives."); // 09-15 原生属性
            it.unitValue = BuildConfig.AiGenPrice; it.unitBaseValue = BuildConfig.AiGenPrice; // 拆包实锤 09-15：原生属性
            try { Il2Cpp.ContrabandHelper.InitContrabandItem(it, 3); } catch { } // 09-16 高级违禁品(level 3)
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 创建生成器失败: " + ex.Message); return null; }
    }

    private static GameItem CreateProtectorCore()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it, PROTECTOR_ICON, "energy_credit");
            it.SetName(LangHelper.T("蛙哥保护器核心", "Wage's Protector Core"));
            SetField(it, "_identifier_k__BackingField", PROTECTOR_ID);
            SetField(it, "_identifierName_k__BackingField", "TYPE-STRING_" + PROTECTOR_ID);
            it.shortDescription = LangHelper.T("蛙哥保护器核心——蛙哥（Wage）研制的稳定器：放入蛙哥不稳定AI生成器舱即阉割版（不报废，上限75%），每次抽卡消耗1个；博士夜晚商店限量出售（1500）", "Wage's Protector Core - a stabilizer made by Wage: place in Wage's Unstable AI Generator bay for Stable mode (no scrap, cap 75%), 1 consumed per draw; limited stock at Doctor night shop (1500)");
            it.longDescription = LangHelper.T("蛙哥保护器核心——蛙哥（Wage）研制的稳定器：放入蛙哥不稳定AI生成器舱即阉割版（不报废，上限75%），每次抽卡消耗1个；博士夜晚商店限量出售（1500）", "Wage's Protector Core - a stabilizer made by Wage: place in Wage's Unstable AI Generator bay for Stable mode (no scrap, cap 75%), 1 consumed per draw; limited stock at Doctor night shop (1500)"); // 09-15 原生属性
            it.unitValue = BuildConfig.ProtectorPrice; it.unitBaseValue = BuildConfig.ProtectorPrice; // 拆包实锤 09-15：原生属性
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 创建保护器失败: " + ex.Message); return null; }
    }

    // 09-15 "不稳定AI模组"（生成器产出物——纯模组实物，无窗口；属性由抽卡写入）
    private static GameItem CreateAiModule()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            // 09-15 拆包实锤：原生模组完整创建链——SetGameItemType("MODULE") 双写（EnableTag + itemTypes.Add），机器模组舱判定 itemTypes.Contains("MODULE") 才接受
            // InitModuleItem 一次补齐 itemTypes + MODULE_TAG + MODULE_TYPE + BONUS×3 + MODULE_TIER_INT + SHIP_MODULE_TAG + ADDITIONAL_EFFECT_STRING
            // 09-16 拆包最终根因：机器模组舱接受 = ContainerHelper.AllowOnlyTaggedItemsOr(网格, ["MODULE_TYPE_UNIVERSAL","MODULE_TYPE_<机器专属>"], true)
            // InitModuleItem 首参 = moduleType tag——原生传 "MODULE_TYPE_UNIVERSAL"（ModuleDirectory L1222-1238 实锤），非模组 id
            // 之前传 "system_module_overclock" → EnableTag("system_module_overclock") = 幽灵 tag → 两个接受 tag 都不命中 → 拒绝入舱
            try { Il2Cpp.ModuleHelper.InitModuleItem(it, "MODULE_TYPE_UNIVERSAL", 0, 0, 0, true, AI_MODULE_ID, 1); } catch { }
            // 09-16 根因修复：identifier 未设（CreateEmptyItem 后 null）→ MayHaveValidInventorySlot 委托链（湿气农场 b__0_1 比较 [0x448]）NRE → 放置判定失败放不进
            try { it.identifier = AI_MODULE_ID; } catch { }
            // 09-16 显式补全（防 InitModuleItem 在 Il2CppInterop 下静默失效）：SetGameItemType 双写 = itemTypes.Add("MODULE") + EnableTag("MODULE")
            try { it.SetGameItemType("MODULE"); } catch { }
            if (!it.IsTag("MODULE_TAG")) { try { it.EnableTag("MODULE_TAG"); } catch { } } // 兜底（InitModuleItem 失败也不失模组身份）
            ApplyIcon(it, AI_MODULE_ICON, "system_module_overclock"); // 09-15 修复：之前误用 AI_ICON（生成器图标）——AI 模组必须用专属 ai_module 图标
            // 09-16 用户拍板：AI 模组 shape 2×2（mod 自建机器舱 8×8 可容纳；不借原生 1×1 shape）
            try { var gsb = new GridShapeBuilder(); gsb.SetDataFill(2, 2); it.SetShape(gsb.Build()); } catch { }
            it.SetName(LangHelper.T("不稳定AI模组", "Unstable AI Module"));
            it.shortDescription = LangHelper.T("由 AI 生成器抽卡产出：属性 = 投入模组之和（上限：阉割75% / 不稳定150%），可装机器或出售。违禁原因：未经许可的自主AI模组。", "Produced by AI generator draws: stats = sum of input modules (cap: Stable 75% / Unstable 150%), installable in machines or sellable. Contraband: unsanctioned autonomous AI module.");
            it.longDescription = LangHelper.T("由 AI 生成器抽卡产出：属性 = 投入模组之和（上限：阉割75% / 不稳定150%），可装机器或出售。违禁原因：未经许可的自主AI模组。", "Produced by AI generator draws: stats = sum of input modules (cap: Stable 75% / Unstable 150%), installable in machines or sellable. Contraband: unsanctioned autonomous AI module.");
            it.unitValue = 0; it.unitBaseValue = 0; // 价值由合成时吞噬原料之和定
            try { Il2Cpp.ContrabandHelper.InitContrabandItem(it, 3); } catch { } // 09-16 高级违禁品(level 3)
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[养蛊机] 创建AI模组失败: " + ex.Message); return null; }
    }

    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch { }
    }


    // ============================================================
    // 养蛊机功能（09-15 段3a）：打烊充能（wage_gu_charge 0-3）+ 满3自动炼蛊
    // 挂点：StoreEventManager.OnDayStart Postfix（吞噬季同挂点，多 postfix 顺序执行）
    // ============================================================
    public const string GU_CHARGE_TAG = "wage_gu_charge";
    // 09-19 P1 充能天数差分：LAST_DAY 记录上次充能日，同日打烊不重复 +1（根治读档后充能卡住/重复计）
    public const string GU_LAST_DAY_TAG = "wage_gu_last_day";

    public static void OnDayStartPostfix()
    {
        try
        {
            int day = DeterministicSchedule.CurrentDay;
            // 09-19 修复：养蛊机充能原切 ModHook.OnHandlingNightlyServicesLate——钩子从未注册且 Demo 版 ModHook 不触发（09-17 注释"事件 0 触发"）
            // → 挂回 OnDayStart Postfix（与电池互吞/AI 抽卡同挂点，时序一致）
            foreach (var gu in FindGuMachines())
            {
                TryGuMachineTick(gu, day);
            }
            foreach (var gen in FindAiGenerators())
            {
                TryAiGeneratorTick(gen, day);
            }
        }
        catch { }
    }

    // 全店找养蛊机（09-19 P4：FindAllItem(true) 全店扫描替代 EmporiumEntry 遍历——场景迁移后不丢机器）
    private static System.Collections.Generic.List<GameItem> FindGuMachines()
    {
        var result = new System.Collections.Generic.List<GameItem>();
        try
        {
            var all = Il2Cpp.PlayerStore.Instance.FindAllItem(true);
            if (all != null) foreach (var it in all) { if (it != null && it.identifier == GU_MACHINE_ID) result.Add(it); }
        }
        catch { }
        return result;
    }

    // 读养蛊机/生成器舱内网格（容器式单网格窗口：contentWindow.childElement 就是舱——09-15 实测 GetModuleInv 期待机器式双网格对容器式返回 null，功能全断，改回本读法）
    internal static GameGridInventory GetGuGrid(GameItem gu) // internal：供 BatteryCannibalism 收集养蛊机舱内电池
    {
        try
        {
            if (gu == null || gu.contentWindow == null || gu.contentWindow.childElement == null) return null;
            return gu.contentWindow.childElement.Cast<GameGridInventory>();
        }
        catch { return null; }
    }

    // 09-15 充能锁定：舱内所有 MODULE_TAG 模组打 MODULE_STUCK_TAG（充能中禁止拖出）
    private static void LockGuModules(GameGridInventory grid)
    {
        try
        {
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (isMod) { try { m.EnableTag("MODULE_STUCK_TAG"); } catch { } }
            }
        }
        catch { }
    }

    // 09-19 修复：充能归零时摘除舱内全部 STUCK_TAG——原实现只打不摘，产出模组永久锁死在舱内（炼蛊器卡住根因）
    private static void UnlockGuModules(GameGridInventory grid)
    {
        try
        {
            if (grid == null || grid.childItems == null) return;
            foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                try { if (m.IsTag("MODULE_STUCK_TAG")) m.DisableTag("MODULE_STUCK_TAG"); } catch { }
            }
        }
        catch { }
    }

    // 09-15 UI 兜底（拆包：GameItemElement : GameItem）：拖拽起点拦截——鼠标下物品带 MODULE_STUCK_TAG → 不开始拖
    public static bool PrefixItemMouseDragHandlerStartDrag(Il2Cpp.ItemMouseDragHandler __instance)
    {
        try
        {
            if (__instance != null && __instance.currentItem != null)
            {
                bool stuck = false; try { stuck = __instance.currentItem.IsTag("MODULE_STUCK_TAG"); } catch { }
                if (stuck) return false;
            }
        }
        catch { }
        return true;
    }

    private static void TryGuMachineTick(GameItem gu, int day)
    {
        try
        {
            var grid = GetGuGrid(gu);
            if (grid == null) return;
            int charge = RobinCrusoePerk.GetTagIntSafe(gu, GU_CHARGE_TAG);
            // 09-15 用户拍板：充能中（charge>0）舱内模组禁止移出——打 MODULE_STUCK_TAG（原生"卡住"语义：PlayerStore 可用性判定 + 卸载链 + tooltip 全拦）
            if (charge > 0) LockGuModules(grid);
            // 09-19 修复：charge=0 摘除舱内全部 STUCK_TAG——原实现只打不摘导致产出永久锁死（卡住根因）；顺带恢复存量卡住的档
            else UnlockGuModules(grid);
            if (charge < BuildConfig.GuChargeDays)
            {
                // 09-19 P1 充能天数差分：同日打烊不重复 +1（根治双挂点重复计/读档后卡住）；LAST_DAY 创建时已记，随 tag 读档保留
                int lastDay = RobinCrusoePerk.GetTagIntSafe(gu, GU_LAST_DAY_TAG);
                if (lastDay < day)
                {
                    RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, 1);
                    RobinCrusoePerk.SetTagIntValue(gu, GU_LAST_DAY_TAG, day);
                }
                return; // 未满不炼
            }
            // 收集模组（排除报废 ruined——不参与炼蛊）
            var mods = new System.Collections.Generic.List<GameItem>();
            if (grid.childItems != null) foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                string id = ""; try { id = m.identifier ?? ""; } catch { }
                if (id == "system_module_ruined") continue;
                // 09-19 防御性排除电池（power_source_item——若电池带 MODULE_TAG 会误入炼蛊/抽卡原料，历史反馈"炼蛊机练电池"）
                bool isBat = false; try { isBat = m.IsTag("power_source_item"); } catch { }
                if (isBat) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (!isMod) continue;
                mods.Add(m); // 09-19 修复：产出模组不再排除——练过的蛊可重复吞噬（原 FORGED 排除导致产出占舱又不算原料，凑不齐 2 个永不炼）
            }
            if (mods.Count < 2) return; // 满3但模组不足——保持满等放模组
            // 09-18 基底选三属性总值最高的模组（高特性优先——参考电池互吞规则；产出体 id = 该模组 id）
            GameItem baseMod = null;
            int bestScore = -1;
            foreach (var m in mods)
            {
                int s = 0;
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                s += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
                if (s > bestScore) { bestScore = s; baseMod = m; }
            }
            if (baseMod == null) baseMod = mods[0];
            string baseId = "system_module_overclock";
            try { string bid = baseMod.identifier ?? ""; if (bid.Length > 0) baseId = bid; } catch { }
            // 三属性之和 ×1.2（上限 150）
            int perf = 0, eff = 0, qual = 0;
            foreach (var m in mods)
            {
                perf += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                eff += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                qual += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
            }
            int newPerf = Math.Min(BuildConfig.GuForgeCap, (int)(perf * BuildConfig.GuForgeMult / 100f));
            int newEff = Math.Min(BuildConfig.GuForgeCap, (int)(eff * BuildConfig.GuForgeMult / 100f));
            int newQual = Math.Min(BuildConfig.GuForgeCap, (int)(qual * BuildConfig.GuForgeMult / 100f));
            // 生成强化模组（基底 id 为产出体）
            GameItem result = null;
            try { result = DirectoryMaster.Item(baseId); } catch { }
            if (result == null)
            {
                // 09-19 P2 失败语义：合成失败 = 原料销毁 + 产报废模组（不留悬垂原料）；充能归零重来
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                GameItem scrap = null;
                try { scrap = DirectoryMaster.Item("system_module_ruined"); } catch { }
                if (scrap != null)
                {
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, scrap, -1); } catch { }
                }
                string fline = LangHelper.T("养蛊机炼蛊失败：投入模组报废", "Swarm Forge forging failed: input modules scrapped");
                try { StoreUIManager.Instance.Notify(fline); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(fline, "#7FC97F"); } catch { } // 09-22 统一柔和绿
                Core.AddNightReportLine(fline);
                RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, -3);
                return;
            }
            // 09-19 修复：产出纯含炼蛊属性——DirectoryMaster.Item 新建的原生模组自带 base 属性（overclock 原生 perf=36/eff=-44，拆包 diff 实证），
            // 直接 AddTagInt 会叠加在原生 base 上（产出=36+newPerf），再投入炼蛊时 36/-44 被 ×1.2 反复放大——"属性叠加不丢"被污染
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
            try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
            if (newPerf > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", newPerf);
            if (newEff > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", newEff);
            if (newQual > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_QUALITY_INT", newQual);
            try { result.EnableTag("MODULE_TAG"); } catch { } // 09-19 保险：确保产出可被下一轮收集（InitContrabandItem 清 tag 未实锤，显式补打幂等）
            try { Il2Cpp.ContrabandHelper.InitContrabandItem(result, 3); } catch { } // 09-16 高级违禁品(level 3)
            try { result.shortDescription = (result.shortDescription ?? "") + LangHelper.T("（违禁原因：炼蛊融合产物，蕴含被禁的模组融合技术）", " (Contraband: forged fusion product with outlawed module-merging tech)"); } catch { }
            // 09-19 修复：先清空原料腾格子、再入产出——原"先入产出后清空"导致产出(2×2 需 4 格)被原料占格
            // → TryAcceptAllMid(-1) 找不到空间静默失败（catch 吞掉）→ 产出丢失（用户反馈"炼蛊成功的模组消失"）
            foreach (var m in mods)
            {
                try { m.parentInventory?.Expel(m); } catch { }
                try { m.Destroy(); } catch { }
            }
            // 09-15 用户拍板：生产成果放入机器舱（玩家打开机器取出；TryAcceptAllMid 接受 GameGridInventory）
            try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, result, -1); } catch { }
            // 充能归零
            RobinCrusoePerk.AddTagInt(gu, GU_CHARGE_TAG, -3);
            // 通知（弹窗 + 晨报 + 日历 Tab）
            string name = "养蛊模组";
            try { name = result.GetDisplayName(); } catch { try { name = result.name ?? baseId; } catch { } }
            string line = LangHelper.T(
                "养蛊机炼成 " + name + " · 性能+" + newPerf + " 效率+" + newEff + " 质量+" + newQual + "（×1.2）",
                "Swarm Forge forged " + name + " · Perf+" + newPerf + " Eff+" + newEff + " Qual+" + newQual + " (x1.2)");
            try { StoreUIManager.Instance.Notify(line); } catch { }
            try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-19 亮绿(#00FF00)改柔和绿——刺眼反馈
            Core.AddNightReportLine(line);
        }
        catch { }
    }


    // ============================================================
    // 不稳定 AI 生成器（09-15 段3b）：打烊自动抽卡（每天 1 次）
    // 装保护器 = 阉割版（消耗 1 个，不报废，上限 75%）
    // 不装 = 不稳定版（50% 成功上限 150% / 50% 失败=投入模组+生成器全报废）
    // 属性写机器 TOTAL_PERCENTAGE_{PERFORMANCE,POWER,QUALITY}_BONUS_INT（拆包：效率=POWER）
    // ============================================================
    public const string AI_LAST_DAY_TAG = "wage_ai_last_day";
    public const string AI_MODULE_ID = "wage_ai_module"; // 09-15 用户拍板：生成器产出"不稳定AI模组"实物（不是机器属性吸收）
    private static Il2CppSystem.Func<GameItem> _aiModuleFactory;

    private static System.Collections.Generic.List<GameItem> FindAiGenerators()
    {
        var result = new System.Collections.Generic.List<GameItem>();
        try
        {
            if (EmporiumEntry.Instance != null)
            {
                var all = EmporiumEntry.Instance.GetAllItems();
                if (all != null) foreach (var it in all) { if (it != null && it.identifier == AI_GENERATOR_ID) result.Add(it); }
            }
        }
        catch { }
        return result;
    }

    private static void TryAiGeneratorTick(GameItem gen, int day)
    {
        try
        {
            int lastDay = RobinCrusoePerk.GetTagIntSafe(gen, AI_LAST_DAY_TAG);
            if (lastDay == day) return; // 每天 1 次
            var grid = GetGuGrid(gen);
            if (grid == null) { return; }
            // 收集模组 + 找保护器
            var mods = new System.Collections.Generic.List<GameItem>();
            GameItem protector = null;
            if (grid.childItems != null) foreach (var m in grid.childItems)
            {
                if (m == null) continue;
                string id = ""; try { id = m.identifier ?? ""; } catch { }
                if (id == PROTECTOR_ID) { protector = m; continue; }
                if (id == "system_module_ruined") continue;
                bool isBat = false; try { isBat = m.IsTag("power_source_item"); } catch { } // 09-19 防御性排除电池
                if (isBat) continue;
                bool isMod = false; try { isMod = m.IsTag("MODULE_TAG") && !RobinCrusoePerk.IsExcludedModule(m); } catch { }
                if (isMod) mods.Add(m);
            }
            if (mods.Count < 2) return; // 模组不足不抽（不记日——补料后当天可抽；抽卡成功/失败后才记日防同日重复）
            bool safe = protector != null;
            if (safe)
            {
                // 阉割版：消耗 1 个保护器
                try { protector.parentInventory?.Expel(protector); } catch { }
                try { protector.Destroy(); } catch { }
            }
            // 抽卡：不稳定版 50% 成功 / 50% 失败；阉割版 100%
            bool success = true;
            if (!safe)
            {
                int roll = 0; try { roll = Core.Rng.Next(100); } catch { roll = 0; }
                success = roll < BuildConfig.AiSuccessPct;
            }
            if (success)
            {
                // 09-15 用户拍板：生成器产出"不稳定AI模组"实物（不是机器属性吸收）——属性 = 投入模组之和，上限 cap
                int cap = safe ? BuildConfig.AiStableCap : BuildConfig.AiUnstableCap;
                int perf = 0, eff = 0, qual = 0;
                foreach (var m in mods)
                {
                    perf += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_PERFORMANCE_INT");
                    eff += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_EFFICIENCY_INT");
                    qual += RobinCrusoePerk.GetTagIntSafe(m, "BONUS_PERCENTAGE_QUALITY_INT");
                }
                int newP = Math.Min(cap, perf);
                int newE = Math.Min(cap, eff);
                int newQ = Math.Min(cap, qual);
                long sumVal = 0; foreach (var mv in mods) { try { sumVal += mv.unitValue; } catch { } }
                GameItem result = null;
                try { result = DirectoryMaster.Item(AI_MODULE_ID); } catch { }
                if (result != null)
                {
                    // 09-19 吸取养蛊机教训：清原生 base（保险，防注册自带属性混入）+ 补 MODULE_TAG（防不可收集）
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(result, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    if (newP > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_PERFORMANCE_INT", newP);
                    if (newE > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_EFFICIENCY_INT", newE);
                    if (newQ > 0) RobinCrusoePerk.AddTagInt(result, "BONUS_PERCENTAGE_QUALITY_INT", newQ);
                    try { result.EnableTag("MODULE_TAG"); } catch { }
                    try { result.unitValue = (int)sumVal; result.unitBaseValue = (int)sumVal; } catch { } // 价值=吞噬原料之和
                }
                // 09-19 吸取养蛊机教训：先清空原料腾格子、再入产出——原"先入产出后清空"导致 2×2 产出被原料占格
                // → TryAcceptAllMid(-1) 静默失败（catch 吞掉）→ 产出丢失（"炼蛊成功的模组消失"同根因）
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                if (result != null)
                {
                    // 实物放入生成器舱
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, result, -1); } catch { }
                }
                string modeName = safe ? LangHelper.T("阉割版", "Stable") : LangHelper.T("不稳定版", "Unstable");
                string line = LangHelper.T(
                    "AI 生成器产出 不稳定AI模组 · 性能+" + newP + " 效率+" + newE + " 质量+" + newQ + "（" + modeName + "）",
                    "Neural Generator produced Unstable AI Module · Perf+" + newP + " Eff+" + newE + " Qual+" + newQ + " (" + modeName + ")");
                try { StoreUIManager.Instance.Notify(line); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-19 成功色统一柔和绿（原金色）
                Core.AddNightReportLine(line);
            }
            else
            {
                // 09-15 用户拍板：30% 失败 = 投入模组变报废模组（生成器保留）
                foreach (var m in mods)
                {
                    try { m.parentInventory?.Expel(m); } catch { }
                    try { m.Destroy(); } catch { }
                }
                GameItem scrap = null;
                try { scrap = DirectoryMaster.Item("system_module_ruined"); } catch { }
                if (scrap != null)
                {
                    // 09-19 P2 失败语义：报废模组三属性清零（不参与后续吞噬、可卖）
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_PERFORMANCE_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_EFFICIENCY_INT", 0); } catch { }
                    try { RobinCrusoePerk.SetTagIntValue(scrap, "BONUS_PERCENTAGE_QUALITY_INT", 0); } catch { }
                    try { Il2Cpp.GraphUtils.TryAcceptAllMid(grid, scrap, -1); } catch { }
                }
                string line = LangHelper.T(
                    "AI 生成器不稳定爆发：投入模组报废",
                    "Neural Generator unstable burst: input modules scrapped");
                try { StoreUIManager.Instance.Notify(line); } catch { }
                try { var ps = PlayerStore.Instance; if (ps != null) ps.AddNightLog(line, "#7FC97F"); } catch { } // 09-22 统一柔和绿
                Core.AddNightReportLine(line);
            }
            RobinCrusoePerk.AddTagInt(gen, AI_LAST_DAY_TAG, day);
        }
        catch { }
    }
}

