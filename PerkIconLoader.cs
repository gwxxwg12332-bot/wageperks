using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace JacksonPerks;

// 特性图标加载器（按照ExtraPerks开发指南实现）
internal static class PerkIconLoader
{
    private static Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();

    // 特性ID到嵌入资源名的映射
    private static readonly Dictionary<string, string> PerkIconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "蛙哥牛逼", "01_蛙哥牛逼.png" },
        { "博士之友", "02_博士之友.png" },
        { "退休枪匠之友", "03_退休枪匠之友.png" },
        { "水商之友", "04_水商之友.png" },
        { "酒商之友", "05_酒商之友.png" },
        { "捡漏直觉", "06_捡漏直觉.png" },
        { "刀尖舔血", "07_刀尖舔血.png" },
        { "好酒之徒", "08_好酒之徒.png" },
        { "童叟无欺", "18_童叟无欺.png" },
        { "笑面虎", "19_笑面虎.png" },
        { "招贼体质", "10_招贼体质.png" },
        { "霉运缠身", "11_霉运缠身.png" },
        { "信誉扫地", "12_信誉扫地.png" },
        { "王尔德之手", "13_王尔德之手.png" },
        { "命运之骰", "14_命运之骰.png" },
        { "治安部眼线", "15_治安部眼线.png" },
        { "流浪者", "16_流浪者.png" },
        { "蛙娘", "17_蛙娘.png" },
        // 兼容旧ID
        { "势利眼", "09_笑面虎.png" },
        { "丑陋店铺", "02_博士之友.png" },
        { "卡车上掉的货", "06_捡漏直觉.png" },
        { "奇葩人物", "01_蛙哥牛逼.png" },
        { "惹毛安保", "07_刀尖舔血.png" },
        { "深度睡眠", "08_好酒之徒.png" },
        { "夜猫子", "04_水商之友.png" },
    };

    internal static void Initialize()
    {
        Core.LogMsg("[特性图标] 图标加载器初始化完成（嵌入资源模式）");
    }

    // 加载嵌入资源PNG（按照ExtraPerks开发指南实现）
    internal static Sprite LoadEmbeddedPng(string resourceSuffix)
    {
        Assembly asm = typeof(Core).Assembly;
        string resName = null;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
            {
                resName = name;
                break;
            }
        }
        if (resName == null)
        {
            return null;
        }

        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null) return null;

        byte[] bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);

        // 按照指南：TextureFormat=4 (RGBA32)
        Texture2D tex = new Texture2D(2, 2, (TextureFormat)4, false, false)
        {
            filterMode = (FilterMode)0,    // Point — 像素风不模糊
            wrapMode = (TextureWrapMode)1, // Clamp — 边缘不重复
            hideFlags = (HideFlags)61      // HideAndDontSave
        };

        // 按照指南：用ImageConversion.LoadImage加载（用反射查找类）
        bool loaded = false;
        try
        {
            // 查找ImageConversion类（IL2CPP中不在标准命名空间，需反射查找）
            Type icType = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly a in assemblies)
            {
                Type[] types;
                try { types = a.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (Type t in types)
                {
                    if (t != null && t.Name == "ImageConversion")
                    {
                        icType = t;
                        break;
                    }
                }
                if (icType != null) break;
            }

            if (icType != null)
            {
                // 明确指定参数类型：Texture2D + Il2CppStructArray<byte>
                MethodInfo loadMethod = icType.GetMethod("LoadImage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) },
                    null);
                if (loadMethod == null)
                {
                    // 如果精确匹配失败，列出所有LoadImage方法
                    MethodInfo[] allMethods = icType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    foreach (MethodInfo m in allMethods)
                    {
                        if (m.Name == "LoadImage")
                        {
                            string paramStr = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                        }
                    }
                }
                if (loadMethod != null)
                {
                    loaded = (bool)loadMethod.Invoke(null, new object[] { tex, (Il2CppStructArray<byte>)bytes });
                }
            }
        }
        catch (Exception ex)
        {
            Core.LogMsg("[特性图标] ImageConversion.LoadImage调用失败: " + ex.Message);
        }

        if (!loaded)
        {
            UnityEngine.Object.Destroy(tex);
            Core.LogMsg("[特性图标] 加载失败: " + resourceSuffix);
            return null;
        }

        Sprite sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = (HideFlags)61;

        return sprite;
    }

    // 获取特性图标
    internal static Sprite GetPerkIcon(string perkId)
    {
        if (string.IsNullOrEmpty(perkId)) return null;
        string cleanId = perkId.Replace("\0", "").Trim();

        if (_iconCache.TryGetValue(cleanId, out Sprite cached))
            return cached;

        if (!PerkIconMap.ContainsKey(cleanId))
            return null;

        string resourceName = PerkIconMap[cleanId];
        Sprite sprite = LoadEmbeddedPng(resourceName);
        if (sprite != null)
            _iconCache[cleanId] = sprite;

        return sprite;
    }

    // 检查是否有自定义图标
    internal static bool HasCustomIcon(string perkId)
    {
        if (string.IsNullOrEmpty(perkId)) return false;
        string cleanId = perkId.Replace("\0", "").Trim();
        return PerkIconMap.ContainsKey(cleanId);
    }
}
