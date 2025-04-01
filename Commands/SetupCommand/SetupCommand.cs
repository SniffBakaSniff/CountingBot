using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {

        [Command("setup")]
        public async Task SetupAsync(CommandContext ctx, NumberSystem? type, DiscordChannel? channel)
        {

            var name = channel!.Name;

            if (type is null || channel is null)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("Invalid Input")
                    .WithDescription("You must provide both a number system and a channel.")
                    .WithColor(DiscordColor.Red);

                await ctx.RespondAsync(embed);
                return;
            }

            if (channel.Type != DiscordChannelType.Text)
            {
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("Invalid Channel")
                    .WithDescription("The specified channel must be a text channel.")
                    .WithColor(DiscordColor.Red);

                await ctx.RespondAsync(embed);
                return;
            }

            int baseValue = (int)type.Value;

            await _guildSettingsService.SetCountingChannel(ctx.Guild!.Id, channel.Id, baseValue, name);
            
            var successEmbed = new DiscordEmbedBuilder()
                .WithTitle("Setup Complete")
                .WithDescription($"The counting channel has been set to {channel.Mention} with a base of {baseValue}.")
                .WithColor(DiscordColor.Green);


            await ctx.RespondAsync(successEmbed);
        }
    }

    public enum NumberSystem
    {
        [ChoiceDisplayName("Binary (Base-2)")]
        Binary = 2,

        [ChoiceDisplayName("Octal (Base-8)")]
        Octal = 8,
        
        [ChoiceDisplayName("Decimal (Base-10)")]
        Decimal = 10,

        [ChoiceDisplayName("Hexadecimal (Base-16)")]
        Hexadecimal = 16
    }

}