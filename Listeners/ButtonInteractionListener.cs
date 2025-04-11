using System.Text.RegularExpressions;
using CountingBot.Helpers;
using CountingBot.Services;
using CountingBot.Services.Database;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog;

namespace CountingBot.Listeners
{
    public class ButtonInteractionListener
    {
        private readonly IUserInformationService _userInformationService;
        private readonly IGuildSettingsService _guildSettingsService;
        private readonly ILanguageService _languageService;
        private readonly MessageHandler _messageHandler;

        public ButtonInteractionListener(
            IGuildSettingsService guildSettingsService,
            IUserInformationService userInformationService,
            ILanguageService languageService,
            MessageHandler messageHandler
        )
        {
            _guildSettingsService = guildSettingsService;
            _userInformationService = userInformationService;
            _languageService = languageService;
            _messageHandler = messageHandler;
        }

        public async Task HandleButtonInteraction(
            DiscordClient client,
            ComponentInteractionCreatedEventArgs e
        )
        {
            try
            {
                Log.Debug("Handling button interaction from {UserId}", e.User.Id);
                DateTimeOffset messageTimestamp = e.Message.Timestamp;
                DateTimeOffset currentTime = DateTimeOffset.UtcNow;
                TimeSpan messageAge = currentTime - messageTimestamp;

                TimeSpan timeoutThreshold = TimeSpan.FromMinutes(5);

                if (
                    messageAge > timeoutThreshold
                    && e.Interaction.Data.ComponentType is DiscordComponentType.Button
                )
                {
                    var timeoutEmbed = new DiscordEmbedBuilder()
                        .WithTitle("Interaction Timed Out")
                        .WithDescription("This interaction has timed out due to inactivity.")
                        .WithColor(DiscordColor.Red)
                        .Build();

                    var message = new DiscordMessageBuilder().ClearEmbeds().AddEmbed(timeoutEmbed);
                    message.ClearComponents();
                    await e.Message.ModifyAsync(message);
                    return;
                }

                switch (e.Id)
                {
                    case "use_revive":
                    {
                        Log.Debug("Handling revive button interaction from {UserId}", e.User.Id);

                        DiscordMessage? referencedMessage = null;
                        try
                        {
                            referencedMessage = await GetReferencedMessageAsync(e);
                        }
                        catch (DSharpPlus.Exceptions.NotFoundException ex)
                        {
                            Log.Warning(
                                ex,
                                "Failed to fetch referenced message as it doesn't exist."
                            );
                        }

                        await HandleReviveUsageAsync(e, referencedMessage);
                        break;
                    }

                    case "confirm_reset":
                    {
                        Log.Information("Reset Confirmed for user {UserId}", e.User.Id);
                        await _userInformationService.DeleteUserInformationAsync(e.User.Id);
                        string lang = await _userInformationService.GetUserPreferredLanguageAsync(
                            e.User.Id
                        );
                        string title = await _languageService.GetLocalizedStringAsync(
                            "ResetConfirmedTitle",
                            lang
                        );
                        string message = await _languageService.GetLocalizedStringAsync(
                            "ResetConfirmedMessage",
                            lang
                        );
                        var embed = MessageHelpers.GenericEmbed(title, message);
                        var responseBuilder = new DiscordInteractionResponseBuilder()
                            .ClearEmbeds()
                            .AddEmbed(embed)
                            .AsEphemeral(true);
                        responseBuilder.ClearComponents();
                        await e.Interaction.CreateResponseAsync(
                            DiscordInteractionResponseType.UpdateMessage,
                            responseBuilder
                        );
                        break;
                    }

                    case "cancel_reset":
                    {
                        Log.Information("Reset Canceled for user {UserId}", e.User.Id);
                        string lang = await _userInformationService.GetUserPreferredLanguageAsync(
                            e.User.Id
                        );
                        string title = await _languageService.GetLocalizedStringAsync(
                            "ResetCanceledTitle",
                            lang
                        );
                        string message = await _languageService.GetLocalizedStringAsync(
                            "ResetCanceledMessage",
                            lang
                        );
                        var embed = MessageHelpers.GenericEmbed(
                            title,
                            message,
                            DiscordColor.Orange.ToString()
                        );
                        var responseBuilder = new DiscordInteractionResponseBuilder()
                            .ClearEmbeds()
                            .AddEmbed(embed)
                            .AsEphemeral(true);
                        responseBuilder.ClearComponents();
                        await e.Interaction.CreateResponseAsync(
                            DiscordInteractionResponseType.UpdateMessage,
                            responseBuilder
                        );
                        break;
                    }

                    case var _ when e.Id.StartsWith("translate_"):
                    {
                        await HandleTranslationAsync(e);
                        break;
                    }

                    case "next_achievements_page":
                    {
                        await HandleAchievementPageNavigation(e, true);
                        break;
                    }

                    case "previous_achievements_page":
                    {
                        await HandleAchievementPageNavigation(e, false);
                        break;
                    }

                    case "bug_fixed":
                    {
                        await HandleBugFixedAsync(e);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling button interaction");
            }
        }

        /// <summary>
        /// Function to handle the "bug_fixed" button interaction.
        /// </summary>
        /// <param name="e" description="The event arguments for the button interaction."></param>
        /// <returns></returns>
        private async Task HandleBugFixedAsync(ComponentInteractionCreatedEventArgs eventContext)
        {
            // Extract the user ID from the button ID
            var lang = await GetUserLanguageAsync(eventContext);

            // Build the response
            var title = await _languageService.GetLocalizedStringAsync("BugReportFixedTitle", lang);
            var message = await _languageService.GetLocalizedStringAsync(
                "BugReportFixedDescription",
                lang
            );
            var embed = MessageHelpers.GenericEmbed(title, message, DiscordColor.Green.ToString());

            // Delete the original message
            await eventContext.Message.DeleteAsync();

            // Respond with the new message
            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);
            responseBuilder.ClearComponents();
            await eventContext.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                responseBuilder
            );

            Log.Information("Bug report marked as fixed by {User}", eventContext.User.Username);
        }

        private async Task HandleAchievementPageNavigation(
            ComponentInteractionCreatedEventArgs e,
            bool isNextPage
        )
        {
            string lang =
                await _userInformationService.GetUserPreferredLanguageAsync(e.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild!.Id)
                ?? "en";

            var (currentPage, totalPages) = GetCurrentPageFromFooter(e.Message.Embeds[0]);

            int newPage = isNextPage ? currentPage + 1 : currentPage - 1;
            newPage = Math.Clamp(newPage, 1, totalPages);

            var unlockedAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                e.User.Id,
                pageNumber: newPage,
                pageSize: 5
            );
            var totalAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                e.User.Id,
                1,
                pageSize: 9999
            );

            string title = await _languageService.GetLocalizedStringAsync(
                "AchievementsTitle",
                lang
            );
            title = string.Format(title, e.User.GlobalName);

            string description = await _languageService.GetLocalizedStringAsync(
                "AchievementsDescription",
                lang
            );
            description = string.Format(
                description,
                totalAchievements.Count(c => c.IsCompleted).ToString(),
                totalAchievements.Count
            );

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = DiscordColor.Green,
            };
            embed.WithThumbnail(e.User.AvatarUrl);
            embed.WithFooter($"Page {newPage}/{totalPages}");

            foreach (var achievement in unlockedAchievements)
            {
                bool isUnlocked = achievement.IsCompleted;
                string statusSymbol = isUnlocked ? "✅" : "❌";

                string? localizedName =
                    achievement.Secret && !isUnlocked
                        ? await _languageService.GetLocalizedStringAsync("SecretAchievement", lang)
                        : await _languageService.GetLocalizedStringAsync(achievement.Name, lang);

                string? localizedDescription =
                    achievement.Secret && !isUnlocked
                        ? await _languageService.GetLocalizedStringAsync(
                            "SecretAchievementDescription",
                            lang
                        )
                        : await _languageService.GetLocalizedStringAsync(
                            achievement.Description!,
                            lang
                        );

                embed.AddField(
                    $"{statusSymbol} {localizedName}",
                    "╰➤ " + localizedDescription!,
                    false
                );
            }

            var response = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AddComponents(e.Message.ComponentActionRows!);

            await e.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.UpdateMessage,
                response
            );
        }

        public static (int currentPage, int totalPages) GetCurrentPageFromFooter(DiscordEmbed embed)
        {
            var footerText = embed.Footer?.Text;

            if (string.IsNullOrEmpty(footerText))
            {
                throw new InvalidOperationException("Footer is empty or not set.");
            }

            var regex = new Regex(@"Page (\d+)/(\d+)", RegexOptions.IgnoreCase);

            var match = regex.Match(footerText);

            if (match.Success)
            {
                var currentPage = int.Parse(match.Groups[1].Value);
                var totalPages = int.Parse(match.Groups[2].Value);

                return (currentPage, totalPages);
            }
            else
            {
                throw new InvalidOperationException("Footer text is not in the expected format.");
            }
        }

        private async Task HandleTranslationAsync(ComponentInteractionCreatedEventArgs e)
        {
            var lang = await GetUserLanguageAsync(e);
            var (embedDescription, embedFields) = ExtractEmbedContent(e.Message);
            var translationKeys = ParseTranslationKeys(e.Id);

            if (translationKeys.titleKey is not null)
            {
                await SendTranslatedResponseAsync(
                    e,
                    lang,
                    translationKeys,
                    embedDescription,
                    embedFields
                );
            }
            else
            {
                Log.Debug("Invalid button ID structure.");
            }
        }

        private async Task<string> GetUserLanguageAsync(ComponentInteractionCreatedEventArgs e)
        {
            return await _userInformationService.GetUserPreferredLanguageAsync(e.User.Id)
                ?? await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id)
                ?? "en";
        }

        private static (string? description, string? fields) ExtractEmbedContent(
            DiscordMessage message
        )
        {
            string? description = null;
            string? fields = null;

            if (message.Embeds.Count > 0)
            {
                description = message.Embeds[0].Description;
                if (message.Embeds[0].Fields is not null)
                {
                    fields = string.Join(
                        "\n",
                        message.Embeds[0].Fields!.Select(f => $"**{f.Name}:** {f.Value}")
                    );
                }
            }

            return (description, fields);
        }

        private static (
            string? titleKey,
            string? messageKey,
            string? footerKey
        ) ParseTranslationKeys(string buttonId)
        {
            string[] args = buttonId.Substring("translate_".Length).Split('_');
            return (
                titleKey: args.Length > 0 ? args[0] : null,
                messageKey: args.Length > 1 ? args[1] : null,
                footerKey: args.Length > 2 ? args[2] : null
            );
        }

        private async Task SendTranslatedResponseAsync(
            ComponentInteractionCreatedEventArgs e,
            string lang,
            (string? titleKey, string? messageKey, string? footerKey) keys,
            string? embedDescription,
            string? embedFields
        )
        {
            var embed = new DiscordEmbedBuilder { Color = DiscordColor.Lilac };

            if (keys.titleKey is not null)
            {
                embed.WithTitle(
                    await _languageService.GetLocalizedStringAsync(keys.titleKey, lang)
                );
            }

            if (keys.messageKey is not null)
            {
                if (keys.messageKey is "Original")
                {
                    embed.WithDescription(embedDescription!);
                    embed.WithDescription(embedFields!);
                }
                else
                {
                    embed.WithDescription(
                        await _languageService.GetLocalizedStringAsync(keys.messageKey, lang)
                    );
                }
            }

            if (keys.footerKey is not null)
            {
                embed.WithFooter(
                    await _languageService.GetLocalizedStringAsync(keys.footerKey, lang)
                );
            }

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);
            await e.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                responseBuilder
            );
        }

        private async Task HandleReviveUsageAsync(
            ComponentInteractionCreatedEventArgs e,
            DiscordMessage? referencedMessage
        )
        {
            var revivesAvailable = await _userInformationService.GetUserRevivesAsync(
                e.User.Id,
                false
            );
            if (!revivesAvailable)
            {
                await RespondNoRevivesAsync(e);
                return;
            }

            await _userInformationService.GetUserRevivesAsync(e.User.Id, true);
            await HandleSuccessfulReviveAsync(e, referencedMessage);
        }

        private async Task HandleSuccessfulReviveAsync(
            ComponentInteractionCreatedEventArgs e,
            DiscordMessage? referencedMessage
        )
        {
            try
            {
                await referencedMessage!.DeleteAsync();
                await e.Message.DeleteAsync();
            }
            catch (DSharpPlus.Exceptions.NotFoundException ex)
            {
                Log.Warning(ex, "Failed to delete referenced message as it dosnt exits.");
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "Failed to delete referenced message {MessageId}",
                    referencedMessage!.Id
                );
            }

            var (baseValue, currentCount) = await GetCountDetailsAsync(e.Guild.Id, e.Channel.Id);
            var nextCount = Convert.ToString(currentCount + 1, baseValue);

            string lang =
                await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id) ?? "en";
            var title = await _languageService.GetLocalizedStringAsync("CountRevivedTitle", lang);
            var descriptionTemplate = await _languageService.GetLocalizedStringAsync(
                "CountRevivedDescription",
                lang
            );
            var description = string.Format(descriptionTemplate, nextCount);

            var revivedEmbed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(DiscordColor.Green)
                .WithTimestamp(DateTime.UtcNow)
                .Build();

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(revivedEmbed)
                .AddComponents(
                    new DiscordButtonComponent(
                        DiscordButtonStyle.Secondary,
                        "translate_CountRevivedTitle_CountRevivedDescription",
                        DiscordEmoji.FromUnicode("🌐")
                    )
                );

            await e.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                responseBuilder
            );
            _messageHandler.RemoveChannelFromReviving(e.Guild.Id, e.Channel.Id);
        }

        private async Task<DiscordMessage?> GetReferencedMessageAsync(
            ComponentInteractionCreatedEventArgs e
        )
        {
            if (e.Message.Reference?.Message is null)
            {
                Log.Warning("No referenced message for interaction {InteractionId}", e.Id);
                string lang =
                    await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id) ?? "en";
                string errorMsg = await _languageService.GetLocalizedStringAsync(
                    "OriginalMessageNotFound",
                    lang
                );
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent(errorMsg)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                "translate_null_OriginalMessageNotFound",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        )
                );
                return null;
            }

            return await e.Channel.GetMessageAsync(e.Message.Reference.Message.Id);
        }

        private async Task RespondNoRevivesAsync(ComponentInteractionCreatedEventArgs e)
        {
            Log.Information("User {UserId} has no revives", e.User.Id);
            string lang =
                await _guildSettingsService.GetGuildPreferredLanguageAsync(e.Guild.Id) ?? "en";
            string content = await _languageService.GetLocalizedStringAsync(
                "NoRevivesLeftMessage",
                lang
            );
            await RespondToInteractionAsync(e, content, "Null", "NoRevivesLeftMessage");
        }

        private static async Task RespondToInteractionAsync(
            ComponentInteractionCreatedEventArgs e,
            string content,
            string titleKey,
            string messageKey
        )
        {
            var embed = new DiscordEmbedBuilder().WithDescription($"{content}");

            await e.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddEmbed(embed)
                    .AddComponents(
                        new DiscordButtonComponent(
                            DiscordButtonStyle.Secondary,
                            $"translate_{titleKey}_{messageKey}",
                            DiscordEmoji.FromUnicode("🌐")
                        )
                    )
                    .AsEphemeral(true)
            );
        }

        private async Task<(int baseValue, int currentCount)> GetCountDetailsAsync(
            ulong guildId,
            ulong channelId
        )
        {
            return (
                await _guildSettingsService.GetChannelBase(guildId, channelId),
                await _guildSettingsService.GetChannelsCurrentCount(guildId, channelId)
            );
        }
    }
}
