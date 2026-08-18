using Microsoft.Extensions.Configuration;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Notifications.Helpers;
using RaidOps.Application.Implementations.Raids.Helpers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc cref="IRaidNotificationContentBuilder"/>
public class RaidNotificationContentBuilder(
    IGuildsRepository guildsRepository,
    IGuildBranchesRepository guildBranchesRepository,
    IBranchRepository branchRepository,
    IDiscordBotService discordBotService,
    IConfiguration configuration) : IRaidNotificationContentBuilder
{
    /// <summary>
    /// Base URL of the front end, used to deep-link the composition announcement's title to the
    /// raid on the site — same config key <c>DiscordAuthController</c>/<c>GuildRegistrationController</c>
    /// already read. Unlike those, missing/blank here just means no link (<see cref="BuildCompositionAnnouncementAsync"/>
    /// still posts the embed) rather than throwing — a notification should never fail to send over a cosmetic link.
    /// </summary>
    private readonly string? _frontendUrl = configuration["FrontendUrl"];


    /// <inheritdoc/>
    public async Task<string> GetGuildLanguageAsync(string guildId, CancellationToken cancellationToken = default)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        return guild?.Language ?? "en";
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPublishedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetPublishedDescription(requesterDiscordId, raidEvent.Name, language);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);

        return BuildEmbed(GuildNotificationEventType.RaidPublished, language, description, guildId, requesterDiscordId,
            [new DiscordEmbedField("Starts", startsAt)], cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildCancelledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetCancelledDescription(requesterDiscordId, raidEvent.Name, language);

        return BuildEmbed(GuildNotificationEventType.RaidCancelled, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildRescheduledAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, DateTime oldStartsAtUtc, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var oldTime = RaidNotificationText.DiscordTimestamp(oldStartsAtUtc);
        var newTime = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var description = RaidNotificationText.GetRescheduledDescription(requesterDiscordId, raidEvent.Name, oldTime, newTime, language);

        return BuildEmbed(GuildNotificationEventType.RaidRescheduled, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotAssignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotAssignedDescription(requesterDiscordId, raidEvent.Name, CharacterLabel(character), slot.GroupNumber, slot.SlotNumber, language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotAssigned, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotUnassignedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, SlotCoordinate slot, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotUnassignedDescription(requesterDiscordId, raidEvent.Name, CharacterLabel(character), slot.GroupNumber, slot.SlotNumber, language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotUnassigned, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotsSwappedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef characterA, RaidCharacterRef characterB, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotsSwappedDescription(
            requesterDiscordId, raidEvent.Name, CharacterLabel(characterA), CharacterLabel(characterB), language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotsSwapped, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSlotSpecChangedAsync(string guildId, string requesterDiscordId, RaidEvent raidEvent, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var description = RaidNotificationText.GetSlotSpecChangedDescription(
            requesterDiscordId, raidEvent.Name, CharacterLabel(character), SpecLabel(oldSpecName, character.ClassId), SpecLabel(newSpecName, character.ClassId), language);

        return BuildEmbed(GuildNotificationEventType.RaidSlotSpecChanged, language, description, guildId, requesterDiscordId, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildCompositionAnnouncementAsync(string guildId, RaidEvent raidEvent, IReadOnlyList<RaidSlotAssignment> assignments, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var description = RaidNotificationText.GetCompositionAnnouncementDescription(startsAt, language);

        var byGroup = assignments.ToLookup(a => a.GroupNumber);
        var fields = new List<DiscordEmbedField>();
        for (var groupNumber = 1; groupNumber <= raidEvent.GroupCount; groupNumber++)
        {
            var slots = byGroup[groupNumber].ToDictionary(a => a.SlotNumber);
            var lines = new List<string>(raidEvent.SlotsPerGroup);
            for (var slotNumber = 1; slotNumber <= raidEvent.SlotsPerGroup; slotNumber++)
            {
                var line = slots.TryGetValue(slotNumber, out var assignment)
                    ? CompositionCharacterLabel(new RaidCharacterRef(assignment.Character.Name, assignment.Character.ClassId, assignment.Spec.Name))
                    : "-";
                lines.Add(line);
            }

            // Inline: Discord renders up to 3 inline fields per row, wrapping to the next line
            // after that — with a typical 5-group raid grid that's a 3-then-2 layout, matching the
            // Raid-Helper-style grid this was modeled after.
            fields.Add(new DiscordEmbedField(RaidNotificationText.GetGroupLabel(groupNumber, language), string.Join('\n', lines), Inline: true));
        }

        // Discord sizes each row's inline columns independently — a trailing row with fewer than 3
        // fields gets wider columns than the 3-column rows above it, so its content visibly doesn't
        // line up underneath them. Pad it out to 3 with invisible fields (zero-width space — Discord
        // rejects an actually-empty field name/value) so every row shares the same column widths.
        var padding = (3 - fields.Count % 3) % 3;
        for (var i = 0; i < padding; i++)
            fields.Add(new DiscordEmbedField("\u200B", "\u200B", Inline: true));

        var (_, color) = RaidNotificationText.GetTitleAndColor(GuildNotificationEventType.RaidCompositionAnnouncementPosted, language);
        var url = BuildRaidEventUrl(raidEvent);

        return new DiscordEmbedContent(
            Title: raidEvent.Name,
            Description: description,
            ColorHex: color,
            Fields: fields,
            Url: url,
            Author: null);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildSignupCallAsync(string guildId, int guildBranchId, RaidEvent raidEvent, IReadOnlyList<RaidSignupResponse> signups, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);

        var acceptedCount = signups.Count(s => s.Status == SignupStatus.Accepted);
        var tentativeCount = signups.Count(s => s.Status == SignupStatus.Tentative);
        var declinedCount = signups.Count(s => s.Status == SignupStatus.Declined);

        // One field per WoW class available on this raid's branch (e.g. no Monk/Death Knight/Demon
        // Hunter/Evoker columns on a Classic Era branch), always shown — blank when nobody's signed
        // up as that class (yet), rather than hiding the column, so the grid's shape stays stable as
        // people respond. This embed is purely about "who's coming as what," never group/slot
        // composition, which stays the composition announcement's job and only exists once an
        // officer actually builds the raid.
        var currentExpansionId = await ResolveCurrentExpansionIdAsync(raidEvent.GuildBranchId, cancellationToken);
        var acceptedByClass = signups
            .Where(s => s.Status == SignupStatus.Accepted && s.ClassId is not null)
            .ToLookup(s => s.ClassId!.Value);

        var fields = new List<DiscordEmbedField>();
        foreach (var (classId, className) in WowClassNames.ByClassId)
        {
            if (WowClassAvailability.FirstExpansionIdByClassId.GetValueOrDefault(classId, 1) > currentExpansionId)
                continue;

            var names = acceptedByClass[classId]
                .OrderBy(s => s.CharacterName ?? s.PlayerName ?? s.UserDiscordId, StringComparer.OrdinalIgnoreCase)
                .Select(SignupCharacterLabel)
                .ToList();
            var classEmoji = ClassEmoji(classId);
            var value = names.Count == 0 ? "-" : string.Join('\n', names);
            fields.Add(new DiscordEmbedField($"{classEmoji}{className}".TrimStart(), value, Inline: true));
        }

        // Two per row (not three) - with every class always shown, most cells are short ("-" or one
        // name), and Discord's three-inline-column width was cramming the header/name text against
        // the next column. Two gives each column noticeably more breathing room. Discord sizes each
        // row's inline columns independently - a trailing row with fewer than 2 fields gets a wider
        // column than the rows above it, so pad to a multiple of 2 with invisible fields (zero-width
        // space - Discord rejects an actually-empty field name/value) so every row lines up (same
        // trick as BuildCompositionAnnouncementAsync, just base 2 instead of base 3 here).
        var padding = (2 - fields.Count % 2) % 2;
        for (var i = 0; i < padding; i++)
            fields.Add(new DiscordEmbedField("\u200B", "\u200B", Inline: true));

        // Full-width, non-inline separator so Tentative/Declined visually break away from the class grid.
        fields.Add(new DiscordEmbedField("\u200B", "\u200B", Inline: false));

        var tentativeNames = signups
            .Where(s => s.Status == SignupStatus.Tentative)
            .OrderBy(s => s.CharacterName ?? s.PlayerName ?? s.UserDiscordId, StringComparer.OrdinalIgnoreCase)
            .Select(SignupCharacterLabel)
            .ToList();
        fields.Add(new DiscordEmbedField(
            $"{RaidNotificationText.GetSignupCallStatusLabel(SignupStatus.Tentative, language)} ({tentativeNames.Count})",
            tentativeNames.Count == 0 ? "-" : string.Join('\n', tentativeNames),
            Inline: false));

        // Declined never commits a character — just the player's name, no class/spec to show.
        var declinedNames = signups
            .Where(s => s.Status == SignupStatus.Declined)
            .Select(s => s.PlayerName ?? s.UserDiscordId)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        fields.Add(new DiscordEmbedField(
            $"{RaidNotificationText.GetSignupCallStatusLabel(SignupStatus.Declined, language)} ({declinedNames.Count})",
            declinedNames.Count == 0 ? "-" : string.Join('\n', declinedNames),
            Inline: false));

        var description = RaidNotificationText.GetSignupCallDescription(
            raidEvent.StartsAtUtc, raidEvent.CreatedByDiscordId, acceptedCount, tentativeCount, declinedCount, language);

        var (_, color) = RaidNotificationText.GetTitleAndColor(GuildNotificationEventType.RaidSignupCallPosted, language);
        var buttons = new[]
        {
            new DiscordEmbedButton(RaidNotificationText.GetSignupCallStatusLabel(SignupStatus.Accepted, language), $"raidsignup:{guildBranchId}:{raidEvent.Id}:accepted", DiscordEmbedButtonStyle.Success),
            new DiscordEmbedButton(RaidNotificationText.GetSignupCallStatusLabel(SignupStatus.Tentative, language), $"raidsignup:{guildBranchId}:{raidEvent.Id}:tentative", DiscordEmbedButtonStyle.Secondary),
            new DiscordEmbedButton(RaidNotificationText.GetSignupCallStatusLabel(SignupStatus.Declined, language), $"raidsignup:{guildBranchId}:{raidEvent.Id}:declined", DiscordEmbedButtonStyle.Danger),
        };

        return new DiscordEmbedContent(
            Title: raidEvent.Name,
            Description: description,
            ColorHex: color,
            Fields: fields,
            Url: BuildRaidEventUrl(raidEvent),
            Author: null,
            Buttons: buttons);
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPlayerAddedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, bool isInitialPublish, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var (title, color) = isInitialPublish
            ? RaidNotificationText.GetRaidPublishedDmTitleAndColor(raidEvent.Name, language)
            : RaidNotificationText.GetPlayerAddedDmTitleAndColor(language);
        var description = RaidNotificationText.GetPlayerCompositionDmDescription(raidEvent.Name, startsAt, CompositionCharacterLabel(character), added: true, language);

        return new DiscordEmbedContent(Title: title, Description: description, ColorHex: color, Url: BuildRaidEventUrl(raidEvent));
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPlayerRemovedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var (title, color) = RaidNotificationText.GetPlayerRemovedDmTitleAndColor(language);
        var description = RaidNotificationText.GetPlayerCompositionDmDescription(raidEvent.Name, startsAt, CompositionCharacterLabel(character), added: false, language);

        return new DiscordEmbedContent(Title: title, Description: description, ColorHex: color, Url: BuildRaidEventUrl(raidEvent));
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildPlayerSpecChangedDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, string oldSpecName, string newSpecName, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var (title, color) = RaidNotificationText.GetPlayerSpecChangedDmTitleAndColor(language);
        var characterLabel = CompositionCharacterLabel(character with { SpecName = null });
        var description = RaidNotificationText.GetPlayerSpecChangedDmDescription(
            raidEvent.Name, startsAt, characterLabel, SpecLabel(oldSpecName, character.ClassId), SpecLabel(newSpecName, character.ClassId), language);

        return new DiscordEmbedContent(Title: title, Description: description, ColorHex: color, Url: BuildRaidEventUrl(raidEvent));
    }

    /// <inheritdoc/>
    public async Task<DiscordEmbedContent> BuildRaidCancelledDmAsync(string guildId, RaidEvent raidEvent, RaidCharacterRef character, CancellationToken cancellationToken = default)
    {
        var (_, language) = await ResolveGuildAsync(guildId, cancellationToken);
        var startsAt = RaidNotificationText.DiscordTimestamp(raidEvent.StartsAtUtc);
        var (title, color) = RaidNotificationText.GetRaidCancelledDmTitleAndColor(language);
        var description = RaidNotificationText.GetRaidCancelledDmDescription(raidEvent.Name, startsAt, CompositionCharacterLabel(character), language);

        return new DiscordEmbedContent(Title: title, Description: description, ColorHex: color, Url: BuildRaidEventUrl(raidEvent));
    }

    /// <inheritdoc/>
    public string? BuildRaidEventUrl(RaidEvent raidEvent) =>
        string.IsNullOrEmpty(_frontendUrl) ? null : $"{_frontendUrl}/guilds/{raidEvent.GuildId}/{raidEvent.GuildBranchId}/raids/{raidEvent.Id}";

    /// <summary>
    /// <c>"{class emoji}{spec emoji} **{name}**"</c> — either icon is silently dropped (never the
    /// whole label) when its class/spec is unknown or its emoji hasn't synced on this bot yet
    /// (<see cref="IEmojiService.GetMarkdown"/> returns <c>null</c> rather than throwing — an icon
    /// is a nice-to-have, never a reason to fail the notification). <see cref="RaidCharacterRef.SpecName"/>
    /// is left <c>null</c> by <see cref="BuildSlotSpecChangedAsync"/>, which shows the spec's own
    /// before/after icons instead of one next to the character.
    /// </summary>
    private string CharacterLabel(RaidCharacterRef character)
    {
        var classEmoji = character.ClassId is { } classId && WowClassEmojiNames.ByClassId.TryGetValue(classId, out var className)
            ? discordBotService.Emojis.GetMarkdown(className)
            : null;
        var specEmoji = character.ClassId is { } cid && character.SpecName is { } specName
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(cid, specName))
            : null;

        var icons = $"{classEmoji}{specEmoji}";
        return icons.Length == 0 ? $"**{character.Name}**" : $"{icons} **{character.Name}**";
    }

    /// <summary>
    /// <c>"{spec emoji} **{name}**"</c> — spec icon only, no class icon, for the composition
    /// announcement's grid (deliberately more compact than <see cref="CharacterLabel"/>'s
    /// per-change notifications, where both icons help scan a single-line diff).
    /// </summary>
    private string CompositionCharacterLabel(RaidCharacterRef character)
    {
        var specEmoji = character.ClassId is { } cid && character.SpecName is { } specName
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(cid, specName))
            : null;

        return specEmoji is null ? $"**{character.Name}**" : $"{specEmoji} **{character.Name}**";
    }

    /// <summary>
    /// <c>"{spec emoji} {name}"</c> — spec icon (when resolvable) prefixed to an accepted signup's
    /// character name; falls back to the player's Discord name/ID when no character was resolved
    /// (shouldn't happen for an Accepted response, but mirrors the other name fallbacks in this
    /// file). Unlike <see cref="CharacterLabel"/> the name isn't bolded — this is a plain roster
    /// list, not a composition grid.
    /// </summary>
    private string SignupCharacterLabel(RaidSignupResponse signup)
    {
        var name = signup.CharacterName ?? signup.PlayerName ?? signup.UserDiscordId;
        var specEmoji = signup.ClassId is { } classId && signup.SpecName is { } specName
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(classId, specName))
            : null;

        return specEmoji is null ? name : $"{specEmoji} {name}";
    }

    /// <summary>
    /// The expansion ID a raid's guild branch is currently on (drives <see cref="WowClassAvailability"/>
    /// filtering) — falls back to <see cref="int.MaxValue"/> (show every class) rather than throwing
    /// or hiding every column if either lookup somehow misses, same "never fail a notification over
    /// a cosmetic detail" rationale as the emoji fallbacks in this file.
    /// </summary>
    private async Task<int> ResolveCurrentExpansionIdAsync(int guildBranchId, CancellationToken cancellationToken)
    {
        var guildBranch = await guildBranchesRepository.GetByIdAsync(guildBranchId, cancellationToken);
        if (guildBranch is null)
            return int.MaxValue;

        var branch = await branchRepository.GetByIdAsync(guildBranch.BranchId, cancellationToken);
        return branch?.CurrentExpansionId ?? int.MaxValue;
    }

    /// <summary><c>"{class emoji} "</c> (trailing space, so the caller can safely concatenate), or an empty string when the icon isn't resolvable/synced yet — used for the signup call's per-class field titles.</summary>
    internal string ClassEmoji(int classId)
    {
        var emoji = WowClassEmojiNames.ByClassId.TryGetValue(classId, out var className)
            ? discordBotService.Emojis.GetMarkdown(className)
            : null;

        return emoji is null ? string.Empty : $"{emoji} ";
    }

    /// <summary><c>"{spec emoji} **{specName}**"</c>, or just <c>"**{specName}**"</c> when the icon isn't resolvable — same fallback rationale as <see cref="CharacterLabel"/>.</summary>
    private string SpecLabel(string specName, int? characterClassId)
    {
        var emoji = characterClassId is { } classId
            ? discordBotService.Emojis.GetMarkdown(WowSpecEmojiNames.GetName(classId, specName))
            : null;

        return emoji is null ? $"**{specName}**" : $"{emoji} **{specName}**";
    }

    private async Task<(Guild? Guild, string Language)> ResolveGuildAsync(string guildId, CancellationToken cancellationToken)
    {
        var guild = await guildsRepository.GetByIdAsync(guildId, cancellationToken);
        return (guild, guild?.Language ?? "en");
    }

    private DiscordEmbedContent BuildEmbed(
        GuildNotificationEventType eventType,
        string language,
        string description,
        string guildId,
        string requesterDiscordId,
        IReadOnlyList<DiscordEmbedField>? fields,
        CancellationToken cancellationToken)
    {
        var (title, color) = RaidNotificationText.GetTitleAndColor(eventType, language);
        var author = DiscordEmbedAuthorResolver.Resolve(discordBotService, guildId, requesterDiscordId, cancellationToken);

        return new DiscordEmbedContent(
            Title: title,
            Description: description,
            ColorHex: color,
            Fields: fields,
            Author: author);
    }
}
