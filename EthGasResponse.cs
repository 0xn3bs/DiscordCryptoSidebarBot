namespace DiscordCryptoSidebarBot
{
    public class EthGasResponse
    {
        public int Fast { get; set; }
        public int Fastest { get; set; }
        public int SafeLow { get; set; }
        public int Average { get; set; }
        public float Block_time { get; set; }
        public int BlockNum { get; set; }
        public float Speed { get; set; }
        public float SafeLowWait { get; set; }
        public float AvgWait { get; set; }
        public float FastWait { get; set; }
        public float FastestWait { get; set; }
    }
}