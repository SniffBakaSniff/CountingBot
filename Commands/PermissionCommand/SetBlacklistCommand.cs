using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("set_blacklist")]
        [Description("Blacklists a user or role from using a specific command.")]
        [PermissionCheck("set_blacklist")]
        public async Task SetBlacklistAsync(
            CommandContext ctx,
            [Description("Command to modify blacklist for.")] PermissionKey Command,
            [Description("User to blacklist from using the command (mention or ID).")]
                DiscordUser? user = null,
            [Description("Role to blacklist from using the command (mention or ID).")]
                DiscordRole? role = null
        )
        {
            ulong? userId = null;
            ulong? roleId = null;

            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            if (user is not null)
            {
                userId = user.Id;
            }

            if (role is not null)
            {
                roleId = role.Id;
            }

            await _guildSettingsService.SetBlacklistAsync(
                ctx.Guild!.Id,
                Command.ToString(),
                userId,
                roleId
            );

            string titleKey = "CommandBlacklistUpdatedTitle";
            string descriptionKey = "CommandBlacklistUpdatedDescription";

            string title =
                await _languageService.GetLocalizedStringAsync(titleKey, lang)
                ?? "Command Blacklist Updated";
            string description =
                await _languageService.GetLocalizedStringAsync(descriptionKey, lang)
                ?? "Blacklist for command '{0}' has been updated.";
            description = string.Format(description, Command.ToString());

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Color = DiscordColor.Red,
                Description = description,
            };

            string blacklistedUserField =
                await _languageService.GetLocalizedStringAsync("CommandBlacklistedUserField", lang)
                ?? "Blacklisted User";
            string blacklistedRoleField =
                await _languageService.GetLocalizedStringAsync("CommandBlacklistedRoleField", lang)
                ?? "Blacklisted Role";

            if (userId.HasValue)
                embed.AddField(blacklistedUserField, $"<@{userId}>", true);
            if (roleId.HasValue)
                embed.AddField(blacklistedRoleField, $"<@&{roleId}>", true);

            var response = new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(
                    new DiscordButtonComponent(
                        DiscordButtonStyle.Secondary,
                        $"translate_{titleKey}_{descriptionKey}",
                        DiscordEmoji.FromUnicode("🌐")
                    )
                );

            await ctx.RespondAsync(response);
        }
    }
}
