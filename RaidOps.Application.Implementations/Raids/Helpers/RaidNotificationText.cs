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

        [(GuildNotificationEventType.RaidCompositionAnnouncementPosted, "en")] = ("Current composition", 0x5865F2),
        [(GuildNotificationEventType.RaidCompositionAnnouncementPosted, "fr")] = ("Composition actuelle", 0x5865F2),
        [(GuildNotificationEventType.RaidCompositionAnnouncementPosted, "de")] = ("Aktuelle Zusammensetzung", 0x5865F2),
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

    /// <summary>
    /// Discord's native <c>&lt;t:unix:format&gt;</c> markup — the client renders it in each
    /// reader's own local timezone/locale, so unlike a pre-formatted string this needs no
    /// guild-timezone conversion or per-language formatting on our end. <paramref name="format"/>
    /// defaults to <c>F</c> ("long date/time", e.g. "Tuesday, 1 February 2026 21:05").
    /// </summary>
    public static string DiscordTimestamp(DateTime utc, char format = 'F') =>
        $"<t:{new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds()}:{format}>";

    public static string GetCompositionAnnouncementDescription(string startsAt, string language) => language switch
    {
        "fr" => $"{startsAt} · mis à jour automatiquement.",
        "de" => $"{startsAt} · wird automatisch aktualisiert.",
        _ => $"{startsAt} · updated automatically.",
    };

    public static string GetGroupLabel(int groupNumber, string language) => language switch
    {
        "fr" => $"Groupe {groupNumber} :",
        "de" => $"Gruppe {groupNumber}:",
        _ => $"Group {groupNumber}:",
    };

    public static (string Title, int Color) GetPlayerAddedDmTitleAndColor(string language) => language switch
    {
        "fr" => ("Ajouté au raid", 0x57F287),
        "de" => ("Zum Raid hinzugefügt", 0x57F287),
        _ => ("Added to the raid", 0x57F287),
    };

    public static (string Title, int Color) GetPlayerRemovedDmTitleAndColor(string language) => language switch
    {
        "fr" => ("Retiré du raid", 0xED4245),
        "de" => ("Vom Raid entfernt", 0xED4245),
        _ => ("Removed from the raid", 0xED4245),
    };

    /// <summary>
    /// Title/color for the "added" DM specifically when it's sent because the raid was just
    /// published with the player already in its roster from the draft phase — distinct from
    /// <see cref="GetPlayerAddedDmTitleAndColor"/> (used when a slot assignment adds them to an
    /// already-published raid), since "you were added" reads oddly for someone who's been on the
    /// roster the whole time; "the raid went live" is what actually happened for them.
    /// </summary>
    public static (string Title, int Color) GetRaidPublishedDmTitleAndColor(string eventName, string language) => language switch
    {
        "fr" => ($"{eventName} publié", 0x57F287),
        "de" => ($"{eventName} veröffentlicht", 0x57F287),
        _ => ($"{eventName} published", 0x57F287),
    };

    /// <summary>
    /// Shared body for both composition DMs — identical shape (event, time, character) for added
    /// and removed alike, only the verb differs, so the two can never drift out of sync.
    /// </summary>
    public static string GetPlayerCompositionDmDescription(string eventName, string startsAt, string characterLabel, bool added, string language) => language switch
    {
        "fr" => $"Tu as été {(added ? "ajouté à" : "retiré de")} **{eventName}** ({startsAt}) avec {characterLabel}.",
        "de" => $"Du wurdest mit {characterLabel} {(added ? "zu" : "von")} **{eventName}** ({startsAt}) {(added ? "hinzugefügt" : "entfernt")}.",
        _ => $"You've been {(added ? "added to" : "removed from")} **{eventName}** ({startsAt}) with {characterLabel}.",
    };

    public static (string Title, int Color) GetPlayerSpecChangedDmTitleAndColor(string language) => language switch
    {
        "fr" => ("Spécialisation changée", 0xFEE75C),
        "de" => ("Skillung geändert", 0xFEE75C),
        _ => ("Spec changed", 0xFEE75C),
    };

    public static string GetPlayerSpecChangedDmDescription(string eventName, string startsAt, string characterLabel, string oldSpecLabel, string newSpecLabel, string language) => language switch
    {
        "fr" => $"Ta spécialisation pour **{eventName}** ({startsAt}) a changé sur {characterLabel} : {oldSpecLabel} → {newSpecLabel}.",
        "de" => $"Deine Skillung für **{eventName}** ({startsAt}) wurde auf {characterLabel} geändert: {oldSpecLabel} → {newSpecLabel}.",
        _ => $"Your spec for **{eventName}** ({startsAt}) changed on {characterLabel}: {oldSpecLabel} → {newSpecLabel}.",
    };

    /// <summary>
    /// Sent unconditionally (see <see cref="RaidOps.Application.Contracts.Services.IRaidCompositionAnnouncementService.NotifyPlayerRaidCancelledAsync"/>)
    /// — deliberately not folded into <see cref="GetPlayerCompositionDmDescription"/>'s
    /// added/removed shape, since this is a guaranteed safety-net message, not an opt-in one.
    /// </summary>
    public static (string Title, int Color) GetRaidCancelledDmTitleAndColor(string language) => language switch
    {
        "fr" => ("Raid annulé", 0xED4245),
        "de" => ("Raid abgesagt", 0xED4245),
        _ => ("Raid cancelled", 0xED4245),
    };

    public static string GetRaidCancelledDmDescription(string eventName, string startsAt, string characterLabel, string language) => language switch
    {
        "fr" => $"Le raid **{eventName}** ({startsAt}) auquel tu participais avec {characterLabel} a été annulé.",
        "de" => $"Der Raid **{eventName}** ({startsAt}), an dem du mit {characterLabel} teilgenommen hast, wurde abgesagt.",
        _ => $"The raid **{eventName}** ({startsAt}) you were in with {characterLabel} has been cancelled.",
    };

    /// <summary>A one-off plain-text ping (not an embed) — <paramref name="mentions"/> is the space-separated <c>&lt;@id&gt;</c> list built by the caller.</summary>
    /// <summary>
    /// The <c>/w ... inv</c> part is a literal WoW client whisper command, kept identical across
    /// locales — only the "grouping in progress" phrase around it is translated.
    /// </summary>
    public static string GetGroupingPingMessage(string mentions, string eventName, string characterName, string language) => language switch
    {
        "fr" => $"{mentions}\n**{eventName}** - groupage en cours. /w {characterName} inv",
        "de" => $"{mentions}\n**{eventName}** - Gruppierung läuft. /w {characterName} inv",
        _ => $"{mentions}\n**{eventName}** - grouping in progress. /w {characterName} inv",
    };
}
