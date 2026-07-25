using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Calendar.Availability.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Calendar.Availability.Services;

/// <inheritdoc cref="IAbsenceNotificationContentBuilder"/>
public class AbsenceNotificationContentBuilder(
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService) : IAbsenceNotificationContentBuilder
{
    /// <inheritdoc/>
    public async Task<string> GetGuildLanguageAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        return guild?.Language ?? "en";
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildAsync(
        string guildId,
        string requesterDiscordId,
        GuildNotificationEventType eventType,
        AbsenceKind kind,
        IReadOnlyList<DiscordEmbedField> fields,
        CancellationToken cancellationToken = default)
    {
        var language = await GetGuildLanguageAsync(guildId, cancellationToken);
        var (title, color) = AbsenceNotificationText.GetTitleAndColor(eventType, kind, language);
        var description = AbsenceNotificationText.GetDescription(eventType, kind, language, requesterDiscordId);
        var author = ResolveAuthor(guildId, requesterDiscordId, cancellationToken);

        return new DiscordEmbedContent(
            Title: title,
            Description: description,
            ColorHex: color,
            Fields: fields,
            Author: author);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPatternAsync(
        string guildId,
        string requesterDiscordId,
        GuildNotificationEventType eventType,
        DateOnly anchorDate,
        int cycleLengthDays,
        IReadOnlyList<PatternDayNotification> days,
        CancellationToken cancellationToken = default)
    {
        var language = await GetGuildLanguageAsync(guildId, cancellationToken);
        var (title, color) = AbsenceNotificationText.GetTitleAndColor(eventType, AbsenceKind.RecurringPattern, language);
        var description = AbsenceNotificationText.GetPatternDescription(eventType, language, requesterDiscordId, anchorDate, cycleLengthDays, days);
        var author = ResolveAuthor(guildId, requesterDiscordId, cancellationToken);

        return new DiscordEmbedContent(
            Title: title,
            Description: description,
            ColorHex: color,
            Author: author);
    }

    /// <summary>
    /// Best-effort — a member not found in the bot's cache (left the server, bot not present)
    /// just means no author byline, never a failure to notify.
    /// </summary>
    private DiscordEmbedAuthor? ResolveAuthor(string guildId, string requesterDiscordId, CancellationToken cancellationToken)
    {
        try
        {
            var member = discordBotService.Guilds.GetUser(guildId, requesterDiscordId, cancellationToken);
            if (member is null)
                return null;

            var name = member.Nickname ?? member.GlobalName ?? member.Username;
            var iconUrl = (member.HasGuildAvatar ? member.GetGuildAvatarUrl() : member.GetAvatarUrl())?.ToString();
            return new DiscordEmbedAuthor(name, iconUrl);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
