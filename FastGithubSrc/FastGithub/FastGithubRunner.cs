using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub
{
    /// <summary>
    /// 供宿主程序（GitPushTool）以「项目引用 + 命名空间」方式在同一个进程内启动/停止 FastGithub。
    ///
    /// 与独立运行 Program.Main 的区别：
    /// 1. 不调用 ConsoleUtil.DisableQuickEdit（WinForms 宿主没有控制台窗口）
    /// 2. 不调用 Run(singleton)，改用 StartAsync/StopAsync 的非阻塞通用主机托管
    /// 3. 不传 ParentProcessId（&lt;=0 时 AppHostedService 不会自动停止宿主），
    ///    由宿主程序显式调用 StopAsync 优雅收尾
    ///
    /// 注意：FastGithub 内部大量使用相对路径（appsettings/、cacert/、logs/、dnscrypt-proxy/），
    /// 因此启动时必须把 Environment.CurrentDirectory 指向内容根目录。
    /// </summary>
    public static class FastGithubRunner
    {
        private static readonly object SyncRoot = new object();
        private static WebApplication? app;

        /// <summary>当前是否正在运行</summary>
        public static bool IsRunning => Volatile.Read(ref app) != null;

        /// <summary>
        /// 在进程内启动 FastGithub 主机（非阻塞，启动完成后立即返回）
        /// </summary>
        /// <param name="contentRoot">内容根目录：appsettings / cacert / logs / dnscrypt-proxy 都在这里</param>
        /// <param name="udpLoggerPort">UDP 日志端口，0 表示使用默认 38457</param>
        public static async Task StartAsync(string contentRoot, int udpLoggerPort = 0)
        {
            lock (SyncRoot)
            {
                if (app != null)
                {
                    return;   // 进程内单例：已经启动过就不再重复启动
                }
            }

            if (string.IsNullOrWhiteSpace(contentRoot))
            {
                contentRoot = AppContext.BaseDirectory;
            }
            Directory.CreateDirectory(contentRoot);

            // 让所有相对路径都落在内容根，保证配置、证书、日志、dnscrypt-proxy 可被找到
            Environment.CurrentDirectory = contentRoot;

            var args = udpLoggerPort > 0
                ? new[] { $"UdpLoggerPort={udpLoggerPort}" }
                : Array.Empty<string>();

            var options = new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot,
                ApplicationName = typeof(FastGithubRunner).Assembly.GetName().Name
            };

            // 复用原项目的完整编排：ConfigureHost / ConfigureWebHost / ConfigureConfiguration /
            // ConfigureServices / ConfigureApp（Kestrel 反向代理、证书、WinDivert 拦截等）
            var host = Program.CreateWebApplication(options);
            await host.StartAsync();
            Volatile.Write(ref app, host);
        }

        /// <summary>
        /// 优雅停止：触发 IHost.StopAsync，恢复 ProxyOverride、停掉 dnscrypt-proxy、
        /// 释放 WinDivert 句柄。切勿用进程 Kill 代替
        /// </summary>
        public static async Task StopAsync()
        {
            WebApplication? host;
            lock (SyncRoot)
            {
                host = app;
                app = null;
            }
            if (host == null)
            {
                return;
            }
            await host.StopAsync();
            await host.DisposeAsync();
        }
    }
}
