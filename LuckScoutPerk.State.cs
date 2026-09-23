using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class LuckScoutPerk : CustomStartingPerk
{
    private static int GetCount(string key, ref int cache)

    {

        if (cache < 0)

        {

            if (WageSaveStore.HasKey("LuckScout", key))
            {
                cache = WageSaveStore.GetInt("LuckScout", key, 0);
            }
            else
            {
                try { cache = PlayerPrefs.GetInt(key, 0); } catch { cache = 0; }
                if (cache != 0) { try { WageSaveStore.SetInt("LuckScout", key, cache); } catch { } } // 回填新层，打烊落盘即完成迁移
            }

        }

        return cache;

    }
    private static void SetCount(string key, int val, ref int cache)

    {

        cache = val;

        try { WageSaveStore.SetInt("LuckScout", key, val); } catch { }

    }
    private static void SaveCountToPlayerPrefs()

    {

        try

        {

            if (_scavCount >= 0) WageSaveStore.SetInt("LuckScout", PK_SCAV, _scavCount);

            if (_scavLevel >= 0) WageSaveStore.SetInt("LuckScout", PK_LEVEL, _scavLevel);

            if (_rareCount >= 0) WageSaveStore.SetInt("LuckScout", PK_RARE, _rareCount);

            if (_firstRareDone >= 0) WageSaveStore.SetInt("LuckScout", PK_FIRST, _firstRareDone);


        } catch (Exception ex) { Core.LogMsg("[捡漏直觉] SaveCount持久化失败: " + ex.Message); }

    }
    public static void PostfixSaveGame()

    {

        try { if (IsActive()) SaveCountToPlayerPrefs(); } catch { }

    }
    private static int GetScavCount() { return GetCount(PK_SCAV, ref _scavCount); }
    private static void SetScavCount(int v) { SetCount(PK_SCAV, v, ref _scavCount); }
    private static int GetScavLevel() { return GetCount(PK_LEVEL, ref _scavLevel); }
    private static void SetScavLevel(int v) { SetCount(PK_LEVEL, v, ref _scavLevel); }
    private static int GetRareCount() { return GetCount(PK_RARE, ref _rareCount); }
    private static void SetRareCount(int v) { SetCount(PK_RARE, v, ref _rareCount); }
    private static int GetFirstRare() { return GetCount(PK_FIRST, ref _firstRareDone); }
    private static void SetFirstRare(int v) { SetCount(PK_FIRST, v, ref _firstRareDone); }
    internal static bool IsActive() => Core.PerkActive(PerkId);
    internal static void ResetGiveFlag()
    {
        _kitGiven = false;
    }
    public static void OnUpdateGiveRetry()
    {
        try
        {
            if (!_pendingGive) return;
            if (_pendingGiveFrames-- <= 0) { _pendingGive = false; return; } // 超时放弃
            EmporiumEntry emporium = EmporiumEntry.Instance;
            if (emporium == null || emporium.backInvinvElement == null) return; // 未就绪继续等
            TryGiveKit();
        }
        catch { }
    }
    internal override void OnNewGame()

    {

        ResetState();

    }
    internal static void ResetState()

    {

        _kitGiven = false;

        _scavCount = -1; _scavLevel = -1; _rareCount = -1; _firstRareDone = -1;

        _initialItemHandled = false;


    }
    internal static void FullReset()

    {

        _kitGiven = false;

        _scavCount = 0; _scavLevel = 0; _rareCount = 0; _firstRareDone = 0;

        try { WageSaveStore.ClearNamespace("LuckScout"); } catch { }

        // 旧层卫生：删裸 PlayerPrefs 全局键（历史遗留，无 runID 隔离，清一次永远干净）

        try { PlayerPrefs.DeleteKey(PK_SCAV); PlayerPrefs.DeleteKey(PK_LEVEL);

              PlayerPrefs.DeleteKey(PK_RARE); PlayerPrefs.DeleteKey(PK_FIRST); PlayerPrefs.Save(); } catch { }


    }
}
