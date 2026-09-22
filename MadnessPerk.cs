using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 精神错乱：进游戏后自动随机抽满所有特性
internal sealed class MadnessPerk : CustomStartingPerk
{
    internal const string PerkId = "精神错乱";
    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("精神错乱", "Madness");
    internal override string Description => LangHelper.T(
        "放弃思考，让命运决定你的天赋。自带+3特性槽。进游戏后自动随机抽取所有特性。点数不够时必抽负面。优先抽取加点数和格子上限的特性。\n\n源代码来源致谢：by 游予.⁧~喵⁦⁦ QQ 1544069837",
        "Give up thinking, let fate decide your perks. Auto-randomize all perks on game start. If not enough points, negative perks are guaranteed. Perks that add points/slots are prioritized.");
    internal override int Cost => 0;
    internal override int Type => 1;  // 负面红色
    internal override int MaxSlot => 3;  // 09-22 自带+3特性槽

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    internal override void OnNewGame()
    {
        try
        {
            if (!IsActive()) return;
            // 09-22 精神错乱：进游戏自动随机抽特性
            // 具体逻辑待拆包确认特性选择机制
            Core.LogMsg("[精神错乱] OnNewGame 触发");
        }
        catch { }
    }
}