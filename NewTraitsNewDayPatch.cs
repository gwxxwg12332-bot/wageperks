using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 补丁：新的一天处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(GameMaster), "OnNewDay")]
internal static class NewTraitsNewDayPatch
{
    static void Postfix()
    {
        // 好酒之徒：新的一天清除宿醉
        WineLoverPerk.ClearHangover();
    }
}
