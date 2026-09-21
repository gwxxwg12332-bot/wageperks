using MelonLoader;

namespace JacksonPerks;

// 配置统一管理
internal static class ConfigHelper
{
    public static bool ContainerUpgradeEnabled => BuildConfig.ContainerUpgradeEnabled;
    public static bool ContainerHalfEnabled => BuildConfig.ContainerHalfEnabled;
}