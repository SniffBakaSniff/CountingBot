using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Serilog;

namespace CountingBot.Features.Attributes
{
    /// <summary>
    /// Attribute for checking command permissions in a Discord guild.
    /// This attribute works in conjunction with the PermissionCheck class to control access to commands.
    /// </summary>
    /// <remarks>
    /// Usage example:
    /// [PermissionCheck("ping_command", userBypass: true)]
    /// public async Task PingCommand(CommandContext ctx) { }
    ///
    /// For developer-only commands:
    /// [PermissionCheck("admin_command", developerOnly: true)]
    /// public async Task AdminCommand(CommandContext ctx) { }
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public class PermissionCheckAttribute : ContextCheckAttribute
    {
        /// <summary>
        /// The unique identifier for the permission being checked.
        /// This key is used to look up permission settings in the guild's configuration.
        /// </summary>
        public string PermissionKey { get; }

        /// <summary>
        /// If true and no permissions are set (empty users and roles lists), all users can use the command.
        /// If false, the command requires explicit permission assignment.
        /// </summary>
        public bool UserBypass { get; }

        /// <summary>
        /// If true, only the bot developer(s) can use this command.
        /// This overrides all other permission checks.
        /// </summary>
        public bool DeveloperOnly { get; }

        /// <summary>
        /// List of developer user IDs that can use developer-only commands.
        /// </summary>
        public static readonly ulong[] DeveloperIds = [509585751487545345];

        /// <summary>
        /// Initializes a new instance of the PermissionCheckAttribute.
        /// </summary>
        /// <param name="permissionKey">The unique identifier for this permission check</param>
        /// <param name="userBypass">Whether the command is publicly available when no permissions are set</param>
        /// <param name="developerOnly">Whether only bot developers can use this command</param>
        public PermissionCheckAttribute(
            string permissionKey,
            bool userBypass = false,
            bool developerOnly = false
        )
        {
            PermissionKey = permissionKey;
            UserBypass = userBypass;
            DeveloperOnly = developerOnly;
        }
    }

    /// <summary>
    /// Implements the permission check logic for the PermissionCheckAttribute.
    /// This class evaluates whether a user has permission to execute a command based on:
    /// - Administrator status (if bypass enabled)
    /// - Direct user permissions
    /// - Role-based permissions
    /// - Command enabled/disabled status
    /// </summary>
    public class PermissionCheck : IContextCheck<PermissionCheckAttribute>
    {
        /// <summary>
        /// Executes the permission check for a command.
        /// </summary>
        /// <param name="attribute">The PermissionCheckAttribute containing the check configuration</param>
        /// <param name="context">The command context containing information about the command execution</param>
        /// <returns>
        /// - null if the check passes and the command should execute
        /// - An error message string if the check fails
        /// </returns>
        public async ValueTask<string?> ExecuteCheckAsync(
            PermissionCheckAttribute attribute,
            CommandContext context
        )
        {
            var _guildSettingsService =
                context.ServiceProvider.GetRequiredService<IGuildSettingsService>();
            var _languageService = context.ServiceProvider.GetRequiredService<ILanguageService>();
            var _userInformationService =
                context.ServiceProvider.GetRequiredService<IUserInformationService>();
            var guildId = context.Guild!.Id;
            var userId = context.User.Id;

            // Get language for localization - prioritize user's language, then guild's, then default to "en"
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(userId)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                ?? "en";

            // Log the start of the permission check
            Log.Information(
                "Permission check started for command '{PermissionKey}' in guild {GuildId} by user {UserId}.",
                attribute.PermissionKey,
                guildId,
                userId
            );

            // Check if this is a developer-only command
            if (attribute.DeveloperOnly)
            {
                // Check if the user is a developer
                if (PermissionCheckAttribute.DeveloperIds.Contains(userId))
                {
                    Log.Information(
                        "User {UserId} is a developer. Allowing access to developer-only command '{PermissionKey}'.",
                        userId,
                        attribute.PermissionKey
                    );
                    return null;
                }
                else
                {
                    Log.Warning(
                        "User {UserId} attempted to use developer-only command '{PermissionKey}' but is not a developer.",
                        userId,
                        attribute.PermissionKey
                    );

                    string devOnlyTitleKey = "DeveloperOnlyTitle";
                    string devOnlyMessageKey = "DeveloperOnlyMessage";
                    string devOnlyMessage =
                        await _languageService.GetLocalizedStringAsync(devOnlyMessageKey, lang)
                        ?? "This command is only available to bot developers.";

                    var devOnlyResponse = new DiscordInteractionResponseBuilder()
                        .WithContent(devOnlyMessage)
                        .AsEphemeral(true)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_{devOnlyTitleKey}_{devOnlyMessageKey}",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        );
                    await context.RespondAsync(devOnlyResponse);

                    return "This command is only available to bot developers.";
                }
            }

            // Check administrator bypass
            if (context.Member!.Permissions.HasPermission(DiscordPermission.Administrator))
            {
                Log.Information(
                    "User {UserId} has Administrator permission. Bypassing permission check for command '{PermissionKey}'.",
                    userId,
                    attribute.PermissionKey
                );
                return null;
            }

            // Get guild settings and command permissions
            var settings = await _guildSettingsService.GetOrCreateGuildSettingsAsync(guildId);
            var permissions = settings.GetOrCreateCommandPermissionData(attribute.PermissionKey);

            // Check if user is blacklisted
            if (permissions.BlacklistedUsers.Contains(userId))
            {
                Log.Information(
                    "User {UserId} is blacklisted from using command '{PermissionKey}' in guild {GuildId}.",
                    userId,
                    attribute.PermissionKey,
                    guildId
                );

                string userBlacklistTitleKey = "UserBlacklistedTitle";
                string userBlacklistMessageKey = "UserBlacklistedMessage";
                string userBlacklistMessage =
                    await _languageService.GetLocalizedStringAsync(userBlacklistMessageKey, lang)
                    ?? "You are blacklisted from using this command.";

                var blacklistResponse = new DiscordInteractionResponseBuilder()
                    .WithContent(userBlacklistMessage)
                    .AsEphemeral(true)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_{userBlacklistTitleKey}_{userBlacklistMessageKey}",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    );
                await context.RespondAsync(blacklistResponse);

                return "User is blacklisted from using this command.";
            }

            // Check if any of user's roles are blacklisted
            var userRoles = context.Member!.Roles.Select(r => r.Id);
            if (userRoles.Any(permissions.BlacklistedRoles.Contains))
            {
                Log.Information(
                    "User {UserId} has a blacklisted role for command '{PermissionKey}' in guild {GuildId}.",
                    userId,
                    attribute.PermissionKey,
                    guildId
                );

                string roleBlacklistTitleKey = "RoleBlacklistedTitle";
                string roleBlacklistMessageKey = "RoleBlacklistedMessage";
                string roleBlacklistMessage =
                    await _languageService.GetLocalizedStringAsync(roleBlacklistMessageKey, lang)
                    ?? "One of your roles is blacklisted from using this command.";

                var roleBlacklistResponse = new DiscordInteractionResponseBuilder()
                    .WithContent(roleBlacklistMessage)
                    .AsEphemeral(true)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_{roleBlacklistTitleKey}_{roleBlacklistMessageKey}",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    );
                await context.RespondAsync(roleBlacklistResponse);

                return "User has a blacklisted role.";
            }

            // Check if command can be used when no permissions are set
            if (
                permissions.Users.Count is 0
                && permissions.Roles.Count is 0
                && attribute.UserBypass
            )
            {
                return null;
            }

            // Check if command permission check is explicitly disabled
            if (permissions.Enabled.HasValue && !permissions.Enabled.Value)
            {
                Log.Information(
                    "Command '{PermissionKey}' is disabled for guild {GuildId}.",
                    attribute.PermissionKey,
                    guildId
                );
                await context.RespondAsync("This command has been disabled.");
                return null;
            }

            // Check user-specific permissions
            if (permissions.Users!.Contains(userId))
            {
                Log.Information(
                    "User {UserId} has direct permission for command '{PermissionKey}' in guild {GuildId}.",
                    userId,
                    attribute.PermissionKey,
                    guildId
                );
                return null;
            }

            // Check role-based permissions
            if (userRoles.Any(permissions.Roles.Contains))
            {
                Log.Information(
                    "User {UserId} has required role permission for command '{PermissionKey}' in guild {GuildId}.",
                    userId,
                    attribute.PermissionKey,
                    guildId
                );
                return null;
            }

            // Permission check failed
            Log.Warning(
                "User {UserId} does not have permission to use command '{PermissionKey}' in guild {GuildId}.",
                userId,
                attribute.PermissionKey,
                guildId
            );

            string noPermTitleKey = "NoPermissionTitle";
            string noPermMessageKey = "NoPermissionMessage";
            string noPermMessage =
                await _languageService.GetLocalizedStringAsync(noPermMessageKey, lang)
                ?? "You do not have permission to run this command.";

            var noPermissionResponse = new DiscordInteractionResponseBuilder()
                .WithContent(noPermMessage)
                .AsEphemeral(true)
                .AddComponents(
                    new DiscordButtonComponent(
                        DiscordButtonStyle.Secondary,
                        $"translate_{noPermTitleKey}_{noPermMessageKey}",
                        DiscordEmoji.FromUnicode("🌐")
                    )
                );
            await context.RespondAsync(noPermissionResponse);

            return "You do not have permission to run this command.";
        }
    }
}
