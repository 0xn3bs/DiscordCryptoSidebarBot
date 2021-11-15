using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using Discord.Rest;
using Discord.WebSocket;
using System.Globalization;
using System.Threading;

namespace DiscordCryptoSidebarBot // Note: actual namespace depends on the project name.
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            using IHost host = CreateHostBuilder(args).Build();

            await host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(path: "config.json", optional: false, reloadOnChange: true)
                .AddCommandLine(args)
                .Build();

            return Host.CreateDefaultBuilder(args).ConfigureServices((context, services) =>
            {
                services.Configure<BotSettings>(configuration)
                        .AddSingleton<DiscordRestClient>()
                        .AddSingleton<DiscordSocketClient>()
                        .AddSingleton<ICoinGeckoClient>(CoinGeckoClient.Instance)
                        .AddHostedService<TimedBackgroundPriceService>();
            });
        }
    }
}
