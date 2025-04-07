using DSharpPlus.Commands;
using DSharpPlus.Entities;
using NCalc;
using System;
using System.Threading.Tasks;
using CountingBot.Services;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("calculate")]
        public async Task EvaluateExpressionCommandAsync(CommandContext ctx, string equation)
        {
            string input = equation.Trim();

            string lang = await _userInformationService.GetUserPreferredLanguageAsync(ctx.User.Id) 
                        ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(ctx.Guild!.Id) ?? "en";

            if (string.IsNullOrEmpty(input))
            {
                string emptyExprMsg = await _languageService.GetLocalizedStringAsync("EmptyExpressionMessage", lang);
                await ctx.RespondAsync(emptyExprMsg);
                return;
            }

            try
            {
                var expression = new Expression(input);
                var evaluation = expression.Evaluate();

                string title = await _languageService.GetLocalizedStringAsync("MathResultTitle", lang);
                string titleKey = "MathResultTitle";
                string footerTemplate = await _languageService.GetLocalizedStringAsync("MathResultFooter", lang);
                string footer = string.Format(footerTemplate, ctx.User.Username);
                string footerKey = "MathResultFooter";
                string descriptionTemplate = await _languageService.GetLocalizedStringAsync("MathResultDescription", lang);
                string descriptionKey = "MathResultDescription";
                string description = string.Format(descriptionTemplate, input, evaluation);

                var resultEmbed = new DiscordEmbedBuilder()
                    .WithTitle(title)
                    .WithColor(DiscordColor.Green)
                    .WithFooter(footer, ctx.User.AvatarUrl)
                    .WithTimestamp(DateTime.Now);

                if (input.Length > 100)
                {
                    resultEmbed.AddField("Equation", input);
                    resultEmbed.WithDescription($"**Result:**\n**{evaluation}**");
                }
                else
                {
                    resultEmbed.WithDescription(description);
                }

                await ctx.RespondAsync(new DiscordMessageBuilder().AddEmbed(resultEmbed).AddComponents(
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"translate_{titleKey}_{descriptionKey}_{footerKey}", DiscordEmoji.FromUnicode("🌐"))
                ));
            }
            catch (Exception ex)
            {
                string errorTitle = await _languageService.GetLocalizedStringAsync("GenericErrorTitle", lang);
                string errorTemplate = await _languageService.GetLocalizedStringAsync("GenericErrorMessage", lang);
                string errorMessage = string.Format(errorTemplate, ex.Message);

                var errorEmbed = new DiscordEmbedBuilder()
                    .WithTitle(errorTitle)
                    .WithDescription($"There was an issue evaluating the expression `{input}`.\n{errorMessage}")
                    .WithColor(DiscordColor.Red)
                    .Build();

                await ctx.RespondAsync(embed: errorEmbed);
            }
        }
    }
}
