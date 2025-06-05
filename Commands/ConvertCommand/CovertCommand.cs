using System.ComponentModel;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("convert")]
        [AllowDMUsage]
        [Description("Converts a number from one base to another (e.g. hex to binary).")]
        public async Task ConvertCommandAsync(
            CommandContext ctx,
            [Description("The number to convert.")] string number,
            [Description("The base the input number is currently in.")] NumberSystem fromBase,
            [Description("The base to convert the number into.")] NumberSystem toBase
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id)
                ?? "en";

            // Gets the error messages
            var ConvertErrorInvalidFormat = await _languageService.GetLocalizedStringAsync(
                "ConvertErrorInvalidFormat",
                lang
            );
            var ConvertErrorGeneral = await _languageService.GetLocalizedStringAsync(
                "ConvertErrorGeneral",
                lang
            );
            try
            {
                // Gets the embed messages
                var title = await _languageService.GetLocalizedStringAsync("ConvertTitle", lang);
                var description = await _languageService.GetLocalizedStringAsync(
                    "ConvertDescription",
                    lang
                );
                var InputFieldTitle = await _languageService.GetLocalizedStringAsync(
                    "ConvertInputField",
                    lang
                );
                var OutputFieldTitle = await _languageService.GetLocalizedStringAsync(
                    "ConvertOutputField",
                    lang
                );

                int decimalValue = Convert.ToInt32(number, (int)fromBase);
                string converted = Convert.ToString(decimalValue, (int)toBase).ToUpper();

                // Main embed response
                var embed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Gray)
                    .WithDescription(description)
                    .AddField(InputFieldTitle, $"`{number}` (Base {(int)fromBase})", true)
                    .AddField(OutputFieldTitle, $"`{converted}` (Base {(int)toBase})", true)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                // Sends the reponse message
                await ctx.RespondAsync(
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(embed)
                        .AsEphemeral(false)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                "translate_ConvertTitle_ConvertDescription",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
            }
            catch (FormatException)
            {
                await ctx.RespondAsync(ConvertErrorInvalidFormat);
            }
            catch (Exception)
            {
                await ctx.RespondAsync(ConvertErrorGeneral);
            }
        }
    }
}
