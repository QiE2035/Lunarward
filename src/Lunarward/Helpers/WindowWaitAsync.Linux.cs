// Lunarward：上游依赖 Scighost.WinUI 的 Window.WaitAsync 扩展，这里提供等价实现。
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace Starward.Helpers;

public static class WindowWaitAsyncExtension
{
    /// <summary>等待窗口关闭，返回 true（上游用它做模态流程）。</summary>
    public static Task<bool> WaitAsync(this Window window)
    {
        var tcs = new TaskCompletionSource<bool>();
        window.Closed += (_, _) => tcs.TrySetResult(true);
        return tcs.Task;
    }
}
