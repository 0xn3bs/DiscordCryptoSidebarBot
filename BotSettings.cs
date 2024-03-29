﻿using System.IO;

namespace DiscordCryptoSidebarBot
{
    public class BotSettings
    {
        public int Delay { get; set; } = 0;
        public int UpdateInterval { get; set; } = 30;
        public string ApiId { get; set; } = null!;
        public string BotToken { get; set; } = null!;
        public string? GainRoleName { get; set; } = "gain";
        public string? LossRoleName { get; set; } = "loss";
        public string? CustomEndpoint { get; set; } = null!;
        public string? CustomTicker { get; set; } = null!;
        public string? CustomEmoji { get; set; } = null!;
        public string? CustomAvatar { get; set; } = null!;
        public string? ClusterPath { get; set; } = null!;
        public bool? ClusterLeader { get; set; } = null!;
        public string? ClusterName { get; set; } = null!;
    }
}