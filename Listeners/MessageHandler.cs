using CountingBot.Helpers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;
using System.Collections.Concurrent;

using CountingBot.Services.Database;
using NCalc;

namespace CountingBot.Listeners
{
    public class MessageHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _cooldowns = new();
        private readonly ConcurrentDictionary<(ulong GuildId, ulong ChannelId), (ulong? messageId, int)> _count = new();
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _channelSemaphores = new();
        private const int CooldownSeconds = 2;
        private DiscordEmoji? _correctEmoji;
        private DiscordEmoji? _wrongEmoji;

        public MessageHandler(IGuildSettingsService guildSettingsService, IUserInformationService userInformationService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
        }

        public async Task HandleMessageDeleted(DiscordClient client, MessageDeletedEventArgs e)
        {
            ulong channelId = e.Channel.Id;
            ulong guildId = e.Guild.Id;
            var (messageId, currentCount) = _count[(guildId, channelId)];

            if (e.Message.Id == messageId)
            {
                int baseValue = await _guildSettingsService.GetChannelBase(guildId, channelId);
                string parsedCurrentCount = Convert.ToString(currentCount, baseValue);

                var countResetEmbed = new DiscordEmbedBuilder()
                    .WithTitle("⚠️ Count Message Deleted!")
                    .WithDescription($"The latest count message was deleted! The current count is **{parsedCurrentCount}**.")
                    .WithColor(DiscordColor.Red)
                    .Build();

                await e.Channel.SendMessageAsync(embed: countResetEmbed);
            }
        }

        public async Task HandleMessage(DiscordClient client, MessageCreatedEventArgs e)
        {
            Log.Debug("Handling message from user {User} in channel {ChannelId}.", e.Author.Username, e.Channel.Id);

            if (e.Author.IsBot)
            {
                Log.Debug("Message ignored because it was sent by a bot.");
                return;
            }

            _correctEmoji ??= DiscordEmoji.FromName(client, ":white_check_mark:");
            _wrongEmoji ??= DiscordEmoji.FromName(client, ":x:");

            if (await _guildSettingsService.CheckIfCountingChannel(e.Guild.Id, e.Channel.Id))
            {
                int baseValue = await _guildSettingsService.GetChannelBase(e.Guild.Id, e.Channel.Id);
                _count[(e.Guild.Id, e.Channel.Id)] = (null, await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id));

                await ProcessCountingMessage(e, baseValue);
            }

            else
            {
                Log.Debug("Message received in a non-counting channel. Ignoring.");
            }
        }

        private SemaphoreSlim GetChannelSemaphore(ulong channelId)
        {
            return _channelSemaphores.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
        }

        private async Task ProcessCountingMessage(MessageCreatedEventArgs e, int baseValue)
        {
            ulong channelId = e.Channel.Id;
            var semaphore = GetChannelSemaphore(channelId);
            await semaphore.WaitAsync();

            try
            {
                var (messageId, currentCount) = _count[(e.Guild.Id, channelId)];
                var (success, parsedNumber) = await TryEvaluateExpressionAsync(e.Guild.Id, e.Message, baseValue);

                if (!success)
                {
                    Log.Debug("Message is not a valid math expression or number in base {Base} for channel {ChannelId}. Ignoring.", baseValue, channelId);
                    return;
                }

                Log.Debug("Parsed number: {ParsedNumber}, Expected: {Expected}", parsedNumber, currentCount + 1);

                if (IsUserOnCooldown(channelId, e.Author.Id))
                {
                    Log.Warning("User {User} is on cooldown in channel {ChannelId}.", e.Author.Username, channelId);
                    var embed = MessageHelpers.GenericErrorEmbed(
                        title: "Slowdown!",
                        message: "You are counting too fast! Please slow down."
                    );
                    var warningMessage = await e.Message.RespondAsync(embed);
                    _ = DeleteMessagesAsync(e.Message, warningMessage, 2500);
                    return;
                }

                if (parsedNumber == currentCount + 1)
                {
                    _count[(e.Guild.Id, e.Channel.Id)] = (e.Message.Id, parsedNumber);
                    AddUserToCooldown(channelId, e.Author.Id);
                    _ = RemoveCooldownAfterDelay(channelId, e.Author.Id);

                    Log.Information("Valid count in channel {ChannelId}: count updated to {NewCount}", channelId, parsedNumber);
                    _ = e.Message.CreateReactionAsync(_correctEmoji!);
                    _ = _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, parsedNumber);
                    _ = _userInformationService.UpdateUserCountAsync(e.Guild.Id, e.Author.Id, parsedNumber, true);
                }

                else
                {
                    var reviveEmbed = MessageHelpers.GenericErrorEmbed(
                        title: "Disaster Strikes! ⚠️", 
                        message: "Oh no! The count has fallen! ⏳ Time is running out—will someone step up and use a revive?"
                    );

                    var reviveMessage = await e.Message.RespondAsync(new DiscordMessageBuilder().AddEmbed(reviveEmbed).AddComponents(
                        new DiscordButtonComponent(DiscordButtonStyle.Primary, "use_revive", "⚡ Revive the Count!")
                    ));

                    await Task.Delay(30000);

                    try
                    {
                        var checkMessage = await e.Channel.GetMessageAsync(reviveMessage.Id);
                        
                        if (checkMessage is not null)
                        {
                            var expiredEmbed = MessageHelpers.GenericErrorEmbed(
                                title: "⏳ Revive Request Expired!", 
                                message: "Looks like no one acted in time... The count is lost."
                            );

                            var builder = new DiscordMessageBuilder()
                                .AddEmbed(expiredEmbed);

                            builder.ClearComponents();

                            await reviveMessage.ModifyAsync(builder).ConfigureAwait(false);
                            _ = e.Message.CreateReactionAsync(_wrongEmoji!);
                            _ = _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, 0);
                            _ = _userInformationService.UpdateUserCountAsync(e.Guild.Id, e.Author.Id, parsedNumber, false);
                            _count[(e.Guild.Id, e.Channel.Id)] = (messageId, await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id));
                            ResetCooldowns(channelId);
                        }
                        return;
                    }
                    catch (DSharpPlus.Exceptions.NotFoundException)
                    {
                        // The message was deleted, so do nothing
                    }

                    Log.Warning("Invalid count in channel {ChannelId}. Expected {Expected}, but got {ParsedNumber}. Resetting state.", channelId, currentCount + 1, parsedNumber);
                }
            }

            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(bool Success, int Result)> TryEvaluateExpressionAsync(ulong guildId, DiscordMessage message, int baseValue)
        {
            int result = 0;
            string input = message.Content;
            try
            {
                bool containsMath = input.Contains('+') || input.Contains('-') || input.Contains('*') ||
                                    input.Contains('/') || input.Contains('(') || input.Contains(')') || input.Contains('^');

                if (containsMath)
                {
                    if (!await _guildSettingsService.GetMathEnabledAsync(guildId))
                    {
                        var warningEmbed = new DiscordEmbedBuilder()
                            .WithTitle("⚠️ Math is Disabled!")
                            .WithDescription("This guild doesn't allow math expressions for counting. Keep it simple—just type the number!")
                            .WithColor(DiscordColor.Orange)
                            .Build();

                        await message.RespondAsync(warningEmbed);
                        return(false, result);
                    }

                    var expression = new Expression(input, EvaluateOptions.NoCache);
                    var evaluation = expression.Evaluate();

                    if (evaluation is int intResult)
                    {
                        result = intResult;
                        return (true, result);
                    }

                    else if (evaluation is double doubleResult)
                    {

                        if (doubleResult % 1 is 0)
                        {
                            result = (int)doubleResult;
                            return (true, result);
                        }

                        var warningEmbed = new DiscordEmbedBuilder()
                            .WithTitle("⚠️ Invalid Result!")
                            .WithDescription($"The expression `{message.Content} = {doubleResult}` does not evaluate to a whole number! Only whole numbers are allowed.")
                            .WithColor(DiscordColor.Orange)
                            .Build();

                        await message.Channel!.SendMessageAsync(embed: warningEmbed);
                        return (false, 0);
                    }
                }

                else
                {
                    result = Convert.ToInt32(input, baseValue);
                    return (true, result);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to evaluate expression '{Expression}' with base {Base}. Error: {Error}", input, baseValue, ex.Message);
            }

            return (false, result);
        }

        private bool IsUserOnCooldown(ulong channelId, ulong userId)
        {
            if (_cooldowns.TryGetValue(channelId, out var users))
            {
                return users.Contains(userId);
            }
            return false;
        }

        private void AddUserToCooldown(ulong channelId, ulong userId)
        {
            var cooldownSet = _cooldowns.GetOrAdd(channelId, _ => new HashSet<ulong>());
            lock (cooldownSet)
            {
                cooldownSet.Add(userId);
            }
        }

        private void ResetCooldowns(ulong channelId)
        {
            if (_cooldowns.TryGetValue(channelId, out var users))
            {
                lock (users)
                {
                    users.Clear();
                }
            }
            Log.Information("All cooldowns have been reset for channel {ChannelId}.", channelId);
        }

        private async Task RemoveCooldownAfterDelay(ulong channelId, ulong userId)
        {
            await Task.Delay(CooldownSeconds * 1000);
            if (_cooldowns.TryGetValue(channelId, out var users))
            {
                lock (users)
                {
                    users.Remove(userId);
                }
            }
            Log.Debug("Cooldown removed for user {UserId} in channel {ChannelId}.", userId, channelId);
        }

        private static async Task DeleteMessagesAsync(DiscordMessage originalMessage, DiscordMessage warningMessage, int delayMs)
        {
            await Task.Delay(delayMs);
            await Task.WhenAll(originalMessage.DeleteAsync(), warningMessage.DeleteAsync());
        }
    }
}