using CountingBot.Helpers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;
using System.Collections.Concurrent;

using CountingBot.Services.Database;

namespace CountingBot.Listeners
{
    public class MessageHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _cooldowns = new();
        private readonly ConcurrentDictionary<ulong, int> _count = new();
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _channelSemaphores = new();
        private const int CooldownSeconds = 2;
        private DiscordEmoji? _correctEmoji;
        private DiscordEmoji? _wrongEmoji;

        public MessageHandler(IGuildSettingsService guildSettingsService, IUserInformationService userInformationService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
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
                _count[e.Channel.Id] = await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id);
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
                int currentCount = _count.GetValueOrDefault(channelId, 0);

                if (!int.TryParse(e.Message.Content, out int parsedNumber))
                {
                    Log.Debug("Message is not a valid number in base {Base} for channel {ChannelId}. Ignoring.", baseValue, channelId);
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
                    var warningMessage = await e.Channel.SendMessageAsync(embed: embed);
                    _ = DeleteMessagesAsync(e.Message, warningMessage, 2500);
                    return;
                }

                if (parsedNumber == currentCount + 1)
                {
                    _count[channelId] = parsedNumber;
                    AddUserToCooldown(channelId, e.Author.Id);
                    _ = RemoveCooldownAfterDelay(channelId, e.Author.Id);

                    Log.Information("Valid count in channel {ChannelId}: count updated to {NewCount}", channelId, parsedNumber);
                    _ = e.Message.CreateReactionAsync(_correctEmoji!);
                    _ = _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, parsedNumber);
                    _ = _userInformationService.UpdateUserCountAsync(e.Guild.Id, e.Author.Id, parsedNumber, true);
                }
                else
                {
                    Log.Warning("Invalid count in channel {ChannelId}. Expected {Expected}, but got {ParsedNumber}. Resetting state.", channelId, currentCount + 1, parsedNumber);
                    _ = e.Message.CreateReactionAsync(_wrongEmoji!);
                    _ = _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, 0);
                    _ = _userInformationService.UpdateUserCountAsync(e.Guild.Id, e.Author.Id, parsedNumber, false);
                    _count[channelId] = 0;
                    ResetCooldowns(channelId);

                    var embed = MessageHelpers.GenericErrorEmbed(
                        title: "Count Ruined!",
                        message: $"{e.Author.Mention} ruined the count! The expected number was **{currentCount + 1}**, but **{parsedNumber}** was provided. The count has been reset to **0**."
                    );
                    await e.Channel.SendMessageAsync(embed: embed);
                }
            }
            finally
            {
                semaphore.Release();
            }
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