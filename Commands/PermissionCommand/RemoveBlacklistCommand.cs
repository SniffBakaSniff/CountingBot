using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("remove_blacklist")]
        [Description("Removes a user or role from the blacklist for a command.")]
        [PermissionCheck("remove_blacklist")]
        public async Task RemoveBlacklistEntryAsync(
            CommandContext ctx,
            [Description("Command to modify blacklist for.")] PermissionKey Command,
            [Description("User to remove from blacklist (mention or ID).")]
                DiscordUser? user = null,
            [Description("Role to remove from blacklist (mention or ID).")] DiscordRole? role = null
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

            await _guildSettingsService.RemoveBlacklistEntryAsync(
                ctx.Guild!.Id,
                Command.ToString(),
                userId,
                roleId
            );

            string titleKey = "CommandBlacklistRemovedTitle";
            string descriptionKey = "CommandBlacklistRemovedDescription";

            string title = await _languageService.GetLocalizedStringAsync(titleKey, lang);
            string description = await _languageService.GetLocalizedStringAsync(
                descriptionKey,
                lang
            );
            description = string.Format(description, Command.ToString());

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Color = DiscordColor.Green,
                Description = description,
            };

            string removedUserField = await _languageService.GetLocalizedStringAsync(
                "CommandBlacklistRemovedUserField",
                lang
            );
            string removedRoleField = await _languageService.GetLocalizedStringAsync(
                "CommandBlacklistRemovedRoleField",
                lang
            );

            if (userId.HasValue)
                embed.AddField(removedUserField, $"<@{userId}>", true);
            if (roleId.HasValue)
                embed.AddField(removedRoleField, $"<@&{roleId}>", true);

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
