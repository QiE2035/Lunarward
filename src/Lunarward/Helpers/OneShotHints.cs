// Lunarward：一次性提示的持久化开关（D86）。
//
// 起因：这几处提示原来都用 `private static bool` 控制，进程一重启就复位 ——
// 于是「已自动识别 N 个游戏」「全局热键不可用（Wayland）」「系统代理」这类提示每次开启动器都来一遍。
// 上游 Windows 的对应提示（如系统代理）本来就是「装好之后提醒一次」的语义，持久化才是对的。
//
// 用 SQLite 的 Setting 表（AppConfig.GetValue/SetValue），不额外引入配置文件。
namespace Starward.Helpers;

internal static class OneShotHints
{
    /// <summary>
    /// 「这条提示现在该不该显示」：第一次返回 true 并记下；之后永远 false。
    /// 数据库不可用时退化成「每个进程一次」——宁可多一次，也不要静默丢掉第一次的告知。
    /// </summary>
    public static bool TryTake(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        string key = "hint_" + name;
        try
        {
            if (AppConfig.GetValue(false, key))
            {
                return false;
            }
            AppConfig.SetValue(true, key);
            return true;
        }
        catch
        {
            return true;
        }
    }
}
