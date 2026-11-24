using System;
using System.Net.Http;

namespace Starward.Helpers;

/// <summary>
/// R85：源探测 / 检查这类短请求共用一个 <see cref="HttpClient"/>。
/// 以前每点一次「检查全部源」都新建并丢弃一个客户端（连接池随 GC 抖动），
/// 源多时还会瞬时把这些请求一起打出去。
/// </summary>
public static class HttpClients
{
    /// <summary>探测用客户端：20 秒超时，进程内共享，调用方不要 Dispose。</summary>
    public static HttpClient Probe { get; } = new() { Timeout = TimeSpan.FromSeconds(20) };
}
