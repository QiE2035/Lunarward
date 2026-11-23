using System;

// Lunarward：跨页面「打开设置并直达某个子页」的最小机制。
//
// 预检对话框里的修复按钮需要把用户送到「运行环境」页，但设置页是在导航时才构造的，
// 发消息会早于订阅。这里放一个一次性目标，SettingPage 在构造时读走并清空。
namespace Starward.Features.Setting;


public static class SettingNavigation
{

    private static Type? _target;


    /// <summary>设置页构造时读到的目标子页；读取后自动清空，避免下次进设置还停在这里。</summary>
    public static Type? TakeTarget()
    {
        Type? target = _target;
        _target = null;
        return target;
    }


    public static void SetTarget(Type? page)
    {
        _target = page;
    }


    private static (string? ProfileId, bool Enhance)? _runtimeTarget;


    /// <summary>R26：预检项要把用户送到「运行环境」里对应方案的指定分组，而不是永远停在第一份方案上。</summary>
    public static void SetRuntimeTarget(string? profileId, bool enhance)
    {
        _runtimeTarget = (profileId, enhance);
    }


    public static (string? ProfileId, bool Enhance)? TakeRuntimeTarget()
    {
        (string? ProfileId, bool Enhance)? target = _runtimeTarget;
        _runtimeTarget = null;
        return target;
    }

}
