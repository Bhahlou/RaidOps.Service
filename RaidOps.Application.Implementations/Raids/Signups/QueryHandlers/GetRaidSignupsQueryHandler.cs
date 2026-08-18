using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Queries;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Signups.QueryHandlers;

/// <inheritdoc cref="GetRaidSignupsQuery"/>
public class GetRaidSignupsQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidEventRepository raidEventRepository,
    IGuildMembershipRepository guildMembershipRepository,
    IRaidSignupRepository raidSignupRepository,
    IUsersRepository usersRepository) : IQueryHandlerAsync<GetRaidSignupsQuery, List<RaidSignupResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<RaidSignupResponse>>> HandleAsync(GetRaidSignupsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel < GuildAccessLevel.Roster)
            return Result<List<RaidSignupResponse>>.Fail(ResponseDetail.Forbidden, "User is not on this guild branch's roster.");

        var raidEvent = await raidEventRepository.GetByIdAsync(query.EventId, query.GuildBranchId, cancellationToken);
        if (raidEvent == null)
            return Result<List<RaidSignupResponse>>.Fail(ResponseDetail.RaidEventNotFound, $"Raid event '{query.EventId}' does not exist.");

        var rosterMemberships = await guildMembershipRepository.GetByGuildBranchIdAsync(query.GuildBranchId, cancellationToken);
        var rosterPlayerIds = rosterMemberships.Select(m => m.Character.UserDiscordId).Distinct().ToList();

        var signups = await raidSignupRepository.GetForEventAsync(query.EventId, cancellationToken);
        var signupsByPlayer = signups.ToDictionary(s => s.UserDiscordId);

        var players = await usersRepository.FindAsync(u => rosterPlayerIds.Contains(u.DiscordId), cancellationToken);
        var playersById = players.ToDictionary(u => u.DiscordId);

        var response = rosterPlayerIds
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
                    SpecIconUrl = signup?.Spec?.IconUrl,
                };
            })
            .OrderBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<List<RaidSignupResponse>>.Ok(response);
    }
}
