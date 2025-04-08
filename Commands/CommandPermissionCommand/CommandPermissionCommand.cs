using System.ComponentModel;
using CountingBot.Features.Attributes;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("set_permission")]
        [PermissionCheck("set_permission",administratorBypass: true)]
        public async Task SetCommandPermissionAsync(CommandContext ctx,
            [Description("Command to modify permissions for.")] PermissionKey Command,
            [Description("User allowed to use the command (mention or ID).")] DiscordUser? user = null,
            [Description("Role allowed to use the command (mention or ID).")] DiscordRole? role = null,
            [Description("Whether the command is enabled. (true/false)")] bool? enabled = true)
        {
            ulong? userId = null;
            ulong? roleId = null;

            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id) 
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id) ?? "en";

            if (user is not null)
            {
                userId = user.Id;
            }

            if (role is not null)
            {
                roleId = role.Id;
            }

            await _guildSettingsService.SetPermissionsAsync(ctx.Guild!.Id, Command.ToString(), enabled, userId, roleId);

            string title = await _languageService.GetLocalizedStringAsync("CommandPermissionsUpdatedTitle", lang);
            string description = await _languageService.GetLocalizedStringAsync("CommandPermissionsUpdatedDescription", lang);
            description = string.Format(description, Command.ToString());
            
            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Color = DiscordColor.Blurple,
                Description = description
            };

            string enabledField = await _languageService.GetLocalizedStringAsync("CommandPermissionsEnabledField", lang);
            string allowedUserField = await _languageService.GetLocalizedStringAsync("CommandPermissionsAllowedUserField", lang);
            string allowedRoleField = await _languageService.GetLocalizedStringAsync("CommandPermissionsAllowedRoleField", lang);

            if (enabled.HasValue)
                embed.AddField(enabledField, enabled.Value.ToString(), true);
            if (userId.HasValue)
                embed.AddField(allowedUserField, $"<@{userId}>", true);
            if (roleId.HasValue)
                embed.AddField(allowedRoleField, $"<@&{roleId}>", true);

            await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(embed).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, 
                    $"translate_CommandPermissionsUpdatedTitle_CommandPermissionsUpdatedDescription", DiscordEmoji.FromUnicode("🌐"))
                ));
        }
    }

    public enum PermissionKey
    {
        [ChoiceDisplayName("Achievements Command")]
        achievements_command,

        [ChoiceDisplayName("Calculate Command")]
        calculate_command,

        [ChoiceDisplayName("Setup Permission Command")]
        setup_permission_command,

        [ChoiceDisplayName("Config Command")]
        config_command,

        [ChoiceDisplayName("Leaderboard Command")]
        leaderboard_command,

        [ChoiceDisplayName("Ping Command")]
        ping_command,

        [ChoiceDisplayName("Prefix Command")]
        prefix_command,

        [ChoiceDisplayName("Profile Command")]
        profile_command,

        [ChoiceDisplayName("Set Count Command")]
        setcount_command,

        [ChoiceDisplayName("Setup Command")]
        setup_command,

        [ChoiceDisplayName("Stats Command")]
        stats_command
    }
}