using Microsoft.AspNetCore.Builder;
using System;
using System.IO;

namespace FastGithub
{

    class Program
    {
        /// <summary>
        /// 程序入口
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            ConsoleUtil.DisableQuickEdit();
            var contentRoot = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(contentRoot) == false)
            {
                Environment.CurrentDirectory = contentRoot;
            }
            var options = new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = contentRoot
            };
            CreateWebApplication(options).Run(singleton: true);
        }

        /// <summary>
        /// 创建host
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <summary>
        /// 创建host（internal：供同程序集内的 FastGithubRunner 在进程内托管时复用）
        /// </summary>
        internal static WebApplication CreateWebApplication(WebApplicationOptions options)
        {
            var builder = WebApplication.CreateBuilder(options);
            builder.ConfigureHost();
            builder.ConfigureWebHost();
            builder.ConfigureConfiguration();
            builder.ConfigureServices();

            var app = builder.Build();
            app.ConfigureApp();
            return app;
        }

    }
}
