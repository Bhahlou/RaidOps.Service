namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>
/// A rich Discord embed to post via <see cref="IMessageService.SendEmbedAsync"/>. Decoupled from
/// the NetCord SDK's own embed types so callers in the Application layer don't need a package
/// reference on NetCord just to build a notification message. Generic on purpose — every event
/// family (absences today, raids later) builds its own content but shares this same envelope.
/// </summary>
/// <param name="Title">Embed title.</param>
/// <param name="Description">Optional embed body text. Supports Discord markdown, including user mentions (<c>&lt;@discordId&gt;</c>).</param>
/// <param name="ColorHex">Optional accent color as a 24-bit RGB value (e.g. <c>0xFFB74D</c>). Falls back to the embed's default color when <c>null</c>.</param>
/// <param name="Fields">Optional key/value fields rendered as a grid under the description.</param>
/// <param name="FooterText">Optional footer text.</param>
/// <param name="Url">Optional URL that turns the title into a link — e.g. a deep link to the raid/event in RaidOps.</param>
/// <param name="Author">Optional small byline shown above the title (e.g. the member who triggered the event, with their avatar).</param>
/// <param name="Buttons">Optional row of buttons attached alongside the embed (e.g. the raid signup-call's Accept/Tentative/Decline row) — a Discord message-level concern, not part of the embed itself.</param>
public record DiscordEmbedContent(
    string Title,
    string? Description = null,
    int? ColorHex = null,
    IReadOnlyList<DiscordEmbedField>? Fields = null,
    string? FooterText = null,
    string? Url = null,
    DiscordEmbedAuthor? Author = null,
    IReadOnlyList<DiscordEmbedButton>? Buttons = null);

/// <summary>A single button attached to a <see cref="DiscordEmbedContent"/>'s message.</summary>
/// <param name="Label">Button text.</param>
/// <param name="CustomId">Opaque identifier round-tripped back on click — see the interaction module handling it for its encoding scheme.</param>
/// <param name="Style">Visual style.</param>
public record DiscordEmbedButton(string Label, string CustomId, DiscordEmbedButtonStyle Style = DiscordEmbedButtonStyle.Secondary);

/// <summary>Mirrors NetCord's own button style enum, kept separate so this layer stays SDK-independent.</summary>
public enum DiscordEmbedButtonStyle
{
    Primary,
    Secondary,
    Success,
    Danger,
}

/// <summary>A single name/value field of a <see cref="DiscordEmbedContent"/>.</summary>
/// <param name="Name">Field label.</param>
/// <param name="Value">Field content.</param>
/// <param name="Inline">Whether this field can sit side-by-side with adjacent inline fields (Discord fits up to 3 per row) instead of always taking the full width.</param>
public record DiscordEmbedField(string Name, string Value, bool Inline = false);

/// <summary>The small byline (name + icon) shown above a <see cref="DiscordEmbedContent"/>'s title.</summary>
/// <param name="Name">Author display name.</param>
/// <param name="IconUrl">Optional small icon shown next to the name (e.g. a Discord avatar URL).</param>
public record DiscordEmbedAuthor(string Name, string? IconUrl = null);
