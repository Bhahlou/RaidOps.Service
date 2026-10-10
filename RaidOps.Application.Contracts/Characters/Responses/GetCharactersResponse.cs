namespace RaidOps.Application.Contracts.Characters.Responses;

/// <summary>
/// Response for <c>GET /api/v1/characters</c> — bundles the user's linked Battle.net accounts
/// alongside their characters so the front end can load everything for the page in one request.
/// </summary>
public class GetCharactersResponse
{
    /// <summary>The user's linked Battle.net accounts. Empty if none are linked yet.</summary>
    public List<BnetAccountResponse> BnetAccounts { get; set; } = [];

    /// <summary>The user's imported characters.</summary>
    public List<CharacterDto> Characters { get; set; } = [];
}
