using System;

using System.Collections.Generic;

using System.Runtime.InteropServices;

using Il2Cpp;

using Il2CppInterop.Runtime;

using Il2CppInterop.Runtime.InteropTypes;

using HarmonyLib;

using MelonLoader;

using UnityEngine;



namespace JacksonPerks;
partial class LuckScoutBackpackUpgrade


{
    // ===== Shape =====



    // ===== SetShape 锁格子工具 =====

    /// <summary>

    /// 生成锁格子形状字符串：前slots个'0'（开放），后面'1'（障碍不能放）

    /// 按行优先排列：第0行前N格开放，第1行继续...

    /// </summary>

    private static string BuildLockedShape(int slots, int width, int height)

    {

        int total = width * height;

        char[] chars = new char[total];

        for (int i = 0; i < total; i++)

        {

            // 09-10 用户实测'封印和解锁反了' → 回滚：'0'=可放置/开放，'1'=关闭/锁（原实现正确；此前按拆包 stub 误改反）
            chars[i] = (i < slots) ? '0' : '1';

        }

        return new string(chars);

    }



    /// <summary>

    /// 应用锁格子形状到 GameGridInventory

    /// </summary>

    private static void ApplyLockedShape(GameGridInventory inv, int slots)

    {

        try

        {

            if (inv == null) return;

            if (slots < 1) slots = 1;

            if (slots > MAX_SLOTS) slots = MAX_SLOTS;

            string shape = BuildLockedShape(slots, GRID_WIDTH, GRID_HEIGHT);

            string __head = shape.Substring(0, Math.Min(20, shape.Length));

            inv.SetShape(shape, GRID_WIDTH);

            inv.Validate();

        }

        catch (Exception ex) { Core.LogMsg("[虚空珠] ApplyLockedShape异常: " + ex.Message); }

    }
}
