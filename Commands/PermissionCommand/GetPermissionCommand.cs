using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("get_permission")]
        [Description(
            "Shows the current permission settings for a command, including allowed and blacklisted users/roles."
        )]
        [PermissionCheck("get_permission")]
        public async Task GetCommandPermissionAsync(
            CommandContext ctx,
            [Description("Command to view permissions for.")] PermissionKey Command
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            var permissionData = await _guildSettingsService.GetCommandPermissionsAsync(
                ctx.Guild!.Id,
                Command.ToString()
            );

            string titleKey = "CommandPermissionsViewTitle";
            string descriptionKey = "CommandPermissionsViewDescription";

            string title = await _languageService.GetLocalizedStringAsync(titleKey, lang);
            string description = await _languageService.GetLocalizedStringAsync(
                descriptionKey,
                lang
            );
            description = string.Format(description, Command.ToString());

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Color = DiscordColor.Blurple,
                Description = description,
            };

            string enabledStatusField = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsEnabledStatus",
                lang
            );
            string allowedUsersField = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsAllowedUsers",
                lang
            );
            string allowedRolesField = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsAllowedRoles",
                lang
            );
            string blacklistedUsersField = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsBlacklistedUsers",
                lang
            );
            string blacklistedRolesField = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsBlacklistedRoles",
                lang
            );
            string noneText = await _languageService.GetLocalizedStringAsync(
                "CommandPermissionsNone",
                lang
            );

            if (permissionData == null)
            {
                embed.AddField(enabledStatusField, "true", true);
                embed.AddField(allowedUsersField, noneText, true);
                embed.AddField(allowedRolesField, noneText, true);
                embed.AddField(blacklistedUsersField, noneText, true);
                embed.AddField(blacklistedRolesField, noneText, true);
            }
            else
            {
                embed.AddField(
                    enabledStatusField,
                    (permissionData.Enabled ?? true).ToString(),
                    true
                );

                string allowedUsers =
                    permissionData.Users.Count > 0
                        ? string.Join(", ", permissionData.Users.Select(u => $"<@{u}>"))
                        : noneText;
                embed.AddField(allowedUsersField, allowedUsers, true);

                string allowedRoles =
                    permissionData.Roles.Count > 0
                        ? string.Join(", ", permissionData.Roles.Select(r => $"<@&{r}>"))
                        : noneText;
                embed.AddField(allowedRolesField, allowedRoles, true);

                string blacklistedUsers =
                    permissionData.BlacklistedUsers.Count > 0
                        ? string.Join(", ", permissionData.BlacklistedUsers.Select(u => $"<@{u}>"))
                        : noneText;
                embed.AddField(blacklistedUsersField, blacklistedUsers, true);

                string blacklistedRoles =
                    permissionData.BlacklistedRoles.Count > 0
                        ? string.Join(", ", permissionData.BlacklistedRoles.Select(r => $"<@&{r}>"))
                        : noneText;
                embed.AddField(blacklistedRolesField, blacklistedRoles, true);
            }

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
