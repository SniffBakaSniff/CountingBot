using System.ComponentModel.DataAnnotations;

public class GuildSettings
{
    [Key]
    public ulong GuildId { get; set; }
    public string Prefix { get; set; } = "!";
}

public class ChannelSettings
{
    [Key]
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public string? Name { get; set; }
    public int Base { get; set; } = 10;
    public int CurrentCount { get; set; } = 0;
}