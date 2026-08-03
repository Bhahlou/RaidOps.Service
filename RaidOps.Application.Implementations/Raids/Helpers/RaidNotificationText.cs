using System.Globalization;
using RaidOps.Domain.Enums;

namespace RaidOps.Application.Implementations.Raids.Helpers;

/// <summary>
/// Localized title/color/description text for the two raid Discord notification families ("Raid
/// changes" and "Raid composition changes"), keyed by (<see cref="GuildNotificationEventType"/>,
/// guild language). Falls back to English for an unsupported/missing language rather than throwing
/// — a notification should never fail to send just because
/// <see cref="RaidOps.Domain.Models.Discord.Guild.Language"/> is unset. Mirrors
/// <see cref="RaidOps.Application.Implementations.Calendar.Availability.Helpers.AbsenceNotificationText"/>'s
/// title/color dictionary-with-fallback shape; descriptions are explicit per-event methods instead
/// of a shared format-string table, since each raid event carries a structurally different payload.
/// </summary>
internal static class RaidNotificationText
{
    private static readonly Dictionary<(GuildNotificationEventType EventType, string Language), (string Title, int Color)> TitleAndColor = new()
    {
        // Discord brand green/red/yellow/blurple — reads clearly against both the light and dark client themes.
        [(GuildNotificationEventType.RaidPublished, "en")] = ("Raid published", 0x57F287),
        [(GuildNotificationEventType.RaidPublished, "fr")] = ("Raid publié", 0x57F287),
        [(GuildNotificationEventType.RaidPublished, "de")] = ("Raid veröffentlicht", 0x57F287),

        [(GuildNotificationEventType.RaidCancelled, "en")] = ("Raid cancelled", 0xED4245),
        [(GuildNotificationEventType.RaidCancelled, "fr")] = ("Raid annulé", 0xED4245),
        [(GuildNotificationEventType.RaidCancelled, "de")] = ("Raid abgesagt", 0xED4245),

        [(GuildNotificationEventType.RaidRescheduled, "en")] = ("Raid rescheduled", 0xFEE75C),
        [(GuildNotificationEventType.RaidRescheduled, "fr")] = ("Raid reprogrammé", 0xFEE75C),
        [(GuildNotificationEventType.RaidRescheduled, "de")] = ("Raid verschoben", 0xFEE75C),

        [(GuildNotificationEventType.RaidSlotAssigned, "en")] = ("Character added", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotAssigned, "fr")] = ("Personnage ajouté", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotAssigned, "de")] = ("Charakter hinzugefügt", 0x5865F2),

        [(GuildNotificationEventType.RaidSlotUnassigned, "en")] = ("Character removed", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotUnassigned, "fr")] = ("Personnage supprimé", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotUnassigned, "de")] = ("Charakter entfernt", 0x5865F2),

        [(GuildNotificationEventType.RaidSlotsSwapped, "en")] = ("Characters swapped", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotsSwapped, "fr")] = ("Personnages échangés", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotsSwapped, "de")] = ("Charaktere getauscht", 0x5865F2),

        [(GuildNotificationEventType.RaidSlotSpecChanged, "en")] = ("Slot spec changed", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotSpecChanged, "fr")] = ("Spécialisation changée", 0x5865F2),
        [(GuildNotificationEventType.RaidSlotSpecChanged, "de")] = ("Skillung geändert", 0x5865F2),
    };

    public static (string Title, int Color) GetTitleAndColor(GuildNotificationEventType eventType, string language)
        => TitleAndColor.TryGetValue((eventType, language), out var value) ? value : TitleAndColor[(eventType, "en")];

    public static string GetPublishedDescription(string requesterDiscordId, string eventName, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a publié **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat **{eventName}** veröffentlicht.",
        _ => $"<@{requesterDiscordId}> published **{eventName}**.",
    };

    public static string GetCancelledDescription(string requesterDiscordId, string eventName, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a annulé **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat **{eventName}** abgesagt.",
        _ => $"<@{requesterDiscordId}> cancelled **{eventName}**.",
    };

    public static string GetRescheduledDescription(string requesterDiscordId, string eventName, string oldTime, string newTime, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a reprogrammé **{eventName}** : {oldTime} → {newTime}.",
        "de" => $"<@{requesterDiscordId}> hat **{eventName}** verschoben: {oldTime} → {newTime}.",
        _ => $"<@{requesterDiscordId}> rescheduled **{eventName}**: {oldTime} → {newTime}.",
    };

    /// <param name="characterLabel">
    /// Already-formatted (optional class emoji + <c>**bold name**</c>) — built by
    /// <see cref="RaidOps.Application.Implementations.Raids.Services.RaidNotificationContentBuilder"/>,
    /// which alone knows how to resolve the class emoji via <c>IDiscordBotService.Emojis</c>. This
    /// helper stays a pure text formatter with no bot dependency.
    /// </param>
    public static string GetSlotAssignedDescription(string requesterDiscordId, string eventName, string characterLabel, int groupNumber, int slotNumber, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a assigné {characterLabel} (groupe {groupNumber}, slot {slotNumber}) dans **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat {characterLabel} (Gruppe {groupNumber}, Slot {slotNumber}) in **{eventName}** zugewiesen.",
        _ => $"<@{requesterDiscordId}> assigned {characterLabel} (group {groupNumber}, slot {slotNumber}) in **{eventName}**.",
    };

    /// <inheritdoc cref="GetSlotAssignedDescription"/>
    public static string GetSlotUnassignedDescription(string requesterDiscordId, string eventName, string characterLabel, int groupNumber, int slotNumber, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a désassigné {characterLabel} (groupe {groupNumber}, slot {slotNumber}) de **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat {characterLabel} (Gruppe {groupNumber}, Slot {slotNumber}) aus **{eventName}** entfernt.",
        _ => $"<@{requesterDiscordId}> unassigned {characterLabel} (group {groupNumber}, slot {slotNumber}) from **{eventName}**.",
    };

    /// <inheritdoc cref="GetSlotAssignedDescription"/>
    public static string GetSlotsSwappedDescription(string requesterDiscordId, string eventName, string characterALabel, string characterBLabel, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a échangé {characterALabel} et {characterBLabel} dans **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat {characterALabel} und {characterBLabel} in **{eventName}** getauscht.",
        _ => $"<@{requesterDiscordId}> swapped {characterALabel} and {characterBLabel} in **{eventName}**.",
    };

    /// <param name="characterLabel">Class-icon-only (no spec icon — the spec is what's changing, it gets its own before/after icons instead).</param>
    /// <param name="oldSpecLabel">Spec-icon + bold old spec name.</param>
    /// <param name="newSpecLabel">Spec-icon + bold new spec name.</param>
    public static string GetSlotSpecChangedDescription(string requesterDiscordId, string eventName, string characterLabel, string oldSpecLabel, string newSpecLabel, string language) => language switch
    {
        "fr" => $"<@{requesterDiscordId}> a changé la spécialisation de {characterLabel} de {oldSpecLabel} vers {newSpecLabel} dans **{eventName}**.",
        "de" => $"<@{requesterDiscordId}> hat die Skillung von {characterLabel} in **{eventName}** von {oldSpecLabel} zu {newSpecLabel} geändert.",
        _ => $"<@{requesterDiscordId}> changed {characterLabel}'s spec from {oldSpecLabel} to {newSpecLabel} in **{eventName}**.",
    };

    /// <summary>French/German read raid times with a "at"/"um" connector and locale digit-grouping; mirrors <c>AbsenceNotificationText</c>'s per-language time formatting.</summary>
    public static string FormatDateTime(DateTime guildLocalDateTime, string language) => language switch
    {
        "fr" => guildLocalDateTime.ToString("dd/MM/yyyy 'à' HH'h'mm", CultureInfo.InvariantCulture),
        "de" => guildLocalDateTime.ToString("dd.MM.yyyy 'um' HH:mm", CultureInfo.InvariantCulture),
        _ => guildLocalDateTime.ToString("M/d/yyyy 'at' HH:mm", CultureInfo.InvariantCulture),
    };
}
