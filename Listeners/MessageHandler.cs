using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using NCalc;
using Serilog;
using CountingBot.Services.Database;
using CountingBot.Services;

namespace CountingBot.Listeners
{
    public class MessageHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly IUserInformationService _userInformationService;
        private readonly ILanguageService _languageService;
        private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _cooldowns = new();
        private readonly ConcurrentDictionary<(ulong GuildId, ulong ChannelId), (ulong? messageId, int)> _count = new();
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _channelSemaphores = new();
        private readonly ConcurrentDictionary<ulong, HashSet<ulong>> _reviving = new();
        public ConcurrentDictionary<ulong, HashSet<ulong>> Reviving => _reviving;
        private const int CooldownSeconds = 2;
        private DiscordEmoji? _correctEmoji;
        private DiscordEmoji? _wrongEmoji;

        public MessageHandler(IGuildSettingsService guildSettingsService, IUserInformationService userInformationService, ILanguageService languageService)
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
        }

        public void RemoveChannelFromReviving(ulong guildId, ulong channelId)
        {
            if (Reviving.TryGetValue(guildId, out var channels))
            {
                bool removed = channels.Remove(channelId);
                if (removed)
                {
                    Log.Debug("Channel {ChannelId} removed from guild {GuildId}.", channelId, guildId);
                }
                else
                {
                    Log.Debug("Channel {ChannelId} was not found in guild {GuildId}'s list.", channelId, guildId);
                }
            }
            else
            {
                Log.Debug("No entry for guild {GuildId} in Reviving dictionary.", guildId);
            }
        }

        public async Task HandleMessageDeleted(DiscordClient client, MessageDeletedEventArgs e)
        {
            ulong channelId = e.Channel.Id;
            ulong guildId = e.Guild.Id;
            try
            {
                var (messageId, currentCount) = _count[(guildId, channelId)];

                if (e.Message.Id == messageId)
                {
                    int baseValue = await _guildSettingsService.GetChannelBase(guildId, channelId);
                    string parsedCurrentCount = Convert.ToString(currentCount, baseValue);
                    string lang = await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";

                    var title = await _languageService.GetLocalizedStringAsync("CountMessageDeletedTitle", lang);
                    var descTemplate = await _languageService.GetLocalizedStringAsync("CountMessageDeletedDescription", lang);
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

        public async Task HandleMessage(DiscordClient client, MessageCreatedEventArgs e)
        {
            Log.Debug("Handling message from user {User} in channel {ChannelId}.", e.Author.Username, e.Channel.Id);

            Log.Debug("{RevivesDictionary}", string.Join(", ", Reviving.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}")));

            if (!Reviving.ContainsKey(e.Guild.Id))
            {
                Reviving[e.Guild.Id] = new HashSet<ulong>();
                Log.Debug("Initialized Reviving for Guild {GuildId}", e.Guild.Id);
                Log.Debug("{RevivesDictionary}", string.Join(", ", Reviving.Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value)}")));
            }

            if (Reviving.TryGetValue(e.Guild.Id, out var channelSet) && channelSet is not null && channelSet.Contains(e.Channel.Id))
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

            if (await _guildSettingsService.CheckIfCountingChannel(e.Guild.Id, e.Channel.Id))
            {
                int baseValue = await _guildSettingsService.GetChannelBase(e.Guild.Id, e.Channel.Id);
                _count[(e.Guild.Id, e.Channel.Id)] = (null, await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id));

                await ProcessCountingMessage(client, e, baseValue);
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

        private async Task ProcessCountingMessage(DiscordClient client, MessageCreatedEventArgs e, int baseValue)
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
                    string lang = await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id) ?? "en";
                    var title = await _languageService.GetLocalizedStringAsync("SlowdownTitle", lang);
                    var messageText = await _languageService.GetLocalizedStringAsync("SlowdownMessage", lang);
                    var embed = new DiscordEmbedBuilder()
                        .WithTitle(title)
                        .WithDescription(messageText)
                        .WithColor(DiscordColor.Orange)
                        .Build();
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
                    string lang = await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id) ?? "en";
                    var reviveTitle = await _languageService.GetLocalizedStringAsync("DisasterStrikesTitle", lang);
                    var reviveMessageTemplate = await _languageService.GetLocalizedStringAsync("DisasterStrikesMessage", lang);
                    var reviveButtonMessage = await _languageService.GetLocalizedStringAsync("ReviveButtonMessage", lang);
                    var reviveEmbed = new DiscordEmbedBuilder()
                        .WithTitle(reviveTitle)
                        .WithDescription(reviveMessageTemplate)
                        .WithColor(DiscordColor.Orange)
                        .Build();

                    var reviveResponse = new DiscordMessageBuilder().AddEmbed(reviveEmbed).AddComponents(
                        new DiscordButtonComponent(DiscordButtonStyle.Primary, "use_revive", reviveButtonMessage),
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary, "translate_DisasterStrikesTitle_DisasterStrikesMessage", 
                            DiscordEmoji.FromName(client,":globe_with_meridians:"))
                    );
                    var reviveMsg = await e.Message.RespondAsync(reviveResponse);
                    Reviving[e.Guild.Id].Add(e.Channel.Id);

                    DateTime userCurrentDay = await _userInformationService.GetOrUpdateCurrentDay(e.Author.Id);
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
                                var expiredTitle = await _languageService.GetLocalizedStringAsync("ReviveRequestExpiredTitle", lang);
                                var expiredMessage = await _languageService.GetLocalizedStringAsync("ReviveRequestExpiredMessage", lang);
                                var expiredEmbed = new DiscordEmbedBuilder()
                                    .WithTitle(expiredTitle)
                                    .WithDescription(expiredMessage)
                                    .WithColor(DiscordColor.Red)
                                    .Build();

                                var builder = new DiscordMessageBuilder().AddEmbed(expiredEmbed);
                                builder.ClearComponents();
                                builder.AddComponents(new DiscordButtonComponent(
                                    DiscordButtonStyle.Secondary, "translate_ReviveRequestExpiredTitle_ReviveRequestExpiredMessage", 
                                    DiscordEmoji.FromName(client,":globe_with_meridians:")));

                                await reviveMsg.ModifyAsync(builder);

                                _ = e.Message.CreateReactionAsync(_wrongEmoji!);
                                _ = _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, 0);
                                _ = _userInformationService.UpdateUserCountAsync(e.Guild.Id, e.Author.Id, parsedNumber, false);
                                _count[(e.Guild.Id, e.Channel.Id)] = (messageId, await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id));
                                ResetCooldowns(channelId);
                                Reviving[e.Guild.Id].Remove(e.Channel.Id);
                            }
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException)
                        {
                            // The message was deleted, so do nothing
                        }
                    });

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
                        string lang = await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                        var mathDisabledTitle = await _languageService.GetLocalizedStringAsync("MathDisabledTitle", lang);
                        var mathDisabledDesc = await _languageService.GetLocalizedStringAsync("MathDisabledDescription", lang);
                        var warningEmbed = new DiscordEmbedBuilder()
                            .WithTitle(mathDisabledTitle)
                            .WithDescription(mathDisabledDesc)
                            .WithColor(DiscordColor.Orange)
                            .Build();

                        await message.RespondAsync(warningEmbed);
                        return (false, result);
                    }

                    var expression = new Expression(input);
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

                        string lang = await _guildSettingsService.GetGuildPreferredLanguageAsync(guildId) ?? "en";
                        var invalidResultTitle = await _languageService.GetLocalizedStringAsync("InvalidResultTitle", lang);
                        var invalidResultTemplate = await _languageService.GetLocalizedStringAsync("InvalidResultDescription", lang);
                        var warningEmbed = new DiscordEmbedBuilder()
                            .WithTitle(invalidResultTitle)
                            .WithDescription(string.Format(invalidResultTemplate, message.Content, doubleResult))
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
