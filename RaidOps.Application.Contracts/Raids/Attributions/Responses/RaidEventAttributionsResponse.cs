namespace RaidOps.Application.Contracts.Raids.Attributions.Responses;

/// <summary>
/// Everything the raid event's Assignments tab needs in one payload: the guild's template rows,
/// this event's existing fills, and the pool of characters the fill picker may offer (those
/// already seated in this event's slot grid).
/// </summary>
public class RaidEventAttributionsResponse
{
    /// <summary>The guild's raid-attribution template, ordered by <c>SortOrder</c>.</summary>
    public required List<GuildAttributionDefinitionResponse> Definitions { get; set; }

    /// <summary>This event's existing fills.</summary>
    public required List<RaidEventAttributionFillResponse> Fills { get; set; }

    /// <summary>Characters seated in this event — the fill picker's candidate pool.</summary>
    public required List<SeatedCharacterResponse> SeatedCharacters { get; set; }
}
