using RaidOps.Application.Contracts.Services;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Services;

/// <inheritdoc cref="IActiveRosterBranchResolver"/>
public class ActiveRosterBranchResolver(
    ICharacterRepository characterRepository,
    IGuildMembershipRepository guildMembershipRepository) : IActiveRosterBranchResolver
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveRosterBranch>> GetActiveBranchesAsync(string userDiscordId, CancellationToken cancellationToken = default)
    {
        var activeCharacterIds = (await characterRepository.GetByUserWithDetailsAsync(userDiscordId, activeOnly: true, cancellationToken))
            .Select(c => c.Id)
            .ToList();

        if (activeCharacterIds.Count == 0)
            return [];

        var memberships = await guildMembershipRepository.GetByCharacterIdsAsync(activeCharacterIds, cancellationToken);

        return [.. memberships
            .Select(m => new ActiveRosterBranch(m.GuildId, m.GuildBranchId))
            .Distinct()];
    }
}
