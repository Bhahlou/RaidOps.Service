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
public record DiscordEmbedContent(
    string Title,
    string? Description = null,
    int? ColorHex = null,
    IReadOnlyList<DiscordEmbedField>? Fields = null,
    string? FooterText = null,
    string? Url = null,
    DiscordEmbedAuthor? Author = null);

/// <summary>A single name/value field of a <see cref="DiscordEmbedContent"/>.</summary>
/// <param name="Name">Field label.</param>
/// <param name="Value">Field content.</param>
public record DiscordEmbedField(string Name, string Value);

/// <summary>The small byline (name + icon) shown above a <see cref="DiscordEmbedContent"/>'s title.</summary>
/// <param name="Name">Author display name.</param>
/// <param name="IconUrl">Optional small icon shown next to the name (e.g. a Discord avatar URL).</param>
public record DiscordEmbedAuthor(string Name, string? IconUrl = null);
