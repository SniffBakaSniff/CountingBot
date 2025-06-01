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

                    case var _ when e.Id.StartsWith("confirm_setcount_"):
                    {
                        await HandleSetCountConfirmationAsync(e, true);
                        break;
                    }

                    case var _ when e.Id.StartsWith("cancel_setcount_"):
                    {
                        await HandleSetCountConfirmationAsync(e, false);
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

            // Get all achievements
            var allAchievements = await _userInformationService.GetUnlockedAchievementsAsync(
                e.User.Id,
                1,
                pageSize: 9999
            );

            // Determine the currently selected achievement type from the select menu
            var achievementTypeSelector = e
                .Message.Components!.SelectMany(row =>
                    (row as DiscordActionRowComponent)!.Components.OfType<DiscordSelectComponent>()
                )
                .FirstOrDefault(c => c.CustomId == "achievement_type_selector");

            // Default to All if no selector is found
            var selectedType = Features.Commands.AchievementType.All;

            // Find the selected option and parse the achievement type
            var selectedOption = achievementTypeSelector?.Options.FirstOrDefault(o => o.Default);
            if (
                selectedOption != null
                && Enum.TryParse<Features.Commands.AchievementType>(
                    selectedOption.Value,
                    out var parsedType
                )
            )
            {
                selectedType = parsedType;
            }

            // Filter achievements by type
            var typeString = selectedType.ToString();
            var filteredAchievements =
                selectedType == Features.Commands.AchievementType.All
                    ? allAchievements
                    : allAchievements
                        .Where(a =>
                            !string.IsNullOrEmpty(a.Type.ToString())
                            && string.Equals(
                                a.Type.ToString(),
                                typeString,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .ToList();

            // Calculate total pages for filtered achievements
            int totalFilteredPages = (int)Math.Ceiling(filteredAchievements.Count / 5.0);

            // Adjust page number if needed
            newPage = Math.Clamp(newPage, 1, Math.Max(1, totalFilteredPages));

            // Get the current page of achievements
            var pageAchievements = filteredAchievements.Skip((newPage - 1) * 5).Take(5).ToList();

            string title = await _languageService.GetLocalizedStringAsync(
                "AchievementsTitle",
                lang
            );
            title = string.Format(title, e.User.GlobalName ?? e.User.Username);

            string description = await _languageService.GetLocalizedStringAsync(
                "AchievementsDescription",
                lang
            );
            description = string.Format(
                description,
                filteredAchievements.Count(c => c.IsCompleted),
                filteredAchievements.Count
            );

            var embed = new DiscordEmbedBuilder
            {
                Title = title,
                Description = description,
                Color = DiscordColor.Green,
            };
            embed.WithThumbnail(e.User.AvatarUrl);
            embed.WithFooter($"Page {newPage}/{Math.Max(1, totalFilteredPages)}");

            foreach (var achievement in pageAchievements)
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

            // Create a response with the same components as the original message
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
            string[] args = buttonId["translate_".Length..].Split('_');
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

        /// <summary>
        /// Handles the confirmation or cancellation of the setcount command.
        /// </summary>
        /// <param name="e">The component interaction event args</param>
        /// <param name="confirmed">Whether the action was confirmed or canceled</param>
        private async Task HandleSetCountConfirmationAsync(
            ComponentInteractionCreatedEventArgs e,
            bool confirmed
        )
        {
            // Parse the channel ID and new count from the button ID
            // Format: confirm_setcount_channelId_newCount or cancel_setcount_channelId_newCount
            string[] parts = e.Id.Split('_');
            if (parts.Length is not 4)
            {
                Log.Error("Invalid setcount confirmation button ID format: {ButtonId}", e.Id);
                return;
            }

            if (
                !ulong.TryParse(parts[2], out ulong channelId)
                || !int.TryParse(parts[3], out int newCount)
            )
            {
                Log.Error(
                    "Failed to parse channel ID or new count from button ID: {ButtonId}",
                    e.Id
                );
                return;
            }

            string lang = await GetUserLanguageAsync(e);

            if (confirmed)
            {
                // User confirmed the action, update the count
                try
                {
                    await _guildSettingsService.SetChannelsCurrentCount(
                        e.Guild.Id,
                        channelId,
                        newCount
                    );

                    // Get the channel's number system base
                    int channelBase = await _guildSettingsService.GetChannelBase(
                        e.Guild.Id,
                        channelId
                    );

                    // Convert the new count to the channel's number system
                    string newCountInChannelBase = Convert
                        .ToString(newCount, channelBase)
                        .ToUpperInvariant();

                    string countUpdatedTitle = await _languageService.GetLocalizedStringAsync(
                        "SetCountUpdatedTitle",
                        lang
                    );
                    string countUpdatedMsg = await _languageService.GetLocalizedStringAsync(
                        "SetCountUpdatedDescription",
                        lang
                    );
                    var successEmbed = MessageHelpers.GenericSuccessEmbed(
                        countUpdatedTitle,
                        string.Format(countUpdatedMsg, newCount, channelBase, newCountInChannelBase)
                    );

                    // Update the confirmation message with success message
                    var responseBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(successEmbed)
                        .AsEphemeral(true);
                    responseBuilder.ClearComponents();
                    await e.Interaction.CreateResponseAsync(
                        DiscordInteractionResponseType.UpdateMessage,
                        responseBuilder
                    );

                    // Also send a public message to notify everyone
                    var publicMessage = new DiscordMessageBuilder()
                        .AddEmbed(successEmbed)
                        .AddComponents(
                            new DiscordButtonComponent(
                                DiscordButtonStyle.Secondary,
                                $"translate_SetCountUpdatedTitle_SetCountUpdatedDescription",
                                DiscordEmoji.FromUnicode("🌐")
                            )
                        );
                    await e.Channel.SendMessageAsync(publicMessage);

                    Log.Information(
                        "Count successfully updated to {NewCount} for channel {ChannelId} by {User}.",
                        newCount,
                        channelId,
                        e.User.Username
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(
                        ex,
                        "An error occurred while updating the count for channel {ChannelId} in guild {GuildId}.",
                        channelId,
                        e.Guild.Id
                    );

                    string errorTitle = await _languageService.GetLocalizedStringAsync(
                        "GenericErrorTitle",
                        lang
                    );
                    string errorMsg = await _languageService.GetLocalizedStringAsync(
                        "GenericErrorMessage",
                        lang
                    );
                    var errorEmbed = MessageHelpers.GenericErrorEmbed(errorTitle, errorMsg);

                    var responseBuilder = new DiscordInteractionResponseBuilder()
                        .AddEmbed(errorEmbed)
                        .AsEphemeral(true);
                    responseBuilder.ClearComponents();
                    await e.Interaction.CreateResponseAsync(
                        DiscordInteractionResponseType.UpdateMessage,
                        responseBuilder
                    );
                }
            }
            else
            {
                // User canceled the action
                string cancelTitle = await _languageService.GetLocalizedStringAsync(
                    "SetCountCanceledTitle",
                    lang
                );
                string cancelMsg = await _languageService.GetLocalizedStringAsync(
                    "SetCountCanceledDescription",
                    lang
                );
                var cancelEmbed = MessageHelpers.GenericEmbed(
                    cancelTitle,
                    cancelMsg,
                    DiscordColor.Orange.ToString()
                );

                var responseBuilder = new DiscordInteractionResponseBuilder()
                    .AddEmbed(cancelEmbed)
                    .AsEphemeral(true);
                responseBuilder.ClearComponents();
                await e.Interaction.CreateResponseAsync(
                    DiscordInteractionResponseType.UpdateMessage,
                    responseBuilder
                );

                Log.Information(
                    "Count update to {NewCount} for channel {ChannelId} was canceled by {User}.",
                    newCount,
                    channelId,
                    e.User.Username
                );
            }
        }
    }
}
