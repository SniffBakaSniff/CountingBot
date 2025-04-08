using System.Diagnostics.Eventing.Reader;
using CountingBot.Services.Database;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Serilog;


//TODO: Add blacklisting system
namespace CountingBot.Features.Attributes
{
    /// <summary>
    /// Attribute for checking permissions for a command
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class PermissionCheckAttribute : ContextCheckAttribute
    {
        public string PermissionKey { get; }
        public bool AdministratorBypass { get; }

        public bool UserBypass { get; }

        public PermissionCheckAttribute(string permissionKey, bool administratorBypass = false, bool userBypass = false)
        {
            PermissionKey = permissionKey;
            AdministratorBypass = administratorBypass;
            UserBypass = userBypass;
        }
    }

    /// <summary>
    /// Performs the permission check logic for the PermissionCheckAttribute
    /// </summary>
    /// <summary>
    /// Performs the permission check logic for the PermissionCheckAttribute
    /// </summary>
    public class PermissionCheck : IContextCheck<PermissionCheckAttribute>
    {
        public async ValueTask<string?> ExecuteCheckAsync(PermissionCheckAttribute attribute, CommandContext ctx)
        {
            var _guildSettingsService = ctx.ServiceProvider.GetRequiredService<IGuildSettingsService>();
            var guildId = ctx.Guild?.Id;
            var userId = ctx.User.Id;

            // Start logging
            Log.Information("Permission check started for command '{PermissionKey}' in guild {GuildId} by user {UserId}.", attribute.PermissionKey, guildId, userId);

            if (attribute.AdministratorBypass && ctx.Member!.Permissions.HasPermission(DiscordPermission.Administrator))
            {
                Log.Information("User {UserId} has Administrator permission. Bypassing permission check for command '{PermissionKey}'.", userId, attribute.PermissionKey);
                return null;
            }

            var settings = await _guildSettingsService.GetOrCreateGuildSettingsAsync(guildId!.Value);

            Log.Debug("Fetched settings for guild {GuildId}. Command permissions: {CommandPermissions}", guildId, settings.CommandPermissions);

            var permissions = settings.GetOrCreateCommandPermissionData(attribute.PermissionKey);

            if (permissions.Users.Count is 0 && permissions.Roles.Count is 0 && attribute.UserBypass)
            {
                return null;
            }

            if (permissions.Enabled.HasValue && !permissions.Enabled.Value)
            {
                Log.Information("Command '{PermissionKey}' is disabled for guild {GuildId}.", attribute.PermissionKey, guildId);
                await ctx.RespondAsync("This command has been disabled.");
                return null;
            }

            if (permissions.Users!.Contains(userId))
            {
                Log.Information("User {UserId} has direct permission for command '{PermissionKey}' in guild {GuildId}.", userId, attribute.PermissionKey, guildId);
                return null;
            }

            var userRoleIds = ctx.Member!.Roles.Select(r => r.Id);
            if (userRoleIds.Any(permissions.Roles.Contains))
            {
                Log.Information("User {UserId} has required role permission for command '{PermissionKey}' in guild {GuildId}.", userId, attribute.PermissionKey, guildId);
                return null; 
            }

            Log.Warning("User {UserId} does not have permission to use command '{PermissionKey}' in guild {GuildId}.", userId, attribute.PermissionKey, guildId);
            
            var message = new DiscordInteractionResponseBuilder().WithContent("You do not have permission to run this command.").AsEphemeral(true);
            await ctx.RespondAsync(message);
            
            return "You do not have permission to run this command.";
        }
    }
}
