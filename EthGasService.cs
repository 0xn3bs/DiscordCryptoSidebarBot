using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiscordCryptoSidebarBot
{
    public class EthGasService
    {
        private readonly HttpClient _httpClient;

        public EthGasService(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<EthGasResponse> GetGas()
        {
            var response = await _httpClient.GetStringAsync("https://ethgasstation.info/json/ethgasAPI.json");
            var ethGasResponse = JsonConvert.DeserializeObject<EthGasResponse>(response);
            return ethGasResponse;
        }
    }
}
