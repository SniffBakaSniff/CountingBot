using System.ComponentModel.DataAnnotations;

namespace CountingBot.Database.Models
    {
    public class GuildSettings
    {
        [Key]
        public ulong GuildId { get; set; }
        public string Prefix { get; set; } = "!";
        public bool MathEnabled { get; set; } = false;
        public string PreferredLanguage { get; set; } = "en";

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }

    public class ChannelSettings
    {
        [Key]
        public ulong ChannelId { get; set; }
        public ulong GuildId { get; set; }
        public string? Name { get; set; }
        public int Base { get; set; } = 10;
        public int CurrentCount { get; set; } = 0;
        public int Highescore { get; set; } = 0;

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}