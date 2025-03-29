using CountingBot.Helpers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;

namespace CountingBot.Listeners
{
    public class MessageHandler
    {
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly Dictionary<ulong, HashSet<ulong>> _cooldowns = [];
        private readonly Dictionary<ulong, int> _count = [];
        private const int CooldownSeconds = 3;
        private DiscordEmoji? _correctEmoji;
        private DiscordEmoji? _wrongEmoji;

        public MessageHandler(IGuildSettingsService guildSettingsService)
        {
            _guildSettingsService = guildSettingsService;
        }

        public async Task HandleMessage(DiscordClient client, MessageCreatedEventArgs e)
        {
            Log.Debug("Handling message from user {User} in channel {ChannelId}", e.Author.Username, e.Channel.Id);

            if (e.Author.IsBot)
            {
                Log.Debug("Message ignored because it was sent by a bot.");
                return;
            }

            if (_correctEmoji is null || _wrongEmoji is null)
            {
                _correctEmoji = DiscordEmoji.FromName(client, ":white_check_mark:");
                _wrongEmoji = DiscordEmoji.FromName(client, ":x:");
            }

            if (await _guildSettingsService.CheckIfCountingChannel(e.Guild.Id, e.Channel.Id))
            {
                int baseValue = await _guildSettingsService.GetChannelBase(e.Guild.Id, e.Channel.Id);
                _count[e.Channel.Id] = await _guildSettingsService.GetChannelsCurrentCount(e.Guild.Id, e.Channel.Id);
                await ProcessCountingMessage(client, e, baseValue);

            }
            else
            {
                Log.Debug("Message received in a non-counting channel. Ignoring.");
            }
        }

        private async Task ProcessCountingMessage(DiscordClient client, MessageCreatedEventArgs e, int baseValue)
        {
            ulong channelId = e.Channel.Id;
            Log.Debug("Processing counting message in base {Base} for channel {ChannelId}.", baseValue, channelId);

            try
            {
                int parsedNumber = Convert.ToInt32(e.Message.Content, baseValue);
                int currentCount = _count[channelId];

                Log.Debug("Parsed number: {ParsedNumber}, Expected: {Expected}", parsedNumber, currentCount + 1);

                if (_cooldowns.ContainsKey(channelId) && _cooldowns[channelId].Contains(e.Author.Id))
                {
                    Log.Warning("User {User} is on cooldown in channel {ChannelId}.", e.Author.Username, channelId);
                    var embed = MessageHelpers.GenericErrorEmbed(
                        title: "Cooldown",
                        message: "You are on cooldown! Please wait before counting again."
                    );
                    var message = await e.Channel.SendMessageAsync(embed: embed);
                    _ = Task.Run(async () =>
                    {
                        await e.Message.DeleteAsync();
                        await Task.Delay(2500);
                        await message.DeleteAsync();
                    });
                    return;
                }

                if (parsedNumber == currentCount + 1)
                {
                    _count[channelId] = parsedNumber;
                    if (!_cooldowns.ContainsKey(channelId))
                    {
                        _cooldowns[channelId] = new HashSet<ulong>();
                    }
                    _cooldowns[channelId].Add(e.Author.Id);
                    _ = RemoveCooldownAfterDelay(channelId, e.Author.Id);

                    Log.Information("Valid count in channel {ChannelId}: count updated to {NewCount}", channelId, parsedNumber);
                    await e.Message.CreateReactionAsync(_correctEmoji!);
                    await _guildSettingsService.SetChannelsCurrentCount(e.Guild.Id, e.Channel.Id, parsedNumber);
                }
                else
                {
                    Log.Warning("Invalid count in channel {ChannelId}. Expected {Expected}, but got {ParsedNumber}. Resetting state.", channelId, currentCount + 1, parsedNumber);
                    await e.Message.CreateReactionAsync(_wrongEmoji!);
                    _count[channelId] = 0;
                    ResetCooldowns(channelId);
                }
            }
            catch (FormatException)
            {
                Log.Debug("Message is not a valid number in base {Base} for channel {ChannelId}. Ignoring.", baseValue, channelId);
            }
        }

        private void ResetCooldowns(ulong channelId)
        {
            if (_cooldowns.ContainsKey(channelId))
            {
                _cooldowns[channelId].Clear();
            }
            Log.Information("All cooldowns have been reset for channel {ChannelId}.", channelId);
        }

        private async Task RemoveCooldownAfterDelay(ulong channelId, ulong userId)
        {
            await Task.Delay(CooldownSeconds * 1000);
            if (_cooldowns.ContainsKey(channelId))
            {
                _cooldowns[channelId].Remove(userId);
            }
            Log.Debug("Cooldown removed for user {UserId} in channel {ChannelId}.", userId, channelId);
        }
    }
}
