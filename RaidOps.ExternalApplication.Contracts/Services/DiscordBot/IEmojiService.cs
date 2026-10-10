namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// Manages the bot's application emojis — bot-owned custom emojis usable in every guild the bot is
/// in (e.g. WoW class/spec icons referenced inline in Discord notification text). Synced once at
/// bot startup from a caller-supplied set of (name, source image URL) entries: adding a new entry
/// (a new spec icon, a new role icon, ...) needs no per-environment configuration — the next boot on
/// any of the bot's environments uploads whatever's missing, and callers resolve by the stable name
/// rather than a hardcoded ID that would differ per environment/bot.
/// </summary>
public interface IEmojiService
{
    /// <summary>
    /// Ensures every entry exists as one of the bot's application emojis, uploading whatever's
    /// missing. Idempotent — safe to call on every startup; existing emojis are left untouched. A
    /// failure to sync one entry is logged and skipped rather than aborting the rest.
    /// </summary>
    Task SyncAsync(IEnumerable<(string Name, string SourceUrl)> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discord markdown (<c>&lt;:name:id&gt;</c>) for the named emoji, or <c>null</c> if it hasn't
    /// been synced yet on this bot (e.g. <see cref="SyncAsync"/> hasn't run yet, or no manifest
    /// entry has this name) — callers should fall back to plain text rather than fail.
    /// </summary>
    string? GetMarkdown(string name);

    /// <summary>
    /// Raw application-emoji snowflake ID for the named emoji, or <c>null</c> under the same
    /// not-synced-yet condition as <see cref="GetMarkdown"/> — needed wherever an API surface wants
    /// the emoji by ID rather than markdown text (e.g. a select-menu option's icon).
    /// </summary>
    ulong? GetId(string name);
}
