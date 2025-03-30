using Serilog;
using Serilog.Events;
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

namespace CountingBot
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId()
                .WriteTo.Console()
                .WriteTo.Seq("http://localhost:5341") // Replace with your Seq server URL
                .CreateLogger();

            // Handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            {
                Log.Error(eventArgs.Exception, "Unobserved task exception occurred.");
                eventArgs.SetObserved();
            };

            DiscordClient? client = null;

            try
            {
                Log.Information("Starting CountingBot...");

                string? discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
                if (string.IsNullOrWhiteSpace(discordToken))
                {
                    Log.Error("No discord token found. Please provide a token via the DISCORD_TOKEN environment variable.");
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
                var messageHandler = new MessageHandler(new GuildSettingsService());

                builder.ConfigureEventHandlers(b =>
                {
                    b.HandleComponentInteractionCreated(buttonInteractionHandler.HandleButtonInteraction);
                    b.HandleMessageCreated(messageHandler.HandleMessage);
                });

                builder.UseCommands(
                    (ServiceProvider, extension) =>
                    {
                        extension.AddCommands(
                        [
                            typeof(PingCommand),
                            typeof(CommandsGroup)
                        ]);

                        TextCommandProcessor textCommandProcessor = new(new TextCommandConfiguration
                        {
                            // PrefixResolver = new DefaultPrefixResolver(true, "?", ".").ResolvePrefixAsync
                        });

                        extension.AddProcessors(textCommandProcessor);

                        extension.CommandErrored += EventHandlers.CommandErrored;
                    },
                    new CommandsConfiguration()
                    {
                        DebugGuildId = 1345544197310255134,
                        RegisterDefaultCommandProcessors = true,
                        UseDefaultCommandErrorHandler = false
                    });

                client = builder.Build();

                DiscordActivity status = new("Counting Bot", DiscordActivityType.Custom);

                await client.ConnectAsync(status, DiscordUserStatus.Online);

                var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                Log.Information("CountingBot is now running.");
                await Task.Delay(-1, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Shutdown signal received.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly.");
            }
            finally
            {
                if (client != null)
                {
                    try
                    {
                        await client.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while disconnecting the Discord client.");
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }
                Log.Warning("CountingBot is shutting down... closing and flushing logs.");
                Log.CloseAndFlush();
            }
        }
    }
}