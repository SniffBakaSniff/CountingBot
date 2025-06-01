using System.ComponentModel;

namespace CountingBot.Features.Commands
{
    [Description("Achievement types available in the achievements command")]
    public enum AchievementType
    {
        [Description("Show all achievements")]
        All,

        [Description("Achievements based on counting milestones")]
        Milestone,

        [Description("Achievements based on counting skills")]
        Skill,

        [Description("Achievements based on collecting items")]
        Collection,

        [Description("Achievements based on time and activity")]
        TimeBased,
    }
}
