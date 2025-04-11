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
        public Dictionary<string, CommandPermissionData> CommandPermissions { get; set; } = [];

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public CommandPermissionData GetOrCreateCommandPermissionData(string permissionKey)
        {
            if (!CommandPermissions.TryGetValue(permissionKey, out var permissionData))
            {
                permissionData = new CommandPermissionData();
                CommandPermissions[permissionKey] = permissionData;
            }
            return permissionData;
        }
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
        public byte[]? RowVersion { get; set; }
    }

    public class CommandPermissionData
    {
        public bool? Enabled { get; set; }
        public List<ulong> Users { get; set; } = new List<ulong>();
        public List<ulong> Roles { get; set; } = new List<ulong>();
        public List<ulong> BlacklistedUsers { get; set; } = new List<ulong>();
        public List<ulong> BlacklistedRoles { get; set; } = new List<ulong>();
    }
}
