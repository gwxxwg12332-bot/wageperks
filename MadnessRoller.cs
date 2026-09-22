using System;
using SysDict = System.Collections.Generic;
using Il2Cpp;
using Il2CppDict = Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 精神错乱：点击后自动随机抽满特性
internal static class MadnessRoller
{
    private static readonly System.Random Rng = new System.Random();
    private const int MaxRounds = 128;
    private const int MinStartReputation = -100;

    // 声望效果表（从 RandomPerk 移植）
    private static readonly SysDict.Dictionary<string, SysDict.Dictionary<string, int>> RepEffects = new SysDict.Dictionary<string, SysDict.Dictionary<string, int>>
    {
        { "convict", new SysDict.Dictionary<string, int> { { "SECURITY", -48 } } },
        { "model_citizen", new SysDict.Dictionary<string, int> { { "SECURITY", 48 } } },
        { "known_snitch", new SysDict.Dictionary<string, int> { { "REVOLUTION", -16 }, { "BLACK_MARKET", -16 } } },
        { "well_connected", new SysDict.Dictionary<string, int> { { "ALL", 10 } } },
        // 09-23 修：声名狼藉绕过声望兜底 → 加进 RepEffects
        { "声名狼藉", new SysDict.Dictionary<string, int> { { "SECURITY", -198 }, { "UPPER_LEVEL", -198 }, { "REVOLUTION", -198 }, { "LOWER_LEVEL", -198 }, { "BLACK_MARKET", -99 } } }
    };

    // 排除列表（不和这些特性抽）
    private static readonly SysDict.HashSet<string> Excluded = new SysDict.HashSet<string>
    {
        "xiaowo_trade_owner_deal",
        "xiaowo_trade_precision_bay_expansion",
        "精神错乱"
    };

    internal static bool IsLocked { get; private set; }

    internal static void ResetLock() { IsLocked = false; }

    internal static void Roll(PerkUIController ui)
    {
        if (ui == null)
        {
            try { ui = PerkUIController.Instance; } catch { }
        }
        if (ui == null) return;
        try
        {
            Il2CppDict.List<StartingPerk> perks = StartingPerkList.Perks;
            if (perks == null) return;
            ui.maxPerkCount += BuildConfig.MadnessExtraSlots;  // 09-22 直接加+3特性槽

            Core.LogMsg("[精神错乱] === 开始随机抽特性 ===");
            int currentPerkCount = ui.currentPerkCount;
            int maxPerkCount = ui.maxPerkCount;
            int currentPerkPoint = ui.currentPerkPoint;
            int maxPerkPoint = ui.maxPerkPoint;
            Core.LogMsg($"[精神错乱] 当前: {currentPerkCount}/{maxPerkCount} 点数={currentPerkPoint}/{maxPerkPoint}");

            Il2CppDict.List<StartingPerk> list = SnapshotSelected(ui);
            SysDict.HashSet<string> hashSet = new SysDict.HashSet<string>();
            foreach (var item in list)
            {
                if (!string.IsNullOrEmpty(item.id)) hashSet.Add(item.id);
            }

            // 先选精神错乱本身
            var madnessPerk = FindInPool(perks, "精神错乱");
            if (madnessPerk != null && TrySelect(ui, madnessPerk))
            {
                list.Add(madnessPerk);
                hashSet.Add("精神错乱");
                Core.LogMsg("[精神错乱] 已选精神错乱本身");
            }

            int count = 0;

            // 阶段1：必选栏位天赋（加格子最多的）
            StartingPerk slotPerk = PickSlotBonusPerk(perks, list, hashSet);
            if (slotPerk != null && TrySelect(ui, slotPerk))
            {
                list.Add(slotPerk);
                hashSet.Add(slotPerk.id);
                count++;
                Core.LogMsg($"[精神错乱] 阶段1 栏位天赋: {slotPerk.id}");
            }

            // 阶段2：随机抽满
            SysDict.Dictionary<string, SysDict.HashSet<string>> incompat = BuildIncompatMap(perks);
            int round = 0;
            while (ui.currentPerkCount < ui.maxPerkCount && round < MaxRounds)
            {
                round++;
                StartingPerk next = PickNext(ui, perks, list, hashSet, incompat);
                if (next == null) break;
                if (!TrySelect(ui, next)) break;
                list.Add(next);
                hashSet.Add(next.id);
                count++;
            }

            // 阶段3：点数不够必抽负面
            int refundRound = 0;
            while (ui.currentPerkPoint > ui.maxPerkPoint && ui.currentPerkCount < ui.maxPerkCount && refundRound < MaxRounds)
            {
                refundRound++;
                StartingPerk refund = PickRefundPerk(perks, hashSet, incompat);
                if (refund == null || !TrySelect(ui, refund)) break;
                list.Add(refund);
                hashSet.Add(refund.id);
                count++;
                Core.LogMsg($"[精神错乱] 阶段3 负面补偿: {refund.id}");
            }

            try { ui.OnChange(); } catch { }
            try { ui.SortPerkContainer(ui.availablePerks); } catch { }

            Core.LogMsg($"[精神错乱] === 抽完 共{count}条 已选{ui.currentPerkCount}/{ui.maxPerkCount} 点数{ui.currentPerkPoint}/{ui.maxPerkPoint} ===");

            if (count > 0)
            {
                LockAndHide(ui);
            }
        }
        catch (Exception e)
        {
            Core.LogMsg("[精神错乱] 抽取出错: " + e.Message);
        }
    }

    private static void LockAndHide(PerkUIController ui)
    {
        try
        {
            // 隐藏已选池里除了精神错乱的其他格子
            var selectedPerks = ui.selectedPerks;
            if (selectedPerks != null)
            {
                var transform = selectedPerks.transform;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    var el = child.gameObject.GetComponent<StartingPerkElement>();
                    if (el != null && el.id != "精神错乱")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }
            IsLocked = true;
            Core.LogMsg("[精神错乱] 已锁定特性选择");
        }
        catch { IsLocked = true; }
    }

    private static Il2CppDict.List<StartingPerk> SnapshotSelected(PerkUIController ui)
    {
        Il2CppDict.List<StartingPerk> list = new Il2CppDict.List<StartingPerk>();
        try
        {
            var selectedPerks = ui.selectedPerks;
            if (selectedPerks == null) return list;
            var transform = selectedPerks.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var el = child.gameObject.GetComponent<StartingPerkElement>();
                if (el != null && !string.IsNullOrEmpty(el.id))
                {
                    var perk = el.perk;
                    if (perk != null) list.Add(perk);
                }
            }
        }
        catch { }
        return list;
    }

    private static StartingPerk PickSlotBonusPerk(Il2CppDict.List<StartingPerk> pool, Il2CppDict.List<StartingPerk> chosen, SysDict.HashSet<string> chosenIds)
    {
        StartingPerk best = null;
        int bestSlots = 0;
        int bestCost = int.MaxValue;
        int chosenMaxSlots = 0;
        var currentRep = BuildRepState(chosenIds);

        try
        {
            foreach (var item in chosen)
            {
                if (item.maxSlot > chosenMaxSlots) chosenMaxSlots = item.maxSlot;
            }

            foreach (var perk in pool)
            {
                if (perk == null) continue;
                string id = perk.id;
                if (string.IsNullOrEmpty(id) || Excluded.Contains(id) || chosenIds.Contains(id)) continue;
                if (perk.maxSlot <= 0 || IsIncompatible(perk, chosen)) continue;
                if (WouldBreakReputationFloor(id, currentRep)) continue;
                if (perk.maxSlot > bestSlots || (perk.maxSlot == bestSlots && perk.cost < bestCost))
                {
                    bestSlots = perk.maxSlot;
                    bestCost = perk.cost;
                    best = perk;
                }
            }
        }
        catch { }

        if (best == null) return null;
        if (chosenMaxSlots >= bestSlots) return null;
        return best;
    }

    private static StartingPerk PickRefundPerk(Il2CppDict.List<StartingPerk> pool, SysDict.HashSet<string> chosenIds, SysDict.Dictionary<string, SysDict.HashSet<string>> incompat)
    {
        StartingPerk best = null;
        int bestCost = 0;
        var currentRep = BuildRepState(chosenIds);

        try
        {
            foreach (var perk in pool)
            {
                if (perk == null) continue;
                string id = perk.id;
                if (string.IsNullOrEmpty(id) || Excluded.Contains(id) || chosenIds.Contains(id)) continue;
                if (perk.cost < 0 && !IsBlockedBy(id, chosenIds, incompat) && !WouldBreakReputationFloor(id, currentRep))
                {
                    if (best == null || perk.cost < bestCost)
                    {
                        best = perk;
                        bestCost = perk.cost;
                    }
                }
            }
        }
        catch { }
        return best;
    }

    private static StartingPerk PickNext(PerkUIController ui, Il2CppDict.List<StartingPerk> pool, Il2CppDict.List<StartingPerk> chosen, SysDict.HashSet<string> chosenIds, SysDict.Dictionary<string, SysDict.HashSet<string>> incompat)
    {
        int pointsLeft = ui.maxPerkPoint - ui.currentPerkPoint;
        Il2CppDict.List<StartingPerk> candidates = new Il2CppDict.List<StartingPerk>();
        var currentRep = BuildRepState(chosenIds);

        try
        {
            foreach (var perk in pool)
            {
                if (perk == null) continue;
                string id = perk.id;
                if (string.IsNullOrEmpty(id) || Excluded.Contains(id) || chosenIds.Contains(id)) continue;
                if (IsBlockedBy(id, chosenIds, incompat)) continue;
                if (WouldBreakReputationFloor(id, currentRep)) continue;
                if (perk.cost <= pointsLeft) candidates.Add(perk);
            }
        }
        catch { }

        if (candidates.Count > 0) return candidates[Rng.Next(candidates.Count)];
        return null;
    }

    private static SysDict.Dictionary<string, SysDict.HashSet<string>> BuildIncompatMap(Il2CppDict.List<StartingPerk> pool)
    {
        var dict = new SysDict.Dictionary<string, SysDict.HashSet<string>>();
        try
        {
            foreach (var perk in pool)
            {
                if (perk == null) continue;
                string id = perk.id;
                if (string.IsNullOrEmpty(id)) continue;
                var set = new SysDict.HashSet<string>();
                if (perk.incompatiblePerks != null)
                {
                    foreach (var inc in perk.incompatiblePerks)
                    {
                        if (!string.IsNullOrEmpty(inc)) set.Add(inc);
                    }
                }
                dict[id] = set;
            }
        }
        catch { }
        return dict;
    }

    private static bool IsBlockedBy(string candidateId, SysDict.HashSet<string> chosenIds, SysDict.Dictionary<string, SysDict.HashSet<string>> incompat)
    {
        try
        {
            if (incompat.TryGetValue(candidateId, out var set))
            {
                foreach (var inc in set) if (chosenIds.Contains(inc)) return true;
            }
            foreach (var chosenId in chosenIds)
            {
                if (incompat.TryGetValue(chosenId, out var set2) && set2.Contains(candidateId)) return true;
            }
        }
        catch { }
        return false;
    }

    private static bool IsIncompatible(StartingPerk candidate, Il2CppDict.List<StartingPerk> chosen)
    {
        try
        {
            string id = candidate.id;
            if (string.IsNullOrEmpty(id)) return false;
            var chosenSet = new SysDict.HashSet<string>();
            foreach (var item in chosen)
            {
                if (!string.IsNullOrEmpty(item.id)) chosenSet.Add(item.id);
            }
            if (candidate.incompatiblePerks != null)
            {
                foreach (var inc in candidate.incompatiblePerks)
                {
                    if (!string.IsNullOrEmpty(inc) && chosenSet.Contains(inc)) return true;
                }
            }
            foreach (var item in chosen)
            {
                if (item.incompatiblePerks != null)
                {
                    foreach (var inc in item.incompatiblePerks)
                    {
                        if (!string.IsNullOrEmpty(inc) && inc == id) return true;
                    }
                }
            }
        }
        catch { }
        return false;
    }

    private static StartingPerk FindInPool(Il2CppDict.List<StartingPerk> pool, string id)
    {
        try
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var perk = pool[i];
                if (perk != null && perk.id == id) return perk;
            }
        }
        catch { }
        return null;
    }

    private static bool TrySelect(PerkUIController ui, StartingPerk perk)
    {
        if (perk == null) return false;
        try
        {
            string id = perk.id;
            if (string.IsNullOrEmpty(id)) return false;
            StartingPerkElement el = FindElement(ui.selectedPerks, id) ?? FindElement(ui.availablePerks, id);
            if (el == null)
            {
                el = CreateElement(ui, perk);
                if (el == null) return false;
            }
            try { el.perk = perk; } catch { }
            ui.SelectPerk(el);
            try { el.isSelected = true; } catch { }
            return true;
        }
        catch { return false; }
    }

    private static StartingPerkElement CreateElement(PerkUIController ui, StartingPerk perk)
    {
        try
        {
            var prefab = ui.perkElementPrefab;
            var available = ui.availablePerks;
            if (prefab == null || available == null) return null;
            var obj = UnityEngine.Object.Instantiate(prefab, available.transform);
            var el = obj.GetComponent<StartingPerkElement>();
            if (el == null) { UnityEngine.Object.Destroy(obj); return null; }
            el.id = perk.id;
            el.isSelected = false;
            el.perk = perk;
            obj.SetActive(true);
            return el;
        }
        catch { return null; }
    }

    private static StartingPerkElement FindElement(GameObject container, string id)
    {
        if (container == null || string.IsNullOrEmpty(id)) return null;
        try
        {
            var transform = container.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var el = child.gameObject.GetComponent<StartingPerkElement>();
                if (el != null && el.id == id) return el;
            }
        }
        catch { }
        return null;
    }

    private static SysDict.Dictionary<string, int> BuildRepState(SysDict.HashSet<string> perkIds)
    {
        var dict = new SysDict.Dictionary<string, int>();
        string[] allFactions = { "SECURITY", "REVOLUTION", "BLACK_MARKET", "LOWER_LEVEL", "UPPER_LEVEL" };
        foreach (var f in allFactions) dict[f] = 0;
        foreach (var perkId in perkIds)
        {
            if (perkId != null && RepEffects.TryGetValue(perkId, out var effects))
            {
                foreach (var kv in effects)
                {
                    if (kv.Key == "ALL")
                    {
                        foreach (var f in allFactions) dict[f] += kv.Value;
                    }
                    else if (dict.ContainsKey(kv.Key)) dict[kv.Key] += kv.Value;
                }
            }
        }
        return dict;
    }

    private static bool WouldBreakReputationFloor(string candidateId, SysDict.Dictionary<string, int> currentRep)
    {
        if (string.IsNullOrEmpty(candidateId) || !RepEffects.TryGetValue(candidateId, out var effects)) return false;
        foreach (var kv in effects)
        {
            string[] factions = kv.Key == "ALL" ? new string[] { "SECURITY", "REVOLUTION", "BLACK_MARKET", "LOWER_LEVEL", "UPPER_LEVEL" } : new string[] { kv.Key };
            foreach (var f in factions)
            {
                int cur = currentRep.TryGetValue(f, out var v) ? v : 0;
                if (cur + kv.Value < MinStartReputation) return true;
            }
        }
        return false;
    }
}
