using DSharpPlus.Commands;
using DSharpPlus.Entities;
using NCalc;

namespace CountingBot.Features.Commands
{
    public partial class CommandsGroup
    {
        [Command("calculate")]
        public async Task EvaluateExpressionCommandAsync(CommandContext ctx, string equation)
        {
            string input = equation.Trim();

            if (string.IsNullOrEmpty(input))
            {
                await ctx.RespondAsync("Please provide a valid mathematical expression.");
                return;
            }

            try
            {
                var expression = new Expression(input); 
                var evaluation = expression.Evaluate(); 

                var resultEmbed = new DiscordEmbedBuilder()
                    .WithTitle("🧮 Math Result")
                    .WithColor(DiscordColor.Green)
                    .WithFooter($"Requested by {ctx.User.Username}", ctx.User.AvatarUrl)
                    .WithTimestamp(DateTime.Now);

                string equationFormatted = $"**Expression:**\n`{input}`\n";
                string resultFormatted = $"**Result:**\n**{evaluation}**";

                if (input.Length > 100)
                {
                    resultEmbed.AddField("Equation", input);
                }
                else
                {
                    resultEmbed.WithDescription($"{equationFormatted}{resultFormatted}");
                }

                await ctx.RespondAsync(embed: resultEmbed);
            }
            catch (Exception ex)
            {
                var errorEmbed = new DiscordEmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription($"There was an issue evaluating the expression `{input}`.\nError: {ex.Message}")
                    .WithColor(DiscordColor.Red)
                    .Build();

                await ctx.RespondAsync(embed: errorEmbed);
            }
        }
    }
}
