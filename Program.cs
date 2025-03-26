using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;

using CountingBot.Features;
using CountingBot.Listeners;
using CountingBot.Services.Database;
using CountingBot.Services;
using CountingBot.Features.ConfigCommands;
using DSharpPlus.EventArgs;

namespace CountingBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            string? discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                Console.WriteLine("Error: No discord token found. Please provide a token via the DISCORD_TOKEN environment variable.");
                Environment.Exit(1);
            }

            DiscordClientBuilder builder = DiscordClientBuilder
                .CreateDefault(discordToken, TextCommandProcessor.RequiredIntents | SlashCommandProcessor.RequiredIntents | DiscordIntents.MessageContents | DiscordIntents.GuildMembers)
                .ConfigureServices(services => 
                {
                    services.AddDbContext<BotDbContext>();
                    services.AddScoped<IPrefixResolver, CustomPrefixResolver>();
                    services.AddScoped<IGuildSettingsService, GuildSettingsService>();
                    services.AddScoped<IStringInterpolatorService, StringInterpolatorService>();
                });


            var buttonInteractionHandler = new ButtonInteractionListener();
            var onMessageListener = new OnMessageListener();

            builder.ConfigureEventHandlers(b =>
            {
                b.HandleComponentInteractionCreated(buttonInteractionHandler.HandleButtonInteraction);
                b.HandleMessageCreated(onMessageListener.HandleMessage);
            });

            // Use the commands extension
            builder.UseCommands
            (
                // we register our commands here
                (ServiceProvider, extension) =>
                {
                    extension.AddCommands([
                        typeof(PingCommand),
                        typeof(CommandsGroup)]);
                    
                    TextCommandProcessor textCommandProcessor = new(new TextCommandConfiguration
                    {
                       // PrefixResolver = new DefaultPrefixResolver(true, "?", ".").ResolvePrefixAsync
                    });

                    // Add text commands with a custom prefix (?ping)
                    extension.AddProcessors(textCommandProcessor);

                    extension.CommandErrored += EventHandlers.CommandErrored;
                },

                
                new CommandsConfiguration()
                {
                    DebugGuildId = 1345544197310255134,
                    RegisterDefaultCommandProcessors = true,
                    UseDefaultCommandErrorHandler = false
                }
            );

            DiscordClient client = builder.Build();

            DiscordActivity status = new("Counting Bot", DiscordActivityType.Custom);

            await client.ConnectAsync(status, DiscordUserStatus.Online);

            await Task.Delay(-1);
        }
    }
}
