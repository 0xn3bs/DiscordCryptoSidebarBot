using System.Numerics;

namespace DiscordCryptoSidebarBot
{
    public class Gas
    {
        private BigInteger _rapid;
        private BigInteger _fast;
        private BigInteger _standard;
        private BigInteger _slow;

        public BigInteger Rapid { get => _rapid / BigInteger.Pow(10, 9); set => _rapid = value; }
        public BigInteger Fast { get => _fast / BigInteger.Pow(10, 9); set => _fast = value; }
        public BigInteger Standard { get => _standard / BigInteger.Pow(10, 9); set => _standard = value; }
        public BigInteger Slow { get => _slow / BigInteger.Pow(10, 9); set => _slow = value; }
    }

    public class EthGasResponse
    {
        public Gas? Data { get; set; }
    }
}