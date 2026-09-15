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
}
