using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;

namespace CountingBot.Features.Commands
{
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
        stats_command,

        [ChoiceDisplayName("Set Blacklist Command")]
        set_blacklist,

        [ChoiceDisplayName("Get Permission Command")]
        get_permission,

        [ChoiceDisplayName("Remove Permission Command")]
        remove_permission,

        [ChoiceDisplayName("Remove Blacklist Command")]
        remove_blacklist,

        [ChoiceDisplayName("Help Command")]
        help_command,
    }
}
