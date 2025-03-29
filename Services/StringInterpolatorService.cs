using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace CountingBot.Services
{
    public interface IContext
    {
        DiscordUser User { get; }
        DiscordChannel Channel { get; }
        DiscordGuild Guild { get; }
        DiscordMember Member { get; }
        string CommandName { get; }
    }

    public class EventContext : IContext
    {
        public DiscordUser User { get; }
        public DiscordChannel Channel { get; }
        public DiscordGuild Guild { get; }
        public DiscordMember Member { get; }
        public string CommandName { get; } = string.Empty;

        public EventContext(CommandContext ctx)
        {
            User = ctx.User;
            Channel = ctx.Channel;
            Guild = ctx.Guild!;
            Member = ctx.Member!;
            CommandName = ctx.Command.Name;
        }
    }

    public class StringInterpolatorService : IStringInterpolatorService
    {
        public string Interpolate(string template, IContext ctx)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "{{username}}", ctx.User.Username },
                { "{{userid}}", ctx.User.Id.ToString() },
                { "{{channel}}", ctx.Channel.Name },
                { "{{server}}", ctx.Guild?.Name ?? "DM" },
                { "{{mention}}", ctx.User.Mention },
                { "{{nickname}}", ctx.Member?.Nickname ?? ctx.User.Username },
                { "{{useravatar}}", ctx.User.AvatarUrl },
                { "{{guildid}}", ctx.Guild?.Id.ToString() ?? "DM" },
                { "{{channelid}}", ctx.Channel.Id.ToString() },
                { "{{timestamp}}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
                { "{{command}}", ctx.CommandName },
            };

            foreach (var placeholder in placeholders)
            {
                template = template.Replace(placeholder.Key, placeholder.Value);
            }

            return template;
        }
    }

    public interface IStringInterpolatorService
    {
        string Interpolate(string template, IContext ctx);
    }
}