﻿using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANCMStressTestSite
{
    public class Program
    {
        public static IApplicationLifetime AppLifetime;
        public static bool AppLifetimeStopping = false;

        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .ConfigureLogging((_, factory) =>
                {
                    factory.AddConsole();
                })
                .UseKestrel()
                .UseStartup<Startup>()
                .Build();

            host.Run();

            AppLifetime = (IApplicationLifetime)host.Services.GetService(typeof(IApplicationLifetime));
            AppLifetime.ApplicationStopping.Register(
                () => {
                    AppLifetimeStopping = true;
                }
            );

            host.Run();
        }
    }
}
