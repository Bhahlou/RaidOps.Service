using FluentAssertions;
using RaidOps.Application.Contracts.Branches.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GET /api/v1/wowbranches.
/// Validates auth enforcement and that seeded reference data is returned correctly.
/// </summary>
public class WowBranchesControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/wowbranches");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithValidToken_Returns4SeededBranches()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/wowbranches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var branches = await response.Content.ReadFromJsonAsync<List<BranchDto>>();
        branches.Should().HaveCount(4)
            .And.Contain(b => b.Name == "Retail")
            .And.Contain(b => b.Name == "Classic Era")
            .And.Contain(b => b.Name == "Classic")
            .And.Contain(b => b.Name == "Classic Anniversary");
    }
}
