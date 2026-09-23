using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

public static partial class WageGirlSystem
{
    public static void RegisterToDirectory(ItemDirectory dir)
    {
        try
        {
            if (dir == null) return;
            ((Directory<GameItem>)(object)dir).Add(ENTITY_ID, (Il2CppSystem.Func<GameItem>)(() => CreateWageGirl()));
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 注册失败: " + ex.Message); }
    }
    private static GameItem CreateWageGirl()
    {
        try
        {
            GameItem it = ItemDirectory.CreateEmptyItem(null);
            if (it == null) return null;
            ApplyIcon(it);
            it.EnableTag(TAG);
            it.SetName(LangHelper.T("蛙娘", "Wage Girl"));
            it.identifier = ENTITY_ID; // 公开 setter（照骰子先例）——identifier 随档
            it.identifierName = "TYPE-STRING_" + ENTITY_ID; // 公开 setter
            it.shortDescription = LangHelper.T("蛙娘——蛙哥留下的仿生女仆。她会自己吃喝、干活，心情不好还会偷拿你的钱和货。但只要你好好照顾她，她会越来越信任你——从刚来时偷你100块，到后来只偷你个小零食；从站在角落不理你，到粘在你身边帮你抬价、叫客、销赃。双击打开她的状态面板。", "Wage Girl — an android maid left by Wage. She eats, works, and steals when moody. But take care of her, and she'll trust you more — from stealing 100 credits on day one to just a snack later; from hiding in the corner to standing by your side, boosting prices, calling customers, and fencing goods. Double-click to open her status panel.");
            it.longDescription = it.shortDescription;
            it.unitValue = 0; it.unitBaseValue = 0; // 09-19 价值归零：客户不买
            return it;
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 创建失败: " + ex.Message); return null; }
    }
    private static void ApplyIcon(GameItem it)
    {
        try { it.SetSpriteAndShape(ICON_ATLAS, ICON); }
        catch { }
    }
    private static Sprite SpriteFromPixels(Color[] pixels, int w, int h)
    {
        try
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            sp.hideFlags = HideFlags.DontSave;
            return sp;
        }
        catch { return null; }
    }
    private static void SetField(GameItem it, string field, object val)
    {
        try
        {
            var f = typeof(GameItem).GetField(field,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(it, val);
        }
        catch { }
    }
    private static bool ExistsInScene()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) return false;
            // 09-23 性能：原先每次调用都 new GameInventory[5] + foreach——本方法在热路径上（见 ExistsCached），
            // 每秒几十次分配纯属 GC 压力。改为 5 次直调，语义完全一致（短路顺序也保持原样）。
            if (FindGirlInInv((GameInventory)em.invElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.backInvinvElementCounter)) return true;
            if (FindGirlInInv((GameInventory)em.frontInvinvElement)) return true;
            if (FindGirlInInv((GameInventory)em.showcaseElement)) return true;
            if (FindGirlInInv((GameInventory)em.hiddenElement)) return true; // 09-23 补：海报后暗格
            return false;
        }
        catch { return false; }
    }
    private static bool FindGirlInInv(GameInventory inv)
    {
        if (inv == null || inv.childItems == null) return false;
        for (int i = 0; i < inv.childItems.Count; i++)
        {
            try
            {
                var it = inv.childItems[i];
                if (it == null) continue;
                if (it.identifier == ENTITY_ID) return true;
                if (it.contentWindow != null)
                {
                    var inner = AddictOfficerEvent.GetInnerInventory(it);
                    if (inner != null && FindGirlInInv(inner)) return true;
                }
            }
            catch { }
        }
        return false;
    }
    private static void TryGiveToBackpack()
    {
        try
        {
            EmporiumEntry em = EmporiumEntry.Instance;
            if (em == null) { Core.LogMsg("[蛙诊] GiveToBackpack: em null"); return; }
            if (em.backInvinvElement == null) { Core.LogMsg("[蛙诊] GiveToBackpack: backInv null"); return; }
            // 09-19 发放前全范围查重（5 网格+容器内部已有 → 不重复发）
            if (ExistsInScene()) { Core.LogMsg("[蛙诊] GiveToBackpack: ExistsInScene true"); return; }
            var inv = (GameInventory)em.backInvinvElement;
            GameItem item = DirectoryMaster.Item(ENTITY_ID, true);
            if (item == null) { Core.LogMsg("[蛙诊] GiveToBackpack: item null"); return; }
            // 照 GiveToBackpack 先例：TryFindOneValidInventorySlot → TryAcceptOnce（防同格重叠）；失败 UncheckedAccept 兜底
            var slot = em.backInvinvElement.TryFindOneValidInventorySlot(item, false);
            if (slot != null) { try { slot.TryAcceptOnce(); return; } catch (Exception) { } }
            inv.UncheckedAccept(item);
            Core.LogMsg("[蛙娘] 已发放实体到背包（全局常驻）");
        _returnTimer = 0.5f; _curState = ""; // 强制播return帧0.5s再切idle
        }
        catch (Exception ex) { Core.LogMsg("[蛙娘] 发放失败: " + ex.Message); }
    }
    private static GameItem FindGirlItem()
    {
        try
        {
            var em = EmporiumEntry.Instance;
            if (em == null) return null;
            GameInventory[] grids = new GameInventory[]
            {
                em.invElement as GameInventory,
                em.frontInvinvElement as GameInventory,
                em.showcaseElement as GameInventory,
                em.backInvinvElement as GameInventory
            };
            foreach (var gi in grids)
            {
                if (gi == null || gi.childItems == null) continue;
                for (int i = 0; i < gi.childItems.Count; i++)
                {
                    var c = gi.childItems[i];
                    if (c == null) continue;
                    if (c.identifier == ENTITY_ID) {
                        return c;
                    }
                }
            }
        }
        catch { }
        return null;
    }
}
