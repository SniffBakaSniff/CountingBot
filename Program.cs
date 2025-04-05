using Serilog;
using Serilog.Events;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;

using CountingBot.Listeners;
using CountingBot.Services;
using CountingBot.Services.Database;
using CountingBot.Features.Commands;
using CountingBot.Database;
using Microsoft.EntityFrameworkCore;


namespace CountingBot
{
    static class Program
    {
        public static readonly DateTime _botStartTime = DateTime.UtcNow;
        public static async Task Main(string[] args)
        {

            if (args.Length > 0)
            {
                // If there are any arguments, stop the bot from running
                // So i can run dotnet ef without starting the bot
                Console.WriteLine("Arguments detected. Bot will not start.");
                return;
            }

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
                        services.AddScoped<IUserInformationService, UserInformationService>();
                        services.AddScoped<IStringInterpolatorService, StringInterpolatorService>();
                        services.AddScoped<ILanguageService, LanguageService>();
                        services.AddScoped<ILeaderboardService, LeaderboardService>();
                    });

                var buttonInteractionHandler = new ButtonInteractionListener(new GuildSettingsService(), new UserInformationService(), new LanguageService());
                var messageHandler = new MessageHandler(new GuildSettingsService(), new UserInformationService(), new LanguageService());
                var joinEventHandler = new JoinEventsHandler(new GuildSettingsService());
                var leaveEventHandler = new LeaveEventsHandler(new GuildSettingsService());

                builder.ConfigureEventHandlers(b =>
                {
                    b.HandleComponentInteractionCreated(buttonInteractionHandler.HandleButtonInteraction);
                    b.HandleMessageCreated(messageHandler.HandleMessage);
                    b.HandleMessageDeleted(messageHandler.HandleMessageDeleted);
                    b.HandleGuildCreated(joinEventHandler.HandleJoinEvents);
                    b.HandleGuildDeleted(leaveEventHandler.HandleLeaveEvents);
                });

                builder.UseCommands(
                    (ServiceProvider, extension) =>
                    {
                        extension.AddCommands(
                        [
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
                        //DebugGuildId = 1345544197310255134,
                        RegisterDefaultCommandProcessors = true,
                        UseDefaultCommandErrorHandler = false
                    });

                client = builder.Build();

                DiscordActivity status = new("Counting Bot", DiscordActivityType.Custom);

                await client.ConnectAsync(status, DiscordUserStatus.Online);

                using (var cts = new CancellationTokenSource())
                {
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        cts.Cancel();
                    };

                    Log.Information("CountingBot is now running.");
                    await Task.Delay(-1, cts.Token);
                }
            }
            catch (OperationCanceledException ex)
            {
                Log.Information(ex, "Shutdown signal received.");
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
                await Log.CloseAndFlushAsync();
            }
        }
    }
}