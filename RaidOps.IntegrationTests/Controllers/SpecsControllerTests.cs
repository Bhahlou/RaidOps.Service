using FluentAssertions;
using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for GET /api/v1/specs.
/// Validates auth enforcement and that seeded reference data is returned correctly.
/// </summary>
[Collection("Integration")]
public class SpecsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/specs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithValidToken_Returns39SeededSpecs()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/specs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var specs = await response.Content.ReadFromJsonAsync<List<SpecDto>>();
        specs.Should().HaveCount(39)
            .And.Contain(s => s.Id == 62 && s.Name == "Arcane" && s.ClassId == 8 && s.Role == "Dps");
    }
}
