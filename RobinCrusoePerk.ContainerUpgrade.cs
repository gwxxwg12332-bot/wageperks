using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 【空间站鲁滨逊】职业生存系统（startType=14）
// v4.2（2026-09-09，v4.2 终稿重构）：
//   - 饱食节点制：calorieBalance（卡，1单位=2200卡）替代 hunger 层数制，与原生 hunger(0-1000) 完全解耦
//   - 精神 5 档（昂扬/常态/低迷/低落/崩溃）：昂扬累计制（每2天+1%售价/+5%预算，封顶+5%/+25%，断档归零）
//   - 双击食物=摄入 cal（变质50%/腐烂20%）+ 已食用档 + 患病判定（变质10%/腐烂40%）
//   - 双击水=清零 thirstLevel（渴系统独立保留）
//   - 节点：濒饿(≤0且≥3天)/饥饿(≤0)/常态(1-5单位)/饱腹(>5单位)
//   - 粮仓充盈：余额≥7单位(15400卡) → 全店售价+5%
//   - 救场：连续≤0达5天 → 好心客户送食1-2份，不删档，归零
//   - 客流削减：低迷-1/低落-2/崩溃-4；禁外出：低落/崩溃
//   - 状态客户联动（Patches/Core 侧）：加价/概率权重/预算/出价
//
// 拆包锚点全部 [L1]（cheatsheet 2.3.9 / 2.3.10 / 2.3.12 / 2.5.16 / 4.6.8 / 4.6.9 / 设计AI v4.2）
// ============================================================
internal static partial class RobinCrusoePerk
{

    // ===== 物品判定 =====
    // 食物判定：硬编码清单 + 原生卡路里兜底（覆盖水培莓果/营养果/异种肉等遗漏）
    // 兜底规则：有 CALORIE_VALUE_TAG/CALORIE 标签 且 非饮品/药品/酒/种子 = 食物
    // 09-20 P2-4：熔炉模组白名单（furnace_module_ 开头不吞噬/炼蛊）


    // ===== 供货商卖水药食物（拆包 09-10：PlaceSupplierInventory 是 supplier 上货入口；仿水商 MerchantHelper.AddItemToCounter 追加，同日不重复）=====
    private static int _supplierGoodsDay = -1;
    public static void PostfixPlaceSupplierInventory()
    {
        try
        {
            if (!IsActive()) return;
            int day = DeterministicSchedule.CurrentDay;
            if (day == _supplierGoodsDay) return; // 同日不重复追加
            _supplierGoodsDay = day;
            string[] sellItems = {
                "bottled_water",        // 瓶装水
                "small_bottled_water",  // 小瓶水
                "water_ration",         // 水配给
                "raw_meat",             // 生肉
                "processed_meat",       // 加工肉
                "cup_noodle",           // 杯面
                "bandage_item",         // 绷带
                "nutrient_tablet",      // 营养片
            };
            int added = 0;
            // 大瓶高品质水（拆包 09-10：WaterPremadeHelper.AccurateHighQualityWater(size) 一行生成指定品质大瓶）
            try
            {
                GameItem hq = Il2Cpp.WaterPremadeHelper.AccurateHighQualityWater("large_bottled_water");
                if (hq != null) { MerchantHelper.AddItemToCounter(hq, 100, false); added++; }
            }
            catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加大瓶高品质水失败: " + ex.Message); }
            foreach (string wid in sellItems)
            {
                try
                {
                    GameItem w = DirectoryMaster.Item(wid, true);
                    if (w == null) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 不存在"); continue; }
                    MerchantHelper.AddItemToCounter(w, 100, false);
                    added++;
                }
                catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] 供货商加 " + wid + " 失败: " + ex.Message); }
            }
            Core.LogMsg("[空间站鲁滨逊] 供货商已追加水/药/食物 " + added + " 件（day " + day + "）");
        }
        catch (Exception ex) { Core.LogMsg("[空间站鲁滨逊] PostfixPlaceSupplierInventory 异常: " + ex.Message); }
    }










}
