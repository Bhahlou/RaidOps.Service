using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Domain.Models.Raids;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidEventRepository"/>'s own defensive not-found guards in
/// <see cref="IRaidEventRepository.UpdateAsync"/> and <see cref="IRaidEventRepository.DeleteAsync"/>.
/// Both are unreachable through <c>RaidsController</c>'s handlers, which already re-fetch the event
/// via <c>GetByIdAsync</c> and fail fast before ever calling into these — so only a direct
/// repository call exercises the repository's own guard rather than the handler's.
/// </summary>
[Collection("Integration")]
public class RaidEventRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task UpdateAsync_EventNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var result = await repo.UpdateAsync(
                new RaidEvent { Id = -1, Name = "Ghost event", GroupCount = 2, SlotsPerGroup = 5 },
                guildBranchId: -1,
                raidZoneIds: [1]);

            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DeleteAsync_EventNotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidEventRepository>();
            var result = await repo.DeleteAsync(id: -1, guildBranchId: -1);

            result.Should().BeFalse();
        }
    }
}
