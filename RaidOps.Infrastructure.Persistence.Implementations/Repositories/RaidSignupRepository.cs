using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Infrastructure.Persistence.Implementations.Repositories;

/// <summary>EF Core implementation of <see cref="IRaidSignupRepository"/>.</summary>
public class RaidSignupRepository(RaidOpsDbContext context) : IRaidSignupRepository
{
    /// <inheritdoc/>
    public async Task<RaidSignup?> GetAsync(int raidEventId, string userDiscordId, CancellationToken cancellationToken = default)
        => await context.RaidSignups
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.RaidEventId == raidEventId && s.UserDiscordId == userDiscordId, cancellationToken);

    /// <inheritdoc/>
    public async Task SetSignupAsync(RaidSignup signup, CancellationToken cancellationToken = default)
    {
        var existing = await context.RaidSignups
            .FirstOrDefaultAsync(s => s.RaidEventId == signup.RaidEventId && s.UserDiscordId == signup.UserDiscordId, cancellationToken);

        if (existing is null)
        {
            context.RaidSignups.Add(signup);
        }
        else
        {
            existing.Status = signup.Status;
            existing.CharacterId = signup.CharacterId;
            existing.SpecId = signup.SpecId;
            existing.RespondedAtUtc = signup.RespondedAtUtc;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<RaidSignup>> GetForEventAsync(int raidEventId, CancellationToken cancellationToken = default)
        => await context.RaidSignups
            .Where(s => s.RaidEventId == raidEventId)
            .Include(s => s.Character).ThenInclude(c => c!.Class)
            .Include(s => s.Spec)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<List<RaidSignup>> GetForEventsAsync(IEnumerable<int> raidEventIds, CancellationToken cancellationToken = default)
    {
        var idList = raidEventIds.ToList();
        return await context.RaidSignups
            .Where(s => idList.Contains(s.RaidEventId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
