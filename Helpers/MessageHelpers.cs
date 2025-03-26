using DSharpPlus.Entities;

namespace CountingBot.Helpers
{
    /// <summary>
    /// Presets for embeded response messages
    /// </summary>
    public static class MessageHelpers
    {
        public static DiscordEmbed GenericSuccessEmbed(string title, string message) =>
            GenericEmbed(title, message, "#00ff00");

        public static DiscordEmbed GenericErrorEmbed(string message, string title = "Error") =>
            GenericEmbed(title, message, "#ff0000");

        public static DiscordEmbed GenericEmbed(string title, string message, string color = "#5865f2") => new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithColor(new DiscordColor(color))
                .WithDescription(message)
                .WithTimestamp(DateTime.UtcNow)
                .Build();

        public static DiscordEmbed GenericUpdateEmbed(string title, string? extra, string color = "#00ffff") => new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithColor(new DiscordColor(color))
                .WithTimestamp(DateTime.UtcNow)
                .AddField("Updated To:" , $"```{extra}```")
                .Build();
    };
}

