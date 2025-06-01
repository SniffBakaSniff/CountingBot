using CountingBot.Database;
using CountingBot.Features.Attributes;
using CountingBot.Features.Commands;
using CountingBot.Listeners;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

namespace CountingBot
{
    static class Program
    {
        public static readonly DateTime _botStartTime = DateTime.UtcNow;
        private static readonly string SeqServerUrl = "http://localhost:534"; // Optional
        private static readonly string LogFilePath = "Data/logs/log.txt";

        public static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("Arguments detected. Bot will not start.");
                return;
            }

            ConfigureSerilog();
            DiscordClient? client = null;

            try
            {
                Log.Information("Starting CountingBot...");
                var discordToken = ValidateDiscordToken();

                var builder = CreateDiscordClientBuilder(discordToken);
                var (dbContext, services) = InitializeServices(builder);
                var handlers = InitializeHandlers(services);

                ConfigureEventHandlers(builder, handlers);
                ConfigureCommands(builder);
                ConfigureLogging(builder);

                client = builder.Build();

                await PerformHealthCheck(dbContext);

                await StartBot(client);
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
                await HandleShutdown(client);
            }
        }

        private static void ConfigureSerilog()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithProcessId()
                .WriteTo.Console()
                .WriteTo.Seq(SeqServerUrl)
                .WriteTo.File(LogFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>
            {
                Log.Error(eventArgs.Exception, "Unobserved task exception occurred.");
                eventArgs.SetObserved();
            };
        }

        private static string ValidateDiscordToken()
        {
            string? discordToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                const string errorMessage =
                    "No discord token found. Please provide a token via the DISCORD_TOKEN environment variable.";
                Log.Fatal(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            return discordToken;
        }

        private static DiscordClientBuilder CreateDiscordClientBuilder(string token)
        {
            return DiscordClientBuilder.CreateDefault(
                token,
                TextCommandProcessor.RequiredIntents
                    | SlashCommandProcessor.RequiredIntents
                    | DiscordIntents.MessageContents
                    | DiscordIntents.GuildMembers
            );
        }

        private static (BotDbContext dbContext, ServiceContainer services) InitializeServices(
            DiscordClientBuilder builder
        )
        {
            var dbContext = new BotDbContext();

            // Create cache service with different expiration times for different data types
            var cacheService = new CacheService(30); // Default 30 minute expiration

            // Create achievement service
            var achievementService = new AchievementService(dbContext);

            builder.ConfigureServices(services =>
            {
                // Register database context as a singleton
                services.AddDbContext<BotDbContext>();

                // Register data access services with scoped lifetime
                services.AddScoped<IGuildSettingsService, GuildSettingsService>();
                services.AddScoped<IUserInformationService, UserInformationService>();
                services.AddScoped<ILanguageService, LanguageService>();
                services.AddScoped<ILeaderboardService, LeaderboardService>();

                // Register services
                services.AddSingleton<ICacheService>(cacheService);
                services.AddSingleton(achievementService);

                services.AddScoped<IPrefixResolver, CustomPrefixResolver>();
            });

            var guildSettingsService = new GuildSettingsService(cacheService);
            var userInformationService = new UserInformationService(
                achievementService,
                cacheService
            );
            var languageService = new LanguageService(cacheService);

            var services = new ServiceContainer
            {
                GuildSettingsService = guildSettingsService,
                UserInformationService = userInformationService,
                LanguageService = languageService,
                CacheService = cacheService,
            };

            return (dbContext, services);
        }

        private sealed class ServiceContainer
        {
            public required GuildSettingsService GuildSettingsService { get; init; }
            public required UserInformationService UserInformationService { get; init; }
            public required LanguageService LanguageService { get; init; }
            public required ICacheService CacheService { get; init; }
        }

        private static HandlerContainer InitializeHandlers(ServiceContainer services)
        {
            var messageHandler = new MessageHandler(
                services.GuildSettingsService,
                services.UserInformationService,
                services.LanguageService,
                services.CacheService
            );

            var buttonInteractionHandler = new ButtonInteractionListener(
                services.GuildSettingsService,
                services.UserInformationService,
                services.LanguageService,
                messageHandler
            );

            return new HandlerContainer
            {
                MessageHandler = messageHandler,
                ButtonInteractionHandler = buttonInteractionHandler,
                JoinEventHandler = new JoinEventsHandler(services.GuildSettingsService),
                LeaveEventHandler = new LeaveEventsHandler(services.GuildSettingsService),
                HelpComponentListener = new HelpComponentListener(
                    services.GuildSettingsService,
                    services.UserInformationService,
                    services.LanguageService
                ),
                AchievementComponentListener = new AchievementComponentListener(
                    services.GuildSettingsService,
                    services.UserInformationService,
                    services.LanguageService
                ),
            };
        }

        private sealed class HandlerContainer
        {
            public required MessageHandler MessageHandler { get; init; }
            public required ButtonInteractionListener ButtonInteractionHandler { get; init; }
            public required JoinEventsHandler JoinEventHandler { get; init; }
            public required LeaveEventsHandler LeaveEventHandler { get; init; }
            public required HelpComponentListener HelpComponentListener { get; init; }
            public required AchievementComponentListener AchievementComponentListener { get; init; }
        }

        private static void ConfigureEventHandlers(
            DiscordClientBuilder builder,
            HandlerContainer handlers
        )
        {
            builder.ConfigureEventHandlers(b =>
            {
                b.HandleComponentInteractionCreated(
                    handlers.ButtonInteractionHandler.HandleButtonInteraction
                );
                b.HandleComponentInteractionCreated(
                    handlers.HelpComponentListener.HandleComponentInteraction
                );
                b.HandleComponentInteractionCreated(
                    handlers.AchievementComponentListener.HandleComponentInteraction
                );
                b.HandleMessageCreated(handlers.MessageHandler.HandleMessage);
                b.HandleMessageDeleted(handlers.MessageHandler.HandleMessageDeleted);
                b.HandleGuildCreated(handlers.JoinEventHandler.HandleJoinEvents);
                b.HandleGuildDeleted(handlers.LeaveEventHandler.HandleLeaveEvents);
            });
        }

        private static void ConfigureCommands(DiscordClientBuilder builder)
        {
            builder.UseCommands(
                (_, extension) =>
                {
                    // Register global commands
                    extension.AddCommands([typeof(CommandsGroup), typeof(ConfigCommands)]);

                    // Guild-specific commands can be registered here if needed

                    var textCommandProcessor = new TextCommandProcessor(
                        new TextCommandConfiguration()
                    );
                    var slashCommandProcessor = new SlashCommandProcessor(
                        new SlashCommandConfiguration { UnconditionallyOverwriteCommands = true }
                    );

                    extension.AddProcessors(textCommandProcessor);
                    extension.AddProcessor(slashCommandProcessor);
                    extension.AddCheck<PermissionCheck>();
                },
                new CommandsConfiguration
                {
                    RegisterDefaultCommandProcessors = true,
                    UseDefaultCommandErrorHandler = false,
                }
            );
        }

        private static void ConfigureLogging(DiscordClientBuilder builder)
        {
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            });
        }

        private static async Task PerformHealthCheck(BotDbContext dbContext)
        {
            try
            {
                await dbContext.Database.CanConnectAsync();
                Log.Information("Database connection test successful");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to connect to database");
            }
        }

        private static async Task StartBot(DiscordClient client)
        {
            var status = new DiscordActivity("Counting Bot", DiscordActivityType.Custom);
            await client.ConnectAsync(status, DiscordUserStatus.Online);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            Log.Information("CountingBot is now running.");
            await Task.Delay(-1, cts.Token);
        }

        private static async Task HandleShutdown(DiscordClient? client)
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
