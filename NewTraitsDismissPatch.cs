using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 补丁：打烊后处理好酒之徒宿醉
// ============================================================
// [HarmonyPatch(typeof(PlayerStore), "DismissCurrentClient")]
internal static class NewTraitsDismissPatch
{
    static void Postfix()
    {
        // 好酒之徒：最后一个客户走后roll宿醉（简化处理）
        // 实际应该在打烊时触发，这里简化
    }
}
