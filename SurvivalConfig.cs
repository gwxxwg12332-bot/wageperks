using MelonLoader;

namespace WageSurvival;

// 10-07 #13（用户拍板"WS 配置+机器效率开关+捡漏+10"）：WS 侧 MelonPreferences 配置
//   （MelonPreferences.cfg 可改——与 WP BuildConfig 同源配置哲学；改了不生效先删配置文件重新生成）
internal static class SurvivalConfig
{
    private static MelonPreferences_Category _cat;
    private static MelonPreferences_Entry<bool> _effHalfEnabled;
    private static MelonPreferences_Entry<float> _effRatio;
    private static MelonPreferences_Entry<int> _luckScoutBonus;

    public static void Init()
    {
        try
        {
            _cat = MelonPreferences.CreateCategory("WageSurvival", "Wage Survival 生存配置");
            _effHalfEnabled = _cat.CreateEntry<bool>("MachineEffHalfEnabled", true, "机器效率减半开关（true=鲁滨逊机器效率减半生效）");
            _effRatio = _cat.CreateEntry<float>("MachineEffRatio", 0.5f, "机器效率减半比例（0~1，默认 0.5）");
            _luckScoutBonus = _cat.CreateEntry<int>("LuckScoutScavBonus", 10, "捡漏直觉拾荒加成（默认 +10）");
            _cat.SaveToFile(false);
            Core.LogMsg("[WageSurvival.Config] 配置初始化完成");
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Config] Init异常: " + ex.Message); }
    }

    // 机器效率减半是否生效（门控：关=原值不乘）
    public static bool EffHalfEnabled { get { try { return _effHalfEnabled != null && _effHalfEnabled.Value; } catch { return true; } } }
    // 减半比例（默认 0.5）
    public static float EffRatio { get { try { return _effRatio != null ? _effRatio.Value : 0.5f; } catch { return 0.5f; } } }
    // 捡漏直觉拾荒加成（默认 +10）
    public static int LuckScoutScavBonus { get { try { return _luckScoutBonus != null ? _luckScoutBonus.Value : 10; } catch { return 10; } } }

    // 10-07 配置器入口：切换机器效率减半开关并落盘（配置器窗口按钮调用——读时取值即时生效）
    public static void ToggleEffHalf()
    {
        try
        {
            if (_effHalfEnabled == null) return;
            _effHalfEnabled.Value = !_effHalfEnabled.Value;
            if (_cat != null) _cat.SaveToFile(false);
            Core.LogMsg("[WageSurvival.Config] 机器效率减半切换为 " + (_effHalfEnabled.Value ? "开" : "关"));
        }
        catch (System.Exception ex) { Core.LogMsg("[WageSurvival.Config] ToggleEffHalf异常: " + ex.Message); }
    }
}
