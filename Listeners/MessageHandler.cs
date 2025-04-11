using System.Collections.Concurrent;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using NCalc;
using Serilog;

namespace CountingBot.Listeners
{
    /// <summary>
    /// Handles Discord message events for counting channels.
    /// Processes counting messages, validates counts, and manages user interactions.
    /// Core component of the counting functionality.
    /// </summary>
    public class MessageHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;
        private readonly ICacheService _cacheService;
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSemaphores = new();
        private const int CooldownSeconds = 2;
        private DiscordEmoji? _correctEmoji;
        private DiscordEmoji? _wrongEmoji;
        private DiscordEmoji? _highscoreEmoji;

        // Cache key prefixes
        private const string CooldownPrefix = "Cooldown_";
        private const string RevivingPrefix = "Reviving_";
        private const string CountPrefix = "Count_";

        public MessageHandler(
            IGuildSettingsService guildSettingsService,
            IUserInformationService userInformationService,
            ILanguageService languageService,
            ICacheService cacheService
        )
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Gets the list of channels that are currently in reviving state for a guild
        /// </summary>
        /// <param name="guildId">The guild ID</param>
        /// <returns>A HashSet of channel IDs that are in reviving state</returns>
        public HashSet<ulong> GetRevivingChannels(ulong guildId)
        {
            string cacheKey = $"{RevivingPrefix}{guildId}";
            if (
                _cacheService.TryGetValue<HashSet<ulong>>(cacheKey, out var channels)
                && channels != null
            )
            {
                return channels;
            }

            var newSet = new HashSet<ulong>();
            _cacheService.Set(cacheKey, newSet);
            return newSet;
        }

        public void RemoveChannelFromReviving(ulong guildId, ulong channelId)
        {
            string cacheKey = $"{RevivingPrefix}{guildId}";
            if (
                _cacheService.TryGetValue<HashSet<ulong>>(cacheKey, out var channels)
                && channels != null
            )
            {
                bool removed = channels.Remove(channelId);
                if (removed)
                {
                    _cacheService.Set(cacheKey, channels); // Update the cache
                    Log.Debug(
                        "Channel {ChannelId} removed from guild {GuildId}.",
                        channelId,
                        guildId
                    );
                }
                else
                {
                    Log.Debug(
                        "Channel {ChannelId} was not found in guild {GuildId}'s list.",
                        channelId,
                        guildId
                    );
                }
            }
            else
            {
                Log.Debug("No entry for guild {GuildId} in Reviving dictionary.", guildId);
            }
        }

        /// <summary>
        /// Handles the deletion of a message in a counting channel.
        /// </summary>
        /// <param name="_"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task HandleMessageDeleted(DiscordClient _, MessageDeletedEventArgs e)
        {
            ulong channelId = e.Channel.Id;
            ulong guildId = e.Guild.Id;
            try
            {
                string countCacheKey = $"{CountPrefix}{guildId}_{channelId}";
                if (
                    !_cacheService.TryGetValue<(ulong? messageId, int currentCount)>(
                        countCacheKey,
                        out var countData
                    )
                )
                {
                    throw new KeyNotFoundException("Count data not found in cache");
                }

                var (messageId, currentCount) = countData;

                if (e.Message.Id == messageId)
                {
                    int baseValue = await _guildSettingsService.GetChannelBase(guildId, channelId);
                    string parsedCurrentCount = Convert.ToString(currentCount, baseValue);
                    string lang =
                        await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";

                    var title = await _languageService.GetLocalizedStringAsync(
                        "CountMessageDeletedTitle",
                        lang
                    );
                    var descTemplate = await _languageService.GetLocalizedStringAsync(
                        "CountMessageDeletedDescription",
                        lang
                    );
                    string description = string.Format(descTemplate, parsedCurrentCount);

                    var countResetEmbed = new DiscordEmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(description)
                        .WithColor(DiscordColor.Red)
                        .Build();

                    await e.Channel.SendMessageAsync(embed: countResetEmbed);
                }
            }
            catch (KeyNotFoundException ex)
            {
                Log.Debug(ex, "Message received in a non-counting channel. Ignoring.");
            }
        }

        /// <summary>
        /// Handles the creation of a message in a counting channel.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task HandleMessage(DiscordClient client, MessageCreatedEventArgs e)
        {
            Log.Debug(
                "Handling message from user {User} in channel {ChannelId}.",
                e.Author.Username,
                e.Channel.Id
            );

            var revivingChannels = GetRevivingChannels(e.Guild.Id);
            Log.Debug("{RevivesDictionary}", string.Join(", ", revivingChannels));

            if (revivingChannels.Contains(e.Channel.Id))
            {
                Log.Debug("Message received while revive message is in place");
                return;
            }

            if (e.Author.IsBot)
            {
                Log.Debug("Message ignored because it was sent by a bot.");
                return;
            }

            _correctEmoji ??= DiscordEmoji.FromName(client, ":white_check_mark:");
            _wrongEmoji ??= DiscordEmoji.FromName(client, ":x:");
            _highscoreEmoji ??= DiscordEmoji.FromName(client, ":trophy:");

            // Check if we have cached channel settings
            string channelSettingsCacheKey =
                $"{CacheService.ChannelSettingsPrefix}{e.Guild.Id}_{e.Channel.Id}";
            if (
                _cacheService.TryGetValue<(int BaseValue, bool IsCountingChannel)>(
                    channelSettingsCacheKey,
                    out var settings
                ) && settings.IsCountingChannel
            )
            {
                // Use cached settings
                string countCacheKey = $"{CountPrefix}{e.Guild.Id}_{e.Channel.Id}";
                _cacheService.Set<(ulong?, int)>(
                    countCacheKey,
                    (
                        null,
                        await _guildSettingsService.GetChannelsCurrentCount(
                            e.Guild.Id,
                            e.Channel.Id
                        )
                    )
                );
                await ProcessCountingMessage(client, e, settings.BaseValue);
                return;
            }

            // No cached settings, check from database
            if (await _guildSettingsService.CheckIfCountingChannel(e.Guild.Id, e.Channel.Id))
            {
                int baseValue = await _guildSettingsService.GetChannelBase(
                    e.Guild.Id,
                    e.Channel.Id
                );
                string countCacheKey = $"{CountPrefix}{e.Guild.Id}_{e.Channel.Id}";
                _cacheService.Set<(ulong?, int)>(
                    countCacheKey,
                    (
                        null,
                        await _guildSettingsService.GetChannelsCurrentCount(
                            e.Guild.Id,
                            e.Channel.Id
                        )
                    )
                );

                // Cache the settings for future use
                _cacheService.Set(channelSettingsCacheKey, (baseValue, true));

                await ProcessCountingMessage(client, e, baseValue);
            }
            else
            {
                // Cache that this is not a counting channel to avoid future database calls
                _cacheService.Set(channelSettingsCacheKey, (0, false));
                Log.Debug("Message received in a non-counting channel. Ignoring.");
            }
        }

        private SemaphoreSlim GetGuildSemaphore(ulong guildId)
        {
            return _guildSemaphores.GetOrAdd(guildId, _ => new SemaphoreSlim(3, 3)); // Allow 3 concurrent operations per guild
        }

        private async Task ProcessCountingMessage(
            DiscordClient client,
            MessageCreatedEventArgs e,
            int baseValue
        )
        {
            ulong channelId = e.Channel.Id;
            ulong guildId = e.Guild.Id;
            var semaphore = GetGuildSemaphore(guildId);
            await semaphore.WaitAsync();

            // Cache the channel settings
            string channelSettingsCacheKey =
                $"{CacheService.ChannelSettingsPrefix}{guildId}_{channelId}";
            _cacheService.Set(channelSettingsCacheKey, (baseValue, true));

            try
            {
                string countCacheKey = $"{CountPrefix}{e.Guild.Id}_{channelId}";
                if (
                    !_cacheService.TryGetValue<(ulong? messageId, int currentCount)>(
                        countCacheKey,
                        out var countData
                    )
                )
                {
                    throw new KeyNotFoundException("Count data not found in cache");
                }
                var (messageId, currentCount) = countData;
                var (success, parsedNumber) = await TryEvaluateExpressionAsync(
                    e.Guild.Id,
                    e.Message,
                    baseValue
                );

                if (!success)
                {
                    Log.Debug(
                        "Message is not a valid math expression or number in base {Base} for channel {ChannelId}. Ignoring.",
                        baseValue,
                        channelId
                    );
                    return;
                }

                Log.Debug(
                    "Parsed number: {ParsedNumber}, Expected: {Expected}",
                    parsedNumber,
                    currentCount + 1
                );

                if (IsUserOnCooldown(channelId, e.Author.Id))
                {
                    Log.Warning(
                        "User {User} is on cooldown in channel {ChannelId}.",
                        e.Author.Username,
                        channelId
                    );
                    string lang =
                        await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id)
                        ?? "en";
                    var title = await _languageService.GetLocalizedStringAsync(
                        "SlowdownTitle",
                        lang
                    );
                    var messageText = await _languageService.GetLocalizedStringAsync(
                        "SlowdownMessage",
                        lang
                    );
                    var embed = new DiscordEmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(messageText)
                        .WithColor(DiscordColor.Orange)
                        .Build();

                    var response = new DiscordMessageBuilder()
                        .AddEmbed(embed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_SlowdownTitle_SlowdownMessage",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        );
                    var warningMessage = await e.Message.RespondAsync(response);
                    _ = DeleteMessagesAsync(e.Message, warningMessage, 2500);
                    return;
                }

                if (parsedNumber == currentCount + 1)
                {
                    var countKey = $"{CountPrefix}{e.Guild.Id}_{e.Channel.Id}";
                    _cacheService.Set<(ulong?, int)>(countKey, (e.Message.Id, parsedNumber));
                    AddUserToCooldown(channelId, e.Author.Id);
                    _ = RemoveCooldownAfterDelay(channelId, e.Author.Id);

                    if (
                        await _guildSettingsService.IsNewHighscore(
                            e.Guild.Id,
                            e.Channel.Id,
                            parsedNumber
                        )
                    )
                    {
                        _ = e.Message.CreateReactionAsync(_highscoreEmoji!);
                    }
                    else
                    {
                        _ = e.Message.CreateReactionAsync(_correctEmoji!);
                    }

                    Log.Information(
                        "Valid count in channel {ChannelId}: count updated to {NewCount}",
                        channelId,
                        parsedNumber
                    );
                    _ = _guildSettingsService.SetChannelsCurrentCount(
                        e.Guild.Id,
                        e.Channel.Id,
                        parsedNumber
                    );
                    _ = _userInformationService.UpdateUserCountAsync(
                        e.Guild.Id,
                        e.Author.Id,
                        parsedNumber,
                        true
                    );
                }
                else
                {
                    string lang =
                        await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id)
                        ?? "en";
                    var reviveTitle = await _languageService.GetLocalizedStringAsync(
                        "DisasterStrikesTitle",
                        lang
                    );
                    var reviveMessageTemplate = await _languageService.GetLocalizedStringAsync(
                        "DisasterStrikesMessage",
                        lang
                    );
                    var reviveButtonMessage = await _languageService.GetLocalizedStringAsync(
                        "ReviveButtonMessage",
                        lang
                    );
                    var reviveEmbed = new DiscordEmbedBuilder()
                        .WithTitle(reviveTitle)
                        .WithDescription(reviveMessageTemplate)
                        .WithColor(DiscordColor.Orange)
                        .Build();

                    var reviveResponse = new DiscordMessageBuilder()
                        .AddEmbed(reviveEmbed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Primary,
                                "use_revive",
                                reviveButtonMessage
                            ),
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                "translate_DisasterStrikesTitle_DisasterStrikesMessage",
                                DiscordEmoji.FromName(client, ":globe_with_meridians:")
                            )
                        );
                    var reviveMsg = await e.Message.RespondAsync(reviveResponse);
                    var revivingChannels = GetRevivingChannels(e.Guild.Id);
                    revivingChannels.Add(e.Channel.Id);
                    _cacheService.Set($"{RevivingPrefix}{e.Guild.Id}", revivingChannels);

                    DateTime userCurrentDay = await _userInformationService.GetOrUpdateCurrentDay(
                        e.Author.Id
                    );
                    if (DateTime.UtcNow.Day == userCurrentDay.Day)
                    {
                        await _userInformationService.UpdateIncorrectCountsToday(e.Author.Id);
                    }

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(30000);

                        try
                        {
                            var checkMessage = await e.Channel.GetMessageAsync(reviveMsg.Id);

                            if (checkMessage is not null)
                            {
                                var expiredTitle = await _languageService.GetLocalizedStringAsync(
                                    "ReviveRequestExpiredTitle",
                                    lang
                                );
                                var expiredMessage = await _languageService.GetLocalizedStringAsync(
                                    "ReviveRequestExpiredMessage",
                                    lang
                                );
                                var expiredEmbed = new DiscordEmbedBuilder()
                                    .WithTitle(expiredTitle)
                                    .WithDescription(expiredMessage)
                                    .WithColor(DiscordColor.Red)
                                    .Build();

                                var builder = new DiscordMessageBuilder().AddEmbed(expiredEmbed);
                                builder.ClearComponents();
                                builder.AddComponents(
                                    new DiscordButtonComponent(
                                        DiscordButtonStyle.Secondary,
                                        "translate_ReviveRequestExpiredTitle_ReviveRequestExpiredMessage",
                                        DiscordEmoji.FromName(client, ":globe_with_meridians:")
                                    )
                                );

                                await reviveMsg.ModifyAsync(builder);

                                _ = e.Message.CreateReactionAsync(_wrongEmoji!);
                                _ = _guildSettingsService.SetChannelsCurrentCount(
                                    e.Guild.Id,
                                    e.Channel.Id,
                                    0
                                );
                                _ = _userInformationService.UpdateUserCountAsync(
                                    e.Guild.Id,
                                    e.Author.Id,
                                    parsedNumber,
                                    false
                                );
                                var countKey = $"{CountPrefix}{e.Guild.Id}_{e.Channel.Id}";
                                _cacheService.Set<(ulong?, int)>(
                                    countKey,
                                    (
                                        messageId,
                                        await _guildSettingsService.GetChannelsCurrentCount(
                                            e.Guild.Id,
                                            e.Channel.Id
                                        )
                                    )
                                );
                                ResetCooldowns(channelId);
                                RemoveChannelFromReviving(e.Guild.Id, e.Channel.Id);
                            }
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException)
                        {
                            // The message was deleted, so do nothing
                        }
                    });

                    Log.Warning(
                        "Invalid count in channel {ChannelId}. Expected {Expected}, but got {ParsedNumber}. Resetting state.",
                        channelId,
                        currentCount + 1,
                        parsedNumber
                    );
                }
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(bool Success, int Result)> TryEvaluateExpressionAsync(
            ulong guildId,
            DiscordMessage message,
            int baseValue
        )
        {
            int result = 0;
            string input = message.Content;
            try
            {
                bool containsMath =
                    input.Contains('+')
                    || input.Contains('-')
                    || input.Contains('*')
                    || input.Contains('/')
                    || input.Contains('(')
                    || input.Contains(')')
                    || input.Contains('^');

                if (containsMath)
                {
                    var expression = new Expression(input);
                    var evaluation = expression.Evaluate();

                    if (evaluation is int intResult)
                    {
                        if (!await _guildSettingsService.GetMathEnabledAsync(guildId))
                        {
                            string lang =
                                await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                                ?? "en";
                            var mathDisabledTitle = await _languageService.GetLocalizedStringAsync(
                                "MathDisabledTitle",
                                lang
                            );
                            var mathDisabledDesc = await _languageService.GetLocalizedStringAsync(
                                "MathDisabledDescription",
                                lang
                            );
                            var warningEmbed = new DiscordEmbedBuilder()
                                .WithTitle(mathDisabledTitle)
                                .WithDescription(mathDisabledDesc)
                                .WithColor(DiscordColor.Orange)
                                .Build();

                            var response = new DiscordMessageBuilder()
                                .AddEmbed(warningEmbed)
                                .AddComponents(
                                    new DiscordButtonComponent(
                                        DiscordButtonStyle.Secondary,
                                        $"translate_MathDisabledTitle_MathDisabledDescription",
                                        DiscordEmoji.FromUnicode("🌐")
                                    )
                                );

                            await message.RespondAsync(response);
                            return (false, result);
                        }

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

                        string lang =
                            await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId)
                            ?? "en";
                        var invalidResultTitle = await _languageService.GetLocalizedStringAsync(
                            "InvalidResultTitle",
                            lang
                        );
                        var invalidResultMessage = await _languageService.GetLocalizedStringAsync(
                            "InvalidResultDescription",
                            lang
                        );
                        var warningEmbed = new DiscordEmbedBuilder()
                            .WithTitle(invalidResultTitle)
                            .WithDescription(
                                string.Format(invalidResultMessage, message.Content, doubleResult)
                            )
                            .WithColor(DiscordColor.Orange)
                            .Build();

                        var response = new DiscordMessageBuilder()
                            .AddEmbed(warningEmbed)
                            .AddComponents(
                                new DiscordButtonComponent(
                                    DiscordButtonStyle.Secondary,
                                    $"translate_InvalidResultTitle_InvalidResultDescription",
                                    DiscordEmoji.FromUnicode("🌐")
                                )
                            );

                        await message.RespondAsync(response);
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
                Log.Debug(
                    ex,
                    "Failed to evaluate expression '{Expression}' with base {Base}. Error: {Error}",
                    input,
                    baseValue,
                    ex.Message
                );
            }

            return (false, result);
        }

        private bool IsUserOnCooldown(ulong channelId, ulong userId)
        {
            string cooldownCacheKey = $"{CooldownPrefix}{channelId}";
            if (
                _cacheService.TryGetValue<HashSet<ulong>>(cooldownCacheKey, out var users)
                && users != null
            )
            {
                return users.Contains(userId);
            }
            return false;
        }

        private void AddUserToCooldown(ulong channelId, ulong userId)
        {
            string cooldownCacheKey = $"{CooldownPrefix}{channelId}";
            if (
                !_cacheService.TryGetValue<HashSet<ulong>>(cooldownCacheKey, out var cooldownSet)
                || cooldownSet == null
            )
            {
                cooldownSet = [];
            }

            lock (cooldownSet)
            {
                cooldownSet.Add(userId);
                _cacheService.Set(
                    cooldownCacheKey,
                    cooldownSet,
                    TimeSpan.FromSeconds(CooldownSeconds + 1)
                );
            }
        }

        private void ResetCooldowns(ulong channelId)
        {
            string cooldownCacheKey = $"{CooldownPrefix}{channelId}";
            _cacheService.Set(
                cooldownCacheKey,
                new HashSet<ulong>(),
                TimeSpan.FromSeconds(CooldownSeconds + 1)
            );
            Log.Information("All cooldowns have been reset for channel {ChannelId}.", channelId);
        }

        private async Task RemoveCooldownAfterDelay(ulong channelId, ulong userId)
        {
            await Task.Delay(CooldownSeconds * 1000);
            string cooldownCacheKey = $"{CooldownPrefix}{channelId}";

            if (
                _cacheService.TryGetValue<HashSet<ulong>>(cooldownCacheKey, out var users)
                && users != null
            )
            {
                lock (users)
                {
                    users.Remove(userId);
                    _cacheService.Set(
                        cooldownCacheKey,
                        users,
                        TimeSpan.FromSeconds(CooldownSeconds + 1)
                    );
                }
            }
            Log.Debug(
                "Cooldown removed for user {UserId} in channel {ChannelId}.",
                userId,
                channelId
            );
        }

        private static async Task DeleteMessagesAsync(
            DiscordMessage originalMessage,
            DiscordMessage warningMessage,
            int delayMs
        )
        {
            await Task.Delay(delayMs);
            await Task.WhenAll(originalMessage.DeleteAsync(), warningMessage.DeleteAsync());
        }
    }
}
