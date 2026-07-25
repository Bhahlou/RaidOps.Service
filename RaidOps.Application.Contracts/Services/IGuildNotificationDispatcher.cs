using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Contracts.Services;

/// <summary>
/// Posts a Discord message for a domain event, if and only if the guild has opted into being
/// notified about it. Callers just report "this happened" — whether anything gets sent, and
/// where, is entirely driven by the guild's <see cref="RaidOps.Domain.Models.Discord.GuildNotificationSetting"/>.
/// Unrelated to <see cref="INotificationSignalProvider"/>, which drives the in-app notification bell.
/// </summary>
public interface IGuildNotificationDispatcher
{
    /// <summary>
    /// Posts <paramref name="embed"/> to the guild's configured channel for
    /// <paramref name="eventType"/>, if the guild has that event enabled with a channel set.
    /// No-ops silently otherwise (including on Discord send failure) — this must never fail the
    /// caller's own command.
    /// </summary>
    /// <param name="guildId">Discord snowflake ID of the guild the event occurred in.</param>
    /// <param name="eventType">The event that occurred.</param>
    /// <param name="embed">The message to post if the event is enabled.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    Task NotifyAsync(string guildId, GuildNotificationEventType eventType, DiscordEmbedContent embed, CancellationToken cancellationToken = default);
}
