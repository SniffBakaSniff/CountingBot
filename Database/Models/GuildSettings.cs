using System.ComponentModel.DataAnnotations;

public class GuildSettings
{
    [Key]
    public ulong GuildId { get; set; }
    public string Prefix { get; set; } = "!";
}
