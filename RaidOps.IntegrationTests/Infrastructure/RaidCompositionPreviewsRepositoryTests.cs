using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="RaidCompositionPreviewsRepository"/> members not already
/// exercised end-to-end through <c>RaidCompositionPreviewsController</c> —
/// <see cref="IRaidCompositionPreviewsRepository.DeleteAsync"/>'s own "not found" branch is
/// unreachable through the controller, whose handler already re-fetches and fails fast before
/// ever calling into it (same shape as <see cref="RaidEventRepositoryTests"/>).
/// </summary>
[Collection("Integration")]
public class RaidCompositionPreviewsRepositoryTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        var (scope, _) = CreateDbScope();
        using (scope)
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRaidCompositionPreviewsRepository>();
            var result = await repo.DeleteAsync(id: -1, guildBranchId: -1);

            result.Should().BeFalse();
        }
    }
}
