using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using Il2Cpp;
using Il2CppInterop.Runtime;

namespace JacksonPerks
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
                
                
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.DontSave;
                tex.SetPixels(pixels);
                tex.Apply();
                
                // 创建sprite（pixelsPerUnit=100，32/100=0.32单位，跟卫生纸一样大）
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.DontSave;
                
                return sprite;
            }
            catch (Exception ex)
            {
                Core.LogMsg($"[自定义储物箱] CreateCustomBoxSprite异常: {ex.Message}\n{ex.StackTrace}");
                return CreateFallbackSprite();
            }
        }
        private static Color GetGameStyleBoxPixel(int x, int y, int w, int h)
        {
            // 透明背景（游戏物品都是透明背景）
            if (x < 2 || x >= w - 2 || y < 3 || y >= h - 2)
            {
                // 圆角效果
                if ((x < 3 && y < 4) || (x >= w - 3 && y < 4) ||
                    (x < 3 && y >= h - 3) || (x >= w - 3 && y >= h - 3))
                {
                    return new Color(0, 0, 0, 0);
                }
            }
            
            // 外边框（暗紫色，保留设计图元素）
            if (x == 2 || x == w - 3 || y == 3 || y == h - 3)
            {
                // 顶部高光
                if (y == 3 && x > 3 && x < w - 4)
                {
                    return new Color(0.50f, 0.35f, 0.55f, 1f);
                }
                return new Color(0.38f, 0.25f, 0.42f, 1f);
            }
            
            // 内边框（金属灰）
            if (x == 3 || x == w - 4 || y == 4 || y == h - 4)
            {
                if (y == 4 && x > 4 && x < w - 5)
                {
                    return new Color(0.55f, 0.52f, 0.48f, 1f);
                }
                return new Color(0.42f, 0.40f, 0.37f, 1f);
            }
            
            // 主体（暗金属灰，带渐变）
            float gradient = (y - 5) / (float)(h - 8);
            float r = 0.38f - gradient * 0.08f;
            float g = 0.36f - gradient * 0.08f;
            float b = 0.33f - gradient * 0.07f;
            
            // 顶部高光带
            if (y >= 5 && y <= 7 && x > 4 && x < w - 5)
            {
                r += 0.10f; g += 0.10f; b += 0.08f;
            }
            
            // 底部阴影带
            if (y >= h - 6 && y <= h - 5 && x > 4 && x < w - 5)
            {
                r -= 0.08f; g -= 0.08f; b -= 0.06f;
            }
            
            // 青色发光条（保留设计图元素，暗青色，像素风）
            int glowY = h / 2 - 1;
            if (y >= glowY && y <= glowY + 1 && x > 5 && x < w - 6)
            {
                // 跳过锁扣位置
                int lockX = w / 2;
                if (x < lockX - 4 || x > lockX + 4)
                {
                    if (y == glowY)
                    {
                        return new Color(0.35f, 0.65f, 0.70f, 1f); // 亮青色
                    }
                    else
                    {
                        return new Color(0.20f, 0.45f, 0.50f, 1f); // 暗青色
                    }
                }
            }
            
            // 中间分隔线（箱子盖和箱体的分界线）
            if (y == h / 2 + 2 && x > 4 && x < w - 5)
            {
                r = 0.28f; g = 0.26f; b = 0.23f;
            }
            
            // 锁扣（中间，紫色金属，保留设计图元素）
            int lockX2 = w / 2;
            int lockY2 = h / 2 - 2;
            if ((x >= lockX2 - 3 && x <= lockX2 + 3 && y >= lockY2 && y <= lockY2 + 6) ||
                (x >= lockX2 - 2 && x <= lockX2 + 2 && y >= lockY2 - 2 && y <= lockY2))
            {
                // 锁扣高光（紫色）
                if (x == lockX2 - 2 || y == lockY2 - 1)
                {
                    return new Color(0.55f, 0.40f, 0.60f, 1f);
                }
                // 锁孔
                if (x == lockX2 && y == lockY2 + 3)
                {
                    return new Color(0.15f, 0.10f, 0.18f, 1f);
                }
                return new Color(0.45f, 0.30f, 0.50f, 1f);
            }
            
            // 铆钉（四角，紫色金属，保留设计图元素）
            int rivet = 5;
            if ((x == rivet && y == rivet + 1) || (x == w - 1 - rivet && y == rivet + 1) ||
                (x == rivet && y == h - 1 - rivet) || (x == w - 1 - rivet && y == h - 1 - rivet))
            {
                return new Color(0.55f, 0.40f, 0.60f, 1f);
            }
            
            // 噪点纹理（像素风质感）
            if ((x * 7 + y * 13) % 19 == 0)
            {
                r -= 0.04f; g -= 0.04f; b -= 0.03f;
            }
            
            return new Color(
                Mathf.Clamp01(r),
                Mathf.Clamp01(g),
                Mathf.Clamp01(b),
                1f
            );
        }
        private static Sprite CreateFallbackSprite()
        {
            int width = 32;
            int height = 32;
            try
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.DontSave;
                
                Color[] pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        pixels[y * width + x] = GetBoxPixel(x, y, width, height);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                
                Sprite sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
                sprite.hideFlags = HideFlags.DontSave;
                return sprite;
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
