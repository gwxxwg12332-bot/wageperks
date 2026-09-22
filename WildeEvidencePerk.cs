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
        "线人王尔德手段高明。每次线人行动额外消除证据：证据条-5，每种犯罪记录额外-50。",
        "Fixer Wilde covers tracks. Each fixer action removes extra evidence: evidence bar -5, each crime record -50.");
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
// 1. 直接减 evidenceLevel（证据条）-5
// 2. 增强 crimeAmount 消除 -50
// 3. 同步减 crimeAmountTotal（保持数据一致）
internal static class WildeFixerPatch
{
    public const int EvidenceReduce = 5;    // 09-22 用户拍板：证据条 -5
    public const int ExtraCrimeReduce = 50; // 每种犯罪额外 -50

    public static void Postfix(SecData __instance)
    {
        try
        {
            if (!WildeEvidencePerk.IsActive()) return;
            // 09-22 狄仁杰之手：免疫王尔德销毁（Prefix 已拦原版，这里双保险）
            if (DetectivePerk.IsActive()) return;
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

// Patch：SecData.OnFixerUsed Prefix —— 狄仁杰之手"完全不销毁"
// 未选狄仁杰 → return true 放行（原版销毁链 + 王尔德追加照常）
// 狄仁杰激活 → return false 跳过整个原版方法（线人行动不再销毁任何证据）
internal static class DetectiveFixerPatch
{
    public static bool Prefix()
    {
        // 诊断：无论拦不拦都打日志，确认 OnFixerUsed 触发 + 狄仁杰真实激活状态
        bool detective = false;
        try { detective = DetectivePerk.IsActive(); }
        catch (Exception ex) { Core.LogMsg("[狄仁杰之手] IsActive 异常: " + ex.Message); }
        Core.LogMsg($"[狄仁杰之手] OnFixerUsed 触发：狄仁杰激活={detective}");
        if (!detective) return true; // 放行（王尔德/原版销毁照常）
        Core.LogMsg("[狄仁杰之手] 线人销毁证据已拦截（原版 OnFixerUsed 跳过）");
        return false;
    }
}

// Patch：NetworkUpgrade.Unlock Prefix + Postfix —— 狄仁杰之手"能买但不清证据"（兜底恢复方案）
// 购买链（拆包 [L1]）：WildUIManager.OnUnlockClicked → NetworkUpgrade.Unlock
//   → 内部按 id 分发：id == "EVIDENCE_REMOVAL" → 清除证据（实测不走 SecData.ResetCrime，05:39 日志实锤）
// 方案：Prefix 记录购买前 SecData 快照（evidenceLevel + 三列表）→ 放行购买
//       Postfix 执行完后把快照写回（不管内部走哪条清除路径，都能恢复证据）
internal static class DetectiveUpgradePatch
{
    internal static bool _blockNextResetCrime; // 标记：狄仁杰+EVIDENCE_REMOVAL 购买流程中
    // 购买前快照（托管列表拷贝，避开 IL2CPP 三列表同步坑）
    private static int _evidenceBefore;
    private static System.Collections.Generic.List<string> _typesBefore;
    private static System.Collections.Generic.List<int> _amountsBefore;
    private static System.Collections.Generic.List<int> _totalsBefore;

    public static bool Prefix(NetworkUpgrade __instance)
    {
        try
        {
            bool detective = false;
            try { detective = DetectivePerk.IsActive(); } catch (Exception ex) { Core.LogMsg("[狄仁杰之手] IsActive异常: " + ex.Message); }
            string id = "?";
            try { id = __instance.id; } catch (Exception ex) { Core.LogMsg("[狄仁杰之手] 读id异常: " + ex.Message); }
            // 诊断：所有 Unlock 购买都打日志，打印真实 id + 狄仁杰状态
            Core.LogMsg($"[狄仁杰之手] Unlock触发 id={id} 狄仁杰={detective}");
            // 真实 id = "EVIDENCE_REMOVAL"（05:37 实测日志实锤，拆包给的小写不匹配）
            if (detective && string.Equals(id, "EVIDENCE_REMOVAL", StringComparison.OrdinalIgnoreCase))
            {
                _blockNextResetCrime = true; // 放行购买，标记清除拦截
                // 记录购买前 SecData 快照
                try
                {
                    var sd = PlayerStore.Instance?.secData;
                    if (sd != null)
                    {
                        _evidenceBefore = sd.evidenceLevel;
                        _typesBefore = new System.Collections.Generic.List<string>();
                        _amountsBefore = new System.Collections.Generic.List<int>();
                        _totalsBefore = new System.Collections.Generic.List<int>();
                        if (sd.crimeTypes != null) { int n = sd.crimeTypes.Count; for (int i = 0; i < n; i++) _typesBefore.Add(sd.crimeTypes[i]); }
                        if (sd.crimeAmount != null) { int n = sd.crimeAmount.Count; for (int i = 0; i < n; i++) _amountsBefore.Add(sd.crimeAmount[i]); }
                        if (sd.crimeAmountTotal != null) { int n = sd.crimeAmountTotal.Count; for (int i = 0; i < n; i++) _totalsBefore.Add(sd.crimeAmountTotal[i]); }
                        Core.LogMsg($"[狄仁杰之手] 快照: evidence={_evidenceBefore} types={_typesBefore.Count} amts={_amountsBefore.Count} totals={_totalsBefore.Count}");
                    }
                    else { Core.LogMsg("[狄仁杰之手] 快照失败: secData 为空"); }
                }
                catch (Exception ex) { Core.LogMsg("[狄仁杰之手] 快照异常: " + ex.Message); }
            }
        }
        catch (Exception ex) { Core.LogMsg("[狄仁杰之手] Unlock Prefix异常: " + ex.Message); }
        return true; // 放行购买（扣钱/解锁照常）
    }

    public static void Postfix(NetworkUpgrade __instance)
    {
        if (!_blockNextResetCrime) return;
        _blockNextResetCrime = false; // 消费标记
        try
        {
            var sd = PlayerStore.Instance?.secData;
            if (sd == null) { Core.LogMsg("[狄仁杰之手] Postfix恢复失败: secData 为空"); return; }
            // 恢复三列表（同步长度，防 GetSuspectedCrimeDisplay 越界）
            if (_typesBefore != null && sd.crimeTypes != null) { sd.crimeTypes.Clear(); foreach (var s in _typesBefore) sd.crimeTypes.Add(s); }
            if (_amountsBefore != null && sd.crimeAmount != null) { sd.crimeAmount.Clear(); foreach (var v in _amountsBefore) sd.crimeAmount.Add(v); }
            if (_totalsBefore != null && sd.crimeAmountTotal != null) { sd.crimeAmountTotal.Clear(); foreach (var v in _totalsBefore) sd.crimeAmountTotal.Add(v); }
            // 恢复证据等级
            int before = _evidenceBefore;
            sd.evidenceLevel = Math.Max(0, Math.Min(100, before));
            Core.LogMsg($"[狄仁杰之手] Unlock 后证据已恢复: evidenceLevel={sd.evidenceLevel} types={sd.crimeTypes?.Count} amts={sd.crimeAmount?.Count}");
        }
        catch (Exception ex) { Core.LogMsg("[狄仁杰之手] Postfix恢复异常: " + ex.Message); }
    }
}

// Patch：SecData.CommitCrime Prefix —— 狄仁杰之手"罪证收集速度 +50%"
// 所有做坏事涨罪证的统一入口（卖违禁品/假酒/行贿/走私...）
internal static class DetectiveCommitCrimePatch
{
    public static void Prefix(SecData __instance, string crimeID, ref int amount)
    {
        try
        {
            if (DetectivePerk.IsActive())
            {
                amount = (int)(amount * 1.5f);  // +50% 罪证收集速度
                Core.LogMsg($"[狄仁杰之手] CommitCrime: {crimeID} amount={amount} (+50%)");
            }
        }
        catch (Exception ex) { Core.LogMsg("[狄仁杰之手] CommitCrime Prefix异常: " + ex.Message); }
    }
}

// Patch：NegociationUIManager.SellItem Postfix —— 统计卖出的违禁品/赃物收入（全玩家通用）
// 拆包 [L1]：卖武器/违禁品真实路径 = NegociationUIManager.SellItem()（L47515，无参）
//   当前交易物品 = __instance.currentNegociatedItem（0x58），成交价 = GetCurrentValue()
// 违禁判断：ContrabandHelper.GetContrabandLevel > 0（AddictOfficerEvent 先例）；赃物：StolenHelper.IsStolenItem（DrJacksonFriendPerk 先例）
// 已知缺口：柜台自动卖出（PlayerStore.SellItemFromTable L32258）签名未实锤，待拆包确认后补挂
internal static class SoldContrabandCounterPatch
{
    internal static int _soldToday; // 当日卖违禁/赃物累计收入（打烊结算后清零）

    public static void Postfix(NegociationUIManager __instance)
    {
        try
        {
            if (!DetectivePerk.IsActive()) return; // 狄仁杰之手专属
            GameItem item = null;
            try { item = __instance.currentNegociatedItem; } catch (Exception ex) { Core.LogMsg("[销赃证据] 读currentNegociatedItem异常: " + ex.Message); return; }
            if (item == null) return;
            bool isContraband = false;
            try { isContraband = ContrabandHelper.GetContrabandLevel(item) > 0; } catch (Exception ex) { Core.LogMsg("[销赃证据] GetContrabandLevel异常: " + ex.Message); }
            bool isStolen = false;
            try { isStolen = StolenHelper.IsStolenItem(item); } catch (Exception ex) { Core.LogMsg("[销赃证据] IsStolenItem异常: " + ex.Message); }
            Core.LogMsg($"[销赃证据] 卖出触发: {item.name} contraband={isContraband} stolen={isStolen}");
            if (!isContraband && !isStolen) return;

            long v = 0;
            try { v = item.GetCurrentValue(); } catch { try { v = item.GetValue(); } catch { } }
            _soldToday += (int)v;
            Core.LogMsg($"[销赃证据] 卖出违禁/赃物: {item.name} 值={v} 当日累计={_soldToday}");
        }
        catch (Exception ex) { Core.LogMsg("[销赃证据] SellItem Postfix异常: " + ex.Message); }
    }
}

// Patch：SecData.OnNewDay Postfix —— 每 5000 销赃收入 → 证据 +15（叠加式，全玩家通用）
// 用户拍板 B+A+B：卖赃物/违禁品累计收入、打烊结算、与现有分档叠加
internal static class SoldEvidenceOnNewDayPatch
{
    public static void Postfix(SecData __instance)
    {
        try
        {
            if (!DetectivePerk.IsActive()) return; // 狄仁杰之手专属
            if (__instance == null) return;
            int today = SoldContrabandCounterPatch._soldToday;
            int gained = today / 5000 * 15; // 每满 5000 → +15（整数除法，不满 5000 不加）
            if (gained > 0)
            {
                int before = __instance.evidenceLevel;
                __instance.evidenceLevel = Math.Min(100, before + gained); // 叠加 + 钳制 100
                Core.LogMsg($"[销赃证据] 打烊: 当日销赃={today} → 证据 +{gained}（{before} → {__instance.evidenceLevel}）");
            }
            SoldContrabandCounterPatch._soldToday = 0; // 当日清零
        }
        catch (Exception ex) { Core.LogMsg("[销赃证据] OnNewDay Postfix异常: " + ex.Message); }
    }
}
