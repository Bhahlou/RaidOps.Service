using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Services;

/// <inheritdoc/>
public class RaidSignupResponseBuilder(
    IRaidSignupRepository raidSignupRepository,
    IUsersRepository usersRepository,
    IGuildMembershipRepository guildMembershipRepository) : IRaidSignupResponseBuilder
{
    /// <inheritdoc/>
    public async Task<List<RaidSignupResponse>> BuildAsync(RaidEvent raidEvent, CancellationToken cancellationToken = default)
    {
        var rosterMemberships = await guildMembershipRepository.GetByGuildBranchIdAsync(raidEvent.GuildBranchId, cancellationToken);
        var rosterPlayerIds = rosterMemberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();

        var signups = await raidSignupRepository.GetForEventAsync(raidEvent.Id, cancellationToken);
        var signupsByPlayer = signups.ToDictionary(s => s.UserDiscordId);

        var players = await usersRepository.FindAsync(u => rosterPlayerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        return [.. rosterPlayerIds
            .Select(playerId =>
            {
                signupsByPlayer.TryGetValue(playerId, out var signup);
                playersById.TryGetValue(playerId, out var player);
                return new RaidSignupResponse
                {
                    UserDiscordId = playerId,
                    PlayerName = player?.Name,
                    Status = signup?.Status,
                    RespondedAtUtc = signup?.RespondedAtUtc,
                    CharacterId = signup?.CharacterId,
                    CharacterName = signup?.Character?.Name,
                    ClassId = signup?.Character?.ClassId,
                    ClassName = signup?.Character?.Class.Name,
                    SpecId = signup?.SpecId,
                    SpecName = signup?.Spec?.Name,
                };
            })];
    }
}
