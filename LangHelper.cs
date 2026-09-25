using System;
using Il2Cpp;
using UnityEngine;

namespace JacksonPerks;

/// <summary>
/// 语言检测工具：mod 文本随游戏设置语言自动切换中英文。
/// 检测优先级：
///   1. LocHelper.GetCurrentLocaleCode() —— 游戏本地化系统当前语言代码（最准确，跟随游戏内设置）
///   2. Unity Localization SelectedLocale —— 兜底
///   3. Application.systemLanguage —— 最终兜底
/// </summary>
internal static class LangHelper
{
    /// <summary>当前是否为英文环境（游戏设置语言非中文）</summary>
    public static bool IsEnglish()
    {
        // 方法1：游戏本地化系统当前语言代码（最可靠，跟随游戏内设置）
        try
        {
            string code = LocHelper.GetCurrentLocaleCode();
            if (!string.IsNullOrEmpty(code))
                return !code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Exception ex) { Core.LogMsg("[LangHelper] 异常: " + ex.Message); }

        // 方法2：Unity Localization 当前语言
        try
        {
            var locale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
            if (locale != null)
            {
                string code = "";
                try { code = locale.Identifier.Code ?? ""; } catch { }
                if (!string.IsNullOrEmpty(code))
                    return !code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (System.Exception ex) { Core.LogMsg("[LangHelper] 异常: " + ex.Message); }

        // 方法3：系统语言兜底
        try
        {
            return Application.systemLanguage != SystemLanguage.ChineseSimplified &&
                   Application.systemLanguage != SystemLanguage.ChineseTraditional &&
                   Application.systemLanguage != SystemLanguage.Chinese;
        }
        catch (System.Exception ex) { Core.LogMsg("[LangHelper] 异常: " + ex.Message); }

        // 最终兜底：默认中文
        return false;
    }

    /// <summary>根据语言返回文本：中文或英文</summary>
    public static string T(string zh, string en)
    {
        return IsEnglish() ? en : zh;
    }

    /// <summary>安全版 T：本地化系统未就绪时直接返回中文，不触发同步等待</summary>
    public static string SafeT(string zh, string en)
    {
        try
        {
            return T(zh, en);
        }
        catch
        {
            return zh; // 本地化未就绪，返回中文兜底
        }
    }
}
