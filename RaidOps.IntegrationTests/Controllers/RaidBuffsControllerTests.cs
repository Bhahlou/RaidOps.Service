using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Buffs.Responses;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Reference;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.RaidBuffsController"/> — the public read
/// side of the curated raid buff/debuff list (writing is owner-only, see the <c>AdminController</c> raid-buffs
/// tests). Spell IDs are 9920001+ to avoid collisions with other test classes. Assertions filter down to this
/// class's own spell IDs rather than asserting exact counts, since the expansion's list is shared reference
/// data other test classes may also write to.
/// </summary>
[Collection("Integration")]
public class RaidBuffsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int ExpansionId = 12;
    private const int SpellId = 9920001;

    private async Task SeedDefinitionAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            if (await db.RaidBuffDefinitions.AnyAsync(d => d.SpellId == SpellId))
                return;

            db.Spells.Add(new Spell { Id = SpellId });
            db.SpellAvailabilities.Add(new SpellAvailability { SpellId = SpellId, ExpansionId = ExpansionId, NameEn = "Zqxbuff Name", NameFr = "Nom Zqxbuff", NameDe = "Zqxbuff Name De", IconUrl = "https://cdn/zqxbuff.jpg" });
            db.RaidBuffDefinitions.Add(new RaidBuffDefinition
            {
                ExpansionId = ExpansionId,
                SpellId = SpellId,
                Scope = RaidBuffScope.Raid,
                Kind = RaidBuffKind.Buff,
                LabelEn = "+1 zqxbuff",
                LabelFr = "+1 zqxbuff fr",
                LabelDe = "+1 zqxbuff de",
                SortOrder = 0,
                Sources = [new RaidBuffSource { ClassId = 7, SpecId = 264 }],
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task GetAll_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync($"/api/v1/raidbuffs?expansionId={ExpansionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_TokenWithoutSubClaim_StillSucceeds()
    {
        // Unlike guild-scoped endpoints, this handler never reads the requester's Discord ID — reading
        // the curated list doesn't depend on who's asking, only on being authenticated at all.
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync($"/api/v1/raidbuffs?expansionId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_AnyAuthenticatedUser_ReturnsTheDefinitionWithItsSpellNameAndSources()
    {
        await SeedDefinitionAsync();
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync($"/api/v1/raidbuffs?expansionId={ExpansionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await response.Content.ReadFromJsonAsync<List<RaidBuffDefinitionResponse>>(ApiJsonOptions);
        var definition = definitions.Should().ContainSingle(d => d.SpellId == SpellId).Subject;
        definition.ExpansionId.Should().Be(ExpansionId);
        definition.LabelEn.Should().Be("+1 zqxbuff");
        definition.Spell.Should().NotBeNull();
        definition.Spell!.NameEn.Should().Be("Zqxbuff Name");
        definition.Spell.IconUrl.Should().Be("https://cdn/zqxbuff.jpg");
        definition.Sources.Should().ContainSingle(s => s.ClassId == 7 && s.SpecId == 264);
    }

    [Fact]
    public async Task GetAll_AnotherExpansion_DoesNotIncludeThisClasssDefinition()
    {
        await SeedDefinitionAsync();
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/v1/raidbuffs?expansionId=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var definitions = await response.Content.ReadFromJsonAsync<List<RaidBuffDefinitionResponse>>(ApiJsonOptions);
        definitions.Should().NotContain(d => d.SpellId == SpellId);
    }
}
