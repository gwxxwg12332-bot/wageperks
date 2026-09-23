using System;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class GuMachineSystem
{
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
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Entity] 异常: " + ex.Message); }
        if (sp != null)
        {
            try { it.SetSpriteAndShape(ICON_ATLAS, iconKey); return; } catch { }
        }
        BorrowNativeSprite(it, nativeId);
    }
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
        catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Entity] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Entity] 异常: " + ex.Message); }
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
            catch (System.Exception ex) { Core.LogMsg("[GuMachineSystem.Entity] 异常: " + ex.Message); }
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
}
