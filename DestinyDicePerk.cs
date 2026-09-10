using System;
using Il2Cpp;
using MelonLoader;

namespace JacksonPerks;

// 命运之骰特性：开局获得命运骰子（拖物品吸收价值，每400价值触发1随机事件）
internal sealed class DestinyDicePerk : CustomStartingPerk
{
    internal const string PerkId = "命运之骰";

    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("命运之骰", "Dice of Fate");
    internal override string Description => LangHelper.T("开局获得一枚命运骰子：拖物品到骰子上可吸收其价值，每累计400价值触发1个随机事件。", "Start with a Dice of Fate: drag items onto it to absorb their value. Every 400 value triggers a random event.");
    internal override int Cost => 3;
    internal override int Type => 0;

    // 防重标记：HandleInitialItem 过天可能误触发，OnNewGame 重置
    internal static bool _diceGiven = false;

    internal override void OnNewGame()
    {
        _diceGiven = false;
    }

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    internal static void GiveIfActive()
    {
        try
        {
            if (!IsActive() || _diceGiven) return;
            _diceGiven = true;
            DestinyDice.GiveDestinyDiceToPlayer();
        }
        catch (Exception ex) { Core.LogMsg("[命运之骰] 特性发放异常: " + ex.Message); }
    }
}
