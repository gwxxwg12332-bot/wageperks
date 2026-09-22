using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace JacksonPerks;

// ============================================================
// 蛙娘系统（09-21 开工，话术 v9 拆包回填 9 项）
// 拍板：实体占地 2×3，全局常驻（不选任何特性也出现）
// 阶段 1：实体注册 + 六维状态 + 常驻面板 + 每日衰减 + 双击面板
// 阶段 2+：喂食/照顾好感、在场增益（预算×4+议价+50）、自动叫客+治安预判、
//          偷钱循环+自主偷拿、好物+销赃+跑路回归（后续迭代）
// ============================================================
public static partial class WageGirlSystem
{
}

// ===================== 蛙娘占位像素图标（32×32 透明底，绿色蛙身+眼睛+腮红） =====================
internal static class WageGirlIcons
{
    public static Color[] Pixels()
    {
        const int S = 32;
        var px = new Color[S * S];
        // 透明底
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
        Color body = new Color(0.36f, 0.72f, 0.42f, 1f);   // 蛙绿
        Color dark = new Color(0.12f, 0.34f, 0.20f, 1f);   // 深描边
        Color eyeW = new Color(0.95f, 0.98f, 0.95f, 1f);   // 眼白
        Color eyeB = new Color(0.10f, 0.10f, 0.12f, 1f);   // 瞳孔
        Color blush = new Color(0.95f, 0.55f, 0.60f, 1f);  // 腮红

        void Set(int x, int y, Color c) { if (x >= 0 && x < S && y >= 0 && y < S) px[y * S + x] = c; }
        // 身体（圆头 + 方身）：中心 16, 行 6-25
        for (int y = 5; y <= 25; y++)
        {
            int half = (y < 14) ? 9 : 8; // 头圆身方
            int x0 = 16 - half, x1 = 16 + half;
            if (y >= 14) { x0 = 7; x1 = 24; }
            for (int x = x0; x <= x1; x++) Set(x, y, body);
        }
        // 深色描边：左右各 1 列 + 底边
        for (int y = 5; y <= 25; y++) { Set(6, y, dark); Set(25, y, dark); }
        for (int x = 6; x <= 25; x++) { Set(x, 25, dark); Set(x, 5, dark); }
        // 眼睛（两枚 3×3 白 + 1px 瞳孔）
        for (int y = 10; y <= 12; y++) { for (int x = 11; x <= 13; x++) Set(x, y, eyeW); for (int x = 19; x <= 21; x++) Set(x, y, eyeW); }
        Set(12, 11, eyeB); Set(20, 11, eyeB);
        // 腮红（两枚 2×2）
        Set(9, 17, blush); Set(10, 17, blush); Set(9, 18, blush); Set(10, 18, blush);
        Set(21, 17, blush); Set(22, 17, blush); Set(21, 18, blush); Set(22, 18, blush);
        // 嘴（微笑 3px）
        Set(15, 20, dark); Set(16, 21, dark); Set(17, 20, dark);
        return px;
    }
}
