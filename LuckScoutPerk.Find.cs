using System;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

internal sealed partial class LuckScoutPerk : CustomStartingPerk
{
    private static GameItem FindPlayerScanner()

    {

        return FindScannerInOwned();

    }
    public static void PostfixGetAllAfterhourOwnedItems(Il2CppSystem.Collections.Generic.List<GameItem> __result)

    {

        try

        {

            if (!IsActive()) return;

            if (__result == null) return;

            if (_inAfterhourScan) return; // 重入（FindScannerInOwned 内部调用）→ 跳过，防递归

            GameItem scanner = FindScannerInOwned(); // 标志在内部管理

            if (scanner == null) return;

            for (int i = 0; i < __result.Count; i++)

                if (__result[i] == scanner) return; // 已含，防重复

            __result.Add(scanner);

        }

        catch { }

    }
    private static GameItem FindScannerInOwned()

    {

        if (_inAfterhourScan) return null;

        _inAfterhourScan = true;

        try

        {

            foreach (GameItem it in EmporiumEntry.Instance.GetAllAfterhourOwnedItems())

            {

                if (it == null) continue;

                try { if (it.identifier == "luck_scout_toolbox") { var s = FindScannerInContainer(it); if (s != null) return s; } } catch { }

                try { if (it.identifier == "metal_scanner" && it.IsTag(SCANNER_TAG)) return it; } catch { }

            }

        }

        catch { }

        finally { _inAfterhourScan = false; }

        return null;

    }
    private static GameInventory GetToolboxInventory(GameItem kit)

    {

        try

        {

            var w = kit.contentWindow;

            if (w == null) return null;

            var p = w.GetType().GetProperty("inventory", BindingFlags.Public | BindingFlags.Instance);

            return p?.GetValue(w) as GameInventory;

        }

        catch { return null; }

    }
    private static GameItem FindScannerInContainer(GameItem container)

    {

        try

        {

            GameInventory inner = GetToolboxInventory(container);

            if (inner == null || inner.childItems == null) return null;

            for (int i = 0; i < inner.childItems.Count; i++)

            {

                GameItem c = inner.childItems[i];

                if (c == null) continue;

                try { if (c.identifier == "metal_scanner" && c.IsTag(SCANNER_TAG)) return c; } catch { }

            }

        }

        catch { }

        return null;

    }
}
