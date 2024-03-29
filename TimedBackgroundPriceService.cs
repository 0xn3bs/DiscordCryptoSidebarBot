﻿using CoinGecko.Entities.Response.Coins;
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
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics;

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
        private CustomApiService _customApiService = null!;
        private string _coinName = null!;
        private bool _firstRun = true;

        private Timer _timer = null!;

        private Dictionary<ulong, (ulong?, ulong?)> _guildToRoleIds;
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
            EthGasService ethGasService,
            CustomApiService customApiService)
        {
            _logger = logger;
            _settings = settings.Value;
            _client = client;
            _discordRestClient = discordRestClient;
            _discordSocketClient = discordSocketClient;
            _ethGasService = ethGasService;
            _customApiService = customApiService;

            _guildToRoleIds = new Dictionary<ulong, (ulong?, ulong?)>();
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
                return _settings.ApiId?.ToLowerInvariant() == "ethgas";
            }
        }

        private bool InCustomApiMode
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_settings.CustomEndpoint);
            }
        }

        private bool HasCustomTicker
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_settings.CustomTicker);
            }
        }

        private bool HasCustomAvatar
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_settings.CustomAvatar);
            }
        }

        private bool HasCustomEmoji
        {
            get
            {
                return !string.IsNullOrWhiteSpace(_settings.CustomEmoji);
            }
        }

        private bool IsClustered
        {
            get
            {
                return !string.IsNullOrEmpty(_settings.ClusterPath) && !string.IsNullOrEmpty(_settings.ClusterName);
            }
        }

        private string? ClusterFilename
        {
            get
            {
                return IsClustered ? Path.Combine(_settings.ClusterPath!, $"{_settings.ClusterName!}.bot") : null;
            }
        }

        private string? ClusterSimplePriceFilename
        {
            get
            {
                return IsClustered ? Path.Combine(_settings.ClusterPath!, "simpleprice.json") : null;
            }
        }

        private string? CoinlistFilename
        {
            get
            {
                return IsClustered ? Path.Combine(_settings.ClusterPath!, "coinlist.json") : null;
            }
        }

        private bool IsClusterLeader
        {
            get
            {
                return IsClustered && _settings.ClusterLeader.HasValue && _settings.ClusterLeader.Value;
            }
        }

        private async Task ConfigureCluster()
        {
            if (IsClustered)
            {
                Directory.CreateDirectory(_settings.ClusterPath!);

                using (var f = File.Create(ClusterFilename!))
                using (var s = new StreamWriter(f))
                {
                    await s.WriteAsync(_settings.ApiId);
                }
            }
        }

        private async Task ExecuteAsync()
        {
            _logger.LogInformation("TimedBackgroundPriceService running.");

            await ConfigureCluster();
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

            if (InCustomApiMode)
            {
                return;
            }

            if (HasCustomAvatar)
            {
                if (IsLocalPath(_settings.CustomAvatar))
                {
                    await SetLogo(_settings.CustomAvatar);
                }
                else
                {
                    var uri = new Uri(_settings.CustomAvatar);
                    await SetLogoFromUri(uri);
                }
            }
            else
            {
                var coinInfo = await _client.CoinsClient.GetAllCoinDataWithId(_settings.ApiId);
                await SetLogoFromCoinInfo(coinInfo);
            }

            var guilds = _discordSocketClient.Guilds;

            foreach (var guild in guilds)
            {
                var gainRoleId = guild.Roles.FirstOrDefault(x => x.Name.ToLowerInvariant() == _settings.GainRoleName.ToLowerInvariant())?.Id;
                var lossRoleId = guild.Roles.FirstOrDefault(x => x.Name.ToLowerInvariant() == _settings.LossRoleName.ToLowerInvariant())?.Id;

                _guildToRoleIds[guild.Id] = (gainRoleId, lossRoleId);
            }
        }

        private async Task SetLogoFromUri(Uri url)
        {
            var fileName = url.Segments.Last();

            if (!Directory.Exists("images"))
            {
                Directory.CreateDirectory("images");
            }

            var path = Path.Combine("images", fileName);

            //  Download Coin Logo
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(url).GetAwaiter().GetResult();
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

        private async Task SetLogoFromCoinInfo(CoinFullDataById coinInfo)
        {
            await SetLogoFromUri(coinInfo.Image.Large);
        }

        private async Task SetLogo(string dir)
        {
            await _discordSocketClient.CurrentUser.ModifyAsync((p) =>
            {
                p.Avatar = new Optional<Image?>(new Image(dir));
            });
        }

        private static decimal? GetValue(Price? price, string apiId, string field)
        {
            var dollars = string.Empty;
            if (price != null)
            {
                var p = price.FirstOrDefault(x => x.Key == apiId).Value.FirstOrDefault(x => x.Key == field).Value;
                return p;
            }

            return null;
        }

        private static decimal? PriceToDollarValue(Price? price, string apiId)
        {
            return GetValue(price, apiId, "usd");
        }

        private static decimal? PriceToChangeLast24Hr(Price? price, string apiId)
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

                if (dec[0] == "0" || dec[0] == "00")
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

            if (hasDecimal && value >= 1000m)
            {
                return string.Format("{0:C0}", value);
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
            Process().GetAwaiter().GetResult();
        }

        private async Task<IReadOnlyList<CoinList>> GetCoinListAsync()
        {
            if (IsClusterLeader)
            {
                var coinlist = await _client.CoinsClient.GetCoinList();

                using (var f = File.Create(CoinlistFilename!))
                using (var s = new StreamWriter(f))
                {
                    await JsonSerializer.SerializeAsync(s.BaseStream, coinlist);
                }

                return coinlist;
            }
            else
            {
                using(var f = File.OpenRead(CoinlistFilename!))
                using(var s = new StreamReader(f))
                {
                    var coinlist = (await JsonSerializer.DeserializeAsync<IReadOnlyList<CoinList>>(s.BaseStream))!;
                    return coinlist;
                }
            }
        }

        private async Task<Price?> GetSimplePrice()
        {
            if (IsClusterLeader)
            {
                var price = await _client.SimpleClient.GetSimplePrice(ClusterApiIds, new string[] { "usd" }, false, false, true, false);

                using (var f = File.Create(ClusterSimplePriceFilename))
                using (var s = new StreamWriter(f))
                {
                    await JsonSerializer.SerializeAsync(s.BaseStream, price);
                    return price;
                }
            }
            else
            {
                using(var f = File.OpenRead(ClusterSimplePriceFilename))
                using(var s = new StreamReader(f))
                {
                    var price = await JsonSerializer.DeserializeAsync<Price>(s.BaseStream);
                    return price;
                }
            }
        }

        private string[] ClusterApiIds
        {
            get
            {
                var files = Directory.GetFiles(_settings.ClusterPath!, "*.bot", SearchOption.AllDirectories);

                var apiIds = new List<string>();

                foreach(var file in files)
                {
                    var coinId = File.ReadAllText(file);
                    apiIds.Add(coinId);
                }

                return apiIds.ToArray();
            }
        }

        private async Task Process()
        {
            try
            {
                string nickname = string.Empty;
                string playing = string.Empty;

                if (InGasMode)
                {
                    var gas = await _ethGasService.GetGas();

                    nickname = $"⚡{gas.Rapid}🏃{gas.Fast}";
                    playing = $"🚶{gas.Standard}🐢{gas.Slow}";

                    await UpdateDiscordInfo(nickname, playing, null);
                    return;
                }

                if (_firstRun)
                {
                    //  Cluster should grab coinlist and cache
                    var coinlist = await GetCoinListAsync();

                    if (!HasCustomTicker)
                    {
                        _coinName = GetCoinNameFromApiId(coinlist, _settings.ApiId);
                    }
                    else
                    {
                        _coinName = _settings.CustomTicker!;
                    }

                    _firstRun = false;
                }

                decimal? dollarValue = null;
                decimal? percentChange = null;

                if (!InCustomApiMode)
                {
                    var price = await GetSimplePrice()!;
                    dollarValue = PriceToDollarValue(price, _settings.ApiId);
                    percentChange = PriceToChangeLast24Hr(price, _settings.ApiId);
                }
                else
                {
                    dollarValue = await _customApiService.GetPrice();
                }

                if (!HasCustomEmoji)
                {
                    nickname = $"{_coinName} {RenderCurrency(dollarValue)} {RenderDirection(percentChange)}";
                }
                else
                {
                    nickname = $"{_coinName} {RenderCurrency(dollarValue)} {_settings.CustomEmoji}";
                }

                if (percentChange != null)
                {
                    playing = $"$ 24h: {RenderPercent(percentChange)}";
                }

                await UpdateDiscordInfo(nickname, playing, percentChange);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occured while attempting to update, skipping this pass...");
            }
        }

        private async Task UpdateDiscordInfo(string nickname, string playing, decimal? percentChange)
        {
            var guilds = await _discordRestClient.GetGuildsAsync();

            var activity = new PriceBotActivity();

            activity.Name = playing;
            activity.Details = playing;

            _logger.LogInformation($"{nickname} - {playing}");
            Console.WriteLine($"{nickname} - {playing}");

            try
            {
                await _discordSocketClient.SetActivityAsync(activity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set Discord Activity");
                Console.Error.WriteLine($"Failed to set Discord Activity, {ex}");
            }

            foreach (var guild in guilds)
            {
                var user = await guild.GetCurrentUserAsync();

                try
                {
                    await AssignGuildRoles(percentChange, user, guild.Id);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Failed to set Discord Guild role in {guildid}", guild.Id);
                    Console.Error.WriteLine($"Failed to set Guild role in {guild.Id}, {ex}");
                }

                try
                {
                    await user.ModifyAsync(x =>
                    {
                        x.Nickname = nickname;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to set Discord Nickname");
                    Console.Error.WriteLine($"Failed to set Discord Nickname, {ex}");
                }
            }
        }

        private async Task AssignGuildRoles(decimal? percentChange, RestGuildUser user, ulong guildId)
        {
            if (percentChange == null)
                return;

            if (!_guildToRoleIds.ContainsKey(guildId))
                return;

            var roles = _guildToRoleIds[guildId];

            var gainRoleId = roles.Item1;
            var lossRoleId = roles.Item2;

            if (gainRoleId != null && percentChange.HasValue)
            {
                if (percentChange.Value >= 0)
                {
                    if (!user.RoleIds.Contains(gainRoleId.Value))
                    {
                        await user.AddRoleAsync(gainRoleId.Value);
                    }
                }
                else
                {
                    if (user.RoleIds.Contains(gainRoleId.Value))
                    {
                        await user.RemoveRoleAsync(gainRoleId.Value);
                    }
                }
            }

            if (lossRoleId != null && percentChange.HasValue)
            {
                if (percentChange.Value < 0)
                {
                    if (!user.RoleIds.Contains(lossRoleId.Value))
                    {
                        await user.AddRoleAsync(lossRoleId.Value);
                    }
                }
                else
                {
                    if (user.RoleIds.Contains(lossRoleId.Value))
                    {
                        await user.RemoveRoleAsync(lossRoleId.Value);
                    }
                }
            }
        }

        private static bool IsLocalPath(string p)
        {
            return new Uri(p).IsFile;
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

