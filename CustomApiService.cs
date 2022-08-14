using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCryptoSidebarBot
{
    public class CustomApiService
    {
        private readonly HttpClient _httpClient;
        private readonly BotSettings _settings;

        public CustomApiService(HttpClient httpClient, IOptions<BotSettings> settings) => (_httpClient, _settings) = (httpClient, settings.Value);

        public async Task<decimal> GetPrice()
        {
            var response = await _httpClient.GetStringAsync(_settings.CustomEndpoint);
            return decimal.Parse(response);
        }
    }
}
