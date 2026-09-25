using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;
partial class ContainerUpgradeV2

{
    // ===== Tooltip =====

    // ===================== 蛙哥箱子 tooltip（独立挂载，非鲁滨逊场景也显示） =====================
    public static void PostfixWageBoxTooltip(RichTextBuilder builder, GameItem item)
    {
        try
        {
            if (builder == null || item == null) return;
            if (!IsWageBox(item)) return;
            int stage = GetTagIntSafe(item, "wb_stage");
            if (stage >= MAX_STAGE)
                builder.AddLine(LangHelper.T("◆ 妙妙箱：满级（" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + "）· 奖励：第二个妙妙箱已入背包", "◆ Wage Box: MAX (" + WAGE_BOX_W[MAX_STAGE] + "×" + WAGE_BOX_H[MAX_STAGE] + ") · Reward: second box in backpack"),
                    true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
            else
                {
                    int progress = GetTagIntSafe(item, "wb_progress");
                    int need = UPGRADE_COSTS[Math.Min(stage, MAX_STAGE - 1)];
                    builder.AddLine(LangHelper.T("◆ 妙妙箱：段位 " + stage + "/5 · 升级进度 " + progress + "/" + need + "（拖螺丝到箱上直接升级，或放箱内过夜自动消耗）", "◆ Wage Box: Stage " + stage + "/5 · progress " + progress + "/" + need + " (drag nuts to box to upgrade, or leave inside overnight)"),
                        true, (RenderHandler.ColorPalette)(-1), false, false, false, false, (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1), (RenderHandler.ColorPalette)(-1));
                }
        }
        catch (System.Exception ex) { Core.LogMsg("[ContainerUpgradeV2] 异常: " + ex.Message); }
    }
}
