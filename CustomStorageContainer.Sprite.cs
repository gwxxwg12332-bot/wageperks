using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace WagePerks
{

public static partial class CustomStorageContainer
{
        public static Sprite GetCustomSprite()
        {
            return _customSprite;
        }
        private static Sprite CreateCustomBoxSprite()
        {
            try
            {
                // 用嵌入的像素数据（用户设计图缩小到32×32，最近邻插值保留像素风）
                int width = StorageBoxPixels.Width;
                int height = StorageBoxPixels.Height;
                Color[] pixels = StorageBoxPixels.GetPixels();
                
                // 10-07 修复（玩家"蛙哥箱子也是反的"）：统一走 WagePixelSprites.CreatePixelSprite——内置 SetPixels 行序 Y 翻转
                //   （StorageBoxPixels 数组为 getdata 序=顶行在前，SetPixels 底行在前 → 直接写上下颠倒）
                return WagePixelSprites.CreatePixelSprite(pixels, width, height, 100f);
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] CreateCustomBoxSprite异常: {ex.Message}\n{ex.StackTrace}");
                return CreateFallbackSprite();
            }
        }
        // 10-19 P3 C组清理：GetGameStyleBoxPixel() 已删（死方法零调用，派活单§六；如需恢复见 git 历史）
        private static Sprite CreateFallbackSprite()
        {
            int width = 32;
            int height = 32;
            try
            {
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = GetBoxPixel(x, y, width, height);
                    }
                }
                // 10-07 修复：同上（GetBoxPixel 程序化 y=0 顶行 → 行序 Y 翻转统一由 CreatePixelSprite 处理）
                return WagePixelSprites.CreatePixelSprite(pixels, width, height, 100f);
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] CreateFallbackSprite异常: {ex.Message}");
                return null;
            }
        }
        private static Color GetBoxPixel(int x, int y, int w, int h)
        {
            // 边框（金属灰色）
            int border = Math.Max(2, w / 16);
            if (x < border || x >= w - border || y < border || y >= h - border)
            {
                float brightness = 1.0f - (x + y) / (float)(w + h) * 0.4f;
                return new Color(0.5f * brightness, 0.5f * brightness, 0.55f * brightness, 1f);
            }

            // 主体（深灰蓝色）
            Color bodyColor = new Color(0.25f, 0.28f, 0.32f, 1f);

            // 中央横向分隔线
            int midY = h / 2;
            if (y >= midY - 1 && y <= midY + 1)
            {
                return new Color(0.15f, 0.17f, 0.2f, 1f);
            }

            // 中央把手（上半部分中央）
            int handleX = w / 2;
            int handleY = h / 4;
            int handleW = Math.Max(8, w / 4);
            int handleH = Math.Max(3, h / 10);
            if (x >= handleX - handleW / 2 && x < handleX + handleW / 2 &&
                y >= handleY - handleH / 2 && y < handleY + handleH / 2)
            {
                if (x == handleX - handleW / 2 || x == handleX + handleW / 2 - 1 ||
                    y == handleY - handleH / 2 || y == handleY + handleH / 2 - 1)
                {
                    return new Color(0.6f, 0.6f, 0.65f, 1f);
                }
                return new Color(0.1f, 0.12f, 0.15f, 1f);
            }

            // 铆钉（四角）
            int rivetSize = Math.Max(1, w / 32);
            int rivetOffset = Math.Max(3, w / 8);
            if ((x >= rivetOffset && x < rivetOffset + rivetSize &&
                 y >= rivetOffset && y < rivetOffset + rivetSize) ||
                (x >= w - rivetOffset - rivetSize && x < w - rivetOffset &&
                 y >= rivetOffset && y < rivetOffset + rivetSize) ||
                (x >= rivetOffset && x < rivetOffset + rivetSize &&
                 y >= h - rivetOffset - rivetSize && y < h - rivetOffset) ||
                (x >= w - rivetOffset - rivetSize && x < w - rivetOffset &&
                 y >= h - rivetOffset - rivetSize && y < h - rivetOffset))
            {
                return new Color(0.7f, 0.7f, 0.75f, 1f);
            }

            // 下半部分标签区域
            if (y > midY + h / 8)
            {
                int labelX = w / 2;
                int labelY = h * 3 / 4;
                int labelW = Math.Max(10, w / 2);
                int labelH = Math.Max(4, h / 6);
                if (x >= labelX - labelW / 2 && x < labelX + labelW / 2 &&
                    y >= labelY - labelH / 2 && y < labelY + labelH / 2)
                {
                    return new Color(0.4f, 0.42f, 0.35f, 1f);
                }
            }

            return bodyColor;
        }
}
}
