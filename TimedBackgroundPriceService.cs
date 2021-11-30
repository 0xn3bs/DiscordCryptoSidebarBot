using CoinGecko.Entities.Response.Coins;
using CoinGecko.Entities.Response.Simple;
using CoinGecko.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Rest;
using Discord.WebSocket;
using Discord;
using System.Net;
using System.Net.Http;
using System.IO;

namespace DiscordCryptoSidebarBot
{
    public class TimedBackgroundPriceService : IHostedService, IDisposable
    {
        private readonly ILogger<TimedBackgroundPriceService> _logger;
        private readonly BotSettings _settings;
        private readonly ICoinGeckoClient _client;
        private DiscordRestClient _discordRestClient = null!;
        private DiscordSocketClient _discordSocketClient = null!;
        private EthGasService _ethGasService = null!;
        private string _coinName = null!;
        private bool _firstRun = true;

        private Timer _timer = null!;

        class PriceBotActivity : IActivity
        {
            public string Name { get; set; }

            public ActivityType Type => ActivityType.Playing;

            public ActivityProperties Flags => ActivityProperties.None;
            public string Details { get; set; }
        }

        public TimedBackgroundPriceService(ILogger<TimedBackgroundPriceService> logger, 
            IOptions<BotSettings> settings, 
            ICoinGeckoClient client, 
            DiscordRestClient discordRestClient,
            DiscordSocketClient discordSocketClient,
            EthGasService ethGasService)
        {
            _logger = logger;
            _settings = settings.Value;
            _client = client;
            _discordRestClient = discordRestClient;
            _discordSocketClient = discordSocketClient;
            _ethGasService = ethGasService;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            ExecuteAsync().Wait();
            return Task.CompletedTask;
        }

        private bool InGasMode 
        { 
            get
            {
                return _settings.ApiId.ToLowerInvariant() == "ethgas";
            }
        }

        private async Task ExecuteAsync()
        {
            _logger.LogInformation("TimedBackgroundPriceService running.");
            
            await _discordRestClient.LoginAsync(TokenType.Bot, _settings.BotToken);
            await _discordSocketClient.LoginAsync(TokenType.Bot, _settings.BotToken);
            await _discordSocketClient.StartAsync();

            _discordSocketClient.Connected += DiscordSocketClientConnected;

            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(_settings.Delay),
                TimeSpan.FromSeconds(_settings.UpdateInterval));
        }

        private async Task DiscordSocketClientConnected()
        {
            if (InGasMode)
            {
                await SetLogo("ethgas.png");
                return;
            }

            var coinInfo = await _client.CoinsClient.GetAllCoinDataWithId(_settings.ApiId);

            await SetLogoFromCoinInfo(coinInfo);
        }

        private async Task SetLogoFromCoinInfo(CoinFullDataById coinInfo)
        {
            var fileName = coinInfo.Image.Large.Segments.Last();

            if (!Directory.Exists("images"))
            {
                Directory.CreateDirectory("images");
            }

            var path = Path.Combine("images", fileName);

            //  Download Coin Logo
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(coinInfo.Image.Large).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                var ms = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

                var fs = File.Create(path);

                ms.Seek(0, SeekOrigin.Begin);
                ms.CopyTo(fs);
                fs.Close();
                ms.Close();
            }

            await SetLogo(path);
        }

        private async Task SetLogo(string dir)
        {
            await _discordSocketClient.CurrentUser.ModifyAsync((p) =>
            {
                p.Avatar = new Optional<Image?>(new Image(dir));
            });
        }

        private static decimal? GetValue(Price price, string apiId, string field)
        {
            var dollars = string.Empty;
            var p = price.FirstOrDefault(x => x.Key == apiId).Value.FirstOrDefault(x => x.Key == field).Value;
            return p;
        }

        private static decimal? PriceToDollarValue(Price price, string apiId)
        {
            return GetValue(price, apiId, "usd");
        }

        private static decimal? PriceToChangeLast24Hr(Price price, string apiId)
        {
            return GetValue(price, apiId, "usd_24h_change");
        }

        private static string GetCoinNameFromApiId(IReadOnlyList<CoinList> coinList, string apiId) 
        {
            if (string.IsNullOrEmpty(apiId))
            {
                throw new ArgumentNullException(nameof(apiId));
            }

            if (coinList == null || coinList.Count == 0)
                return "!UNKNOWN!";

            var coin = coinList.FirstOrDefault(x => x.Id == apiId)!.Symbol.ToUpperInvariant();
            return coin;
        }

        private static string RenderDirection(decimal? value)
        {
            return value switch
            {
                >= 80 => "🌕",
                >= 50 => "🚀",
                >= 35 => "💰",
                >= 30 => "🔥",
                >= 25 => "🤑",
                >= 20 => "🏌",
                >= 15 => "😝",
                >= 10 => "😁",
                >= 7 => "😃",
                >= 5 => "🙂",
                > 0 => "📈",
                <= -80 => "☠️",
                <= -50 => "😀🔫",
                <= -40 => "🚽",
                <= -35 => "💩",
                <= -30 => "😱",
                <= -25 => "💸",
                <= -20 => "🤢",
                <= -15 => "🤬",
                <= -10 => "🥺",
                <= -7 => "😤",
                <= -5 => "😅",
                < 0 => "📉",
                0 => "😐",
                _ => "😐"
            };
        }

        private static bool HasDecimal(decimal? value)
        {
            var val = value.ToString()!;

            if (val.Contains("."))
            {
                var dec = val.Split('.');

                if(dec[0] == "0" || dec[0] == "00")
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static string RenderCurrency(decimal? value)
        {
            bool hasDecimal = HasDecimal(value);

            if (value < 1)
            {
                return string.Format("{0:C4}", value);
            }

            if (hasDecimal)
            {
                return string.Format("{0:C}", value);
            }

            return string.Format("{0:C0}", value);
        }

        private static string RenderPercent(decimal? value)
        {
            return string.Format("{0:0.00}%", value);
        }

        private void DoWork(object? state)
        {
            try
            {
                string nickname = string.Empty;
                string playing = string.Empty;

                if (InGasMode)
                {
                    var gas = _ethGasService.GetGas().GetAwaiter().GetResult();

                    nickname = $"⚡{gas.Fastest / 10}🏃{gas.Fast / 10}";
                    playing = $"🚶{gas.Average / 10}🐢{gas.SafeLow / 10}";

                    UpdateDiscordInfo(nickname, playing);
                    return;
                }

                if (_firstRun)
                {
                    var coinlist = _client.CoinsClient.GetCoinList().GetAwaiter().GetResult();
                    _coinName = GetCoinNameFromApiId(coinlist, _settings.ApiId);
                    _firstRun = false;
                }

                var price = _client.SimpleClient.GetSimplePrice(new string[] { _settings.ApiId }, new string[] { "usd" }, false, false, true, false).GetAwaiter().GetResult();

                var dollarValue = PriceToDollarValue(price, _settings.ApiId);
                var percentChange = PriceToChangeLast24Hr(price, _settings.ApiId);

                nickname = $"{_coinName} {RenderCurrency(dollarValue)} {RenderDirection(percentChange)}";
                playing = $"$ 24h: {RenderPercent(percentChange)}";

                UpdateDiscordInfo(nickname, playing);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occured while attempting to update, skipping this pass...");
            }
        }

        private void UpdateDiscordInfo(string nickname, string playing)
        {
            var guilds = _discordRestClient.GetGuildsAsync().GetAwaiter().GetResult();

            var activity = new PriceBotActivity();

            activity.Name = playing;
            activity.Details = playing;

            _logger.LogInformation($"{nickname} - {playing}");
            Console.WriteLine($"{nickname} - {playing}");

            _discordSocketClient.SetActivityAsync(activity).Wait();

            foreach (var guild in guilds)
            {
                var user = guild.GetCurrentUserAsync().GetAwaiter().GetResult();

                try
                {
                    user.ModifyAsync(x =>
                    {
                        x.Nickname = nickname;
                    }).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null!, null!);
                    Console.Error.WriteLine(ex);
                }
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TimedBackgroundPriceService is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

