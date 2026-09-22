using FluentAssertions;
using RaidOps.Application.Contracts.Reference.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GET /api/v1/wowclasses.
/// Validates auth enforcement and that seeded reference data is returned correctly.
/// </summary>
[Collection("Integration")]
public class WowClassesControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/wowclasses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithValidToken_Returns13SeededClasses()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/wowclasses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var classes = await response.Content.ReadFromJsonAsync<List<WowClassDto>>();
        classes.Should().HaveCount(13)
            .And.Contain(c => c.Id == 1 && c.Name == "Warrior" && c.Color == "C79C6E" && c.FirstExpansionId == 1)
            .And.Contain(c => c.Id == 10 && c.Name == "Monk" && c.FirstExpansionId == 5);
    }

    [Fact]
    public async Task GetAll_AvailableForExpansionIdForever_ExcludesMainlineClassesAddedAfterTheForkPoint()
    {
        var client = CreateAuthenticatedClient();

        // Expansion 12 = "Forever", forked from Classic (1) — see RaidOpsDbContext.SeedExpansions.
        var response = await client.GetAsync("/api/v1/wowclasses?availableForExpansionId=12");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var classes = await response.Content.ReadFromJsonAsync<List<WowClassDto>>();
        classes.Should().Contain(c => c.Id == 1) // Warrior — Classic, the fork point itself
            .And.NotContain(c => c.Id == 6) // Death Knight — WotLK, mainline-only
            .And.NotContain(c => c.Id == 10) // Monk — MoP, mainline-only
            .And.NotContain(c => c.Id == 12) // Demon Hunter — Legion, mainline-only
            .And.NotContain(c => c.Id == 13); // Evoker — Dragonflight, mainline-only
    }
}
