using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Domain.Models.Reference;
using RaidOps.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace RaidOps.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <see cref="RaidOps.API.Controllers.v1.SpellsController"/>. Spells are public
/// reference data — any authenticated user may search them, no guild involved — so these tests need no
/// seeded guild or user row, only spells. Spell IDs are 9910001+ to avoid collisions with other test classes.
/// </summary>
[Collection("Integration")]
public class SpellsControllerTests(RaidOpsWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int TbcExpansionId = 2;
    private const int ForeverExpansionId = 12;

    /// <summary>A spell that exists on both expansions under a different name/icon on each.</summary>
    private const int SharedSpellId = 9910001;

    /// <summary>A spell that only exists on Forever's expansion.</summary>
    private const int ForeverOnlySpellId = 9910002;

    /// <summary>Idempotently seeds the two shared test spells (only one test needs them today, but this matches the seeding pattern used elsewhere).</summary>
    private async Task EnsureSpellsAsync()
    {
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            if (await db.Spells.AnyAsync(s => s.Id == SharedSpellId))
                return;
        }

        await SeedAsync(db =>
        {
            db.Spells.AddRange(new Spell { Id = SharedSpellId }, new Spell { Id = ForeverOnlySpellId });
            db.SpellAvailabilities.AddRange(
                new SpellAvailability { SpellId = SharedSpellId, ExpansionId = TbcExpansionId, NameEn = "Zqxspell Tbc Name", NameFr = "Zqxspell Nom Tbc", NameDe = "Zqxspell Tbc Name De", IconUrl = "https://cdn/shared-tbc.jpg" },
                new SpellAvailability { SpellId = SharedSpellId, ExpansionId = ForeverExpansionId, NameEn = "Zqxspell Forever Name", NameFr = "Zqxspell Nom Forever", NameDe = "Zqxspell Forever Name De", IconUrl = "https://cdn/shared-forever.jpg" },
                new SpellAvailability { SpellId = ForeverOnlySpellId, ExpansionId = ForeverExpansionId, NameEn = "Zqxspell Forever Only", NameFr = "Zqxspell Forever Seul", NameDe = "Zqxspell Nur Forever", IconUrl = "https://cdn/forever-only.jpg" });
            return Task.CompletedTask;
        });
    }

    private static string SearchUrl(int expansionId, string searchTerm, string locale) => $"/api/v1/spells/search?expansionId={expansionId}&searchTerm={searchTerm}&locale={locale}";

    [Fact]
    public async Task Search_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync(SearchUrl(TbcExpansionId, "zqxspell", "en"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_TokenWithoutSubClaim_StillSucceeds()
    {
        // Unlike guild-scoped endpoints, this handler never reads the requester's Discord ID — the
        // search doesn't depend on who's asking, only on being authenticated at all.
        var client = CreateClientWithoutSubClaim();

        var response = await client.GetAsync(SearchUrl(TbcExpansionId, "no-such-spell-zqx", "en"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_AnyAuthenticatedUser_ReturnsTheNameAndIconOfTheGivenExpansion()
    {
        await EnsureSpellsAsync();
        var client = CreateAuthenticatedClient();

        var onTbc = await client.GetFromJsonAsync<List<SpellResponse>>(SearchUrl(TbcExpansionId, "zqxspell", "en"));
        var onForever = await client.GetFromJsonAsync<List<SpellResponse>>(SearchUrl(ForeverExpansionId, "zqxspell", "en"));
        var onForeverFr = await client.GetFromJsonAsync<List<SpellResponse>>(SearchUrl(ForeverExpansionId, "zqxspell", "fr"));

        onTbc.Should().ContainSingle().Which.Should().BeEquivalentTo(new SpellResponse { Id = SharedSpellId, Name = "Zqxspell Tbc Name", IconUrl = "https://cdn/shared-tbc.jpg" });
        onForever!.Select(x => (x.Id, x.Name, x.IconUrl)).Should().Equal(
            (SharedSpellId, "Zqxspell Forever Name", "https://cdn/shared-forever.jpg"),
            (ForeverOnlySpellId, "Zqxspell Forever Only", "https://cdn/forever-only.jpg"));
        onForeverFr!.Select(x => x.Name).Should().Equal("Zqxspell Forever Seul", "Zqxspell Nom Forever");
    }

    [Fact]
    public async Task Search_NoMatch_ReturnsAnEmptyList()
    {
        var client = CreateAuthenticatedClient();

        var response = await client.GetAsync(SearchUrl(TbcExpansionId, "no-such-spell-zqx", "en"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<SpellResponse>>())!.Should().BeEmpty();
    }
}
