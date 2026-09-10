using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 王尔德消除更多证据：线人(fixer)行动时额外消除证据
// 最优雅方案：SecData.OnFixerUsed Postfix，原版执行完后追加效果，不碰原版逻辑
internal sealed class WildeEvidencePerk : CustomStartingPerk
{
    internal const string PerkId = "王尔德之手";
    internal override string Id => PerkId;
    internal override string DisplayName => LangHelper.T("王尔德之手", "Wilde's Touch");
    internal override string Description => LangHelper.T(
        "线人王尔德手段高明。每次线人行动额外消除证据：证据条-50，每种犯罪记录额外-500。让治安部永远查不到你头上。",
        "Fixer Wilde knows how to cover tracks. Each fixer action removes extra evidence: evidence bar -50, each crime record -500. Security never catches up.");
    internal override int Cost => 2;

    internal static bool IsActive()
    {
        return Core.PerkActive(PerkId);
    }

    // 抽象方法实现（开新档时调用，此特性不需要开局给物品，空实现）
    internal override void OnNewGame() { }
}

// Patch：SecData.OnFixerUsed Postfix
// 原版线人只减半crimeAmount+减50，证据条纹丝不动。这里追加：
// 1. 直接减 evidenceLevel（证据条）-20
// 2. 增强 crimeAmount 消除 -30
// 3. 同步减 crimeAmountTotal（保持数据一致）
internal static class WildeFixerPatch
{
    public const int EvidenceReduce = 50;   // 每次线人行动证据条 -50
    public const int ExtraCrimeReduce = 500; // 每种犯罪额外 -500

    public static void Postfix(SecData __instance)
    {
        try
        {
            if (!WildeEvidencePerk.IsActive()) return;
            if (__instance == null) return;

            // 1. 直接减 evidenceLevel（原版完全不做的，效果最显著）
            __instance.evidenceLevel = Mathf.Max(0, __instance.evidenceLevel - EvidenceReduce);

            // 2. 增强 crimeAmount 消除（原版已减半+减50，这里追加）
            if (__instance.crimeAmount != null)
            {
                for (int i = 0; i < __instance.crimeAmount.Count; i++)
                {
                    __instance.crimeAmount[i] = Mathf.Max(0, __instance.crimeAmount[i] - ExtraCrimeReduce);
                }
            }

            // 3. 同步减累计统计（不影响调查速度但保持数据一致）
            if (__instance.crimeAmountTotal != null)
            {
                for (int i = 0; i < __instance.crimeAmountTotal.Count; i++)
                {
                    __instance.crimeAmountTotal[i] = Mathf.Max(0, __instance.crimeAmountTotal[i] - ExtraCrimeReduce);
                }
            }

        }
        catch (Exception ex)
        {
            Core.LogMsg("[王尔德之手] OnFixerUsed Postfix异常: " + ex.Message);
        }
    }
}
