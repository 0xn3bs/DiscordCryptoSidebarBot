namespace DiscordCryptoSidebarBot
{
    public class BotSettings
    {
        public int Delay { get; set; } = 0;
        public int UpdateInterval { get; set; } = 30;
        public string ApiId { get; set; } = null!;
        public string BotToken { get; set; } = null!;
    }
}