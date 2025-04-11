using System.Text;
using System.Text.RegularExpressions;
using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace CountingBot.Helpers
{
    /// <summary>
    /// Converting string arguments into the correct types
    /// </summary>
    public static class ConversionHelpers
    {
        public enum Type
        {
            Message,
        }

        public class Exception : System.Exception
        {
            public new string? Message;
            public string Argument;
            public Type Type;

            public Exception(string? message, string argument, Type type)
            {
                Message = message;
                Argument = argument;
                Type = type;
            }

            public DiscordEmbed ToEmbed()
            {
                StringBuilder message = new StringBuilder();
                message.AppendLine($"Error when attempting to parse argument `{Argument}`.");
                if (Message is not null)
                    message.AppendLine($"> {Message}");
                switch (Type)
                {
                    case Type.Message:
                        message.AppendLine("*A message link or message ID must be provided.*");
                        break;
                }

                return MessageHelpers.GenericErrorEmbed(
                    title: "Parsing Error",
                    message: message.ToString()
                );
            }
        }

        /// <summary>
        /// Converts a message link or ID into the DiscordMessage
        /// </summary>
        public static async Task<DiscordMessage> GetMessage(
            string s,
            string argumentName,
            CommandContext context
        )
        {
            ulong guildId,
                channelId,
                messageId;

            guildId = context.Guild!.Id;

            // TODO: Make this work with providing a channel as well (once channel parsing is done),
            // with a syntax like: `[channel]:[message]` where [channel] is the parsed channel (id or link) and [message] is the message id, whitespace shouldn't matter
            if (ulong.TryParse(s, out ulong messageIdOut))
            {
                messageId = messageIdOut;
                channelId = context.Channel.Id;
            }
            else
            {
                // Regex to match the URL
                var regex = new Regex(@"^https:\/\/discord\.com\/channels\/(\d+)\/(\d+)\/(\d+)$");
                var match = regex.Match(s);
                if (!match.Success || match.Groups.Count != 4)
                {
                    throw new Exception(null, argumentName, Type.Message);
                }

                // Extract and convert the IDs
                if (ulong.Parse(match.Groups[1].Value) != guildId)
                {
                    throw new Exception(
                        "Can only use messages of current server.",
                        argumentName,
                        Type.Message
                    );
                }
                channelId = ulong.Parse(match.Groups[2].Value);
                messageId = ulong.Parse(match.Groups[3].Value);
            }

            try
            {
                DiscordChannel channel =
                    context.Channel.Id == channelId
                        ? context.Channel
                        : await context.Guild.GetChannelAsync(channelId);
                DiscordMessage message = await channel.GetMessageAsync(messageId);

                return message;
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            {
                throw new Exception("Message not found.", argumentName, Type.Message);
            }
        }
    }
}
