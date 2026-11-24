using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace Starward.Helpers;

/// <summary>
/// R86：Uno 同一时间只允许一个 <see cref="ContentDialog"/>。以前二十多处裸 <c>ShowAsync()</c>
/// 全靠调用方自觉，连点「检查 / 删除 / 清理」就会抛 <c>Only a single ContentDialog can be open</c>，
/// 而处理器是 <c>async void</c> —— 等于进程崩。
/// 现在所有对话框走这里：已有对话框打开时直接返回 <see cref="ContentDialogResult.None"/>，
/// 调用方的「不是 Primary 就返回」自然把它当成用户取消。
/// </summary>
public static class DialogGate
{
    private static int _open;


    /// <summary>打开对话框；已有对话框打开时立即返回 None，不再抛。</summary>
    public static async Task<ContentDialogResult> Show(ContentDialog dialog)
    {
        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0)
        {
            return ContentDialogResult.None;
        }
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            Volatile.Write(ref _open, 0);
        }
    }


    /// <summary>
    /// R87：调用方刚 <c>Hide()</c> 了外层对话框、要接着开子对话框时，先等这里的锁真正放掉
    /// （<c>Hide()</c> 到 <c>ShowAsync</c> 续体之间是异步的），否则子对话框会被当成「已有对话框」直接拒掉。
    /// </summary>
    public static async Task WaitClosedAsync()
    {
        for (int i = 0; i < 200 && Volatile.Read(ref _open) != 0; i++)
        {
            await Task.Delay(15);
        }
    }
}
