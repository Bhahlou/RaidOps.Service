using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Reference;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetMyMembershipsInGuildQueryHandler"/>.
/// </summary>
public class GetMyMembershipsInGuildQueryHandlerTests
{
    private readonly Mock<IGuildMembershipRepository>    _memberships = new();
    private readonly GetMyMembershipsInGuildQueryHandler _sut;

    private const int    CharacterId = 1;
    private const string GuildId     = "guild-1";
    private const string DiscordId   = "user-1";

    private static readonly GetMyMembershipsInGuildQuery Query = new()
    {
        GuildId            = GuildId,
        RequesterDiscordId = DiscordId,
    };

    public GetMyMembershipsInGuildQueryHandlerTests()
    {
        _sut = new GetMyMembershipsInGuildQueryHandler(_memberships.Object);
    }

    // ── NoMemberships ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoMemberships_ReturnsEmptyList()
    {
        _memberships.Setup(r => r.GetByGuildIdAndUserAsync(GuildId, DiscordId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Active expansion state is used ────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WithActiveState_MapsGuildNameFromActiveState()
    {
        var joinedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        _memberships.Setup(r => r.GetByGuildIdAndUserAsync(GuildId, DiscordId, default))
            .ReturnsAsync([MakeMembership(joinedAt, activeGuildName: "ActiveGuild", withActive: true)]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var r = result.Value[0];
        r.CharacterId.Should().Be(CharacterId);
        r.Name.Should().Be("Arthas");
        r.RealmName.Should().Be("Kazzak");
        r.ClassName.Should().Be("Paladin");
        r.ClassColor.Should().Be("#F58CBA");
        r.AvatarUrl.Should().Be("https://cdn/avatar.jpg");
        r.GuildName.Should().Be("ActiveGuild");
        r.CharacterRank.Should().Be(CharacterRank.Main);
        r.JoinedAt.Should().Be(joinedAt);
    }

    // ── No active state falls back to highest level ───────────────────────

    [Fact]
    public async Task HandleAsync_NoActiveState_FallsBackToHighestLevel()
    {
        var joinedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var character = MakeCharacter();
        character.ExpansionStates =
        [
            new CharacterExpansionState { IsActive = false, Level = 60, GuildName = "OldGuild" },
            new CharacterExpansionState { IsActive = false, Level = 80, GuildName = "HighGuild" },
        ];
        _memberships.Setup(r => r.GetByGuildIdAndUserAsync(GuildId, DiscordId, default))
            .ReturnsAsync([new GuildMembership
            {
                CharacterId   = CharacterId,
                GuildId       = GuildId,
                CharacterRank = CharacterRank.Main,
                JoinedAt      = joinedAt,
                Character     = character,
            }]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].GuildName.Should().Be("HighGuild");
    }

    // ── No expansion states — GuildName is null ───────────────────────────

    [Fact]
    public async Task HandleAsync_NoExpansionStates_GuildNameIsNull()
    {
        var character = MakeCharacter();
        character.ExpansionStates = [];
        _memberships.Setup(r => r.GetByGuildIdAndUserAsync(GuildId, DiscordId, default))
            .ReturnsAsync([new GuildMembership
            {
                CharacterId   = CharacterId,
                GuildId       = GuildId,
                CharacterRank = CharacterRank.Alt,
                JoinedAt      = DateTime.UtcNow,
                Character     = character,
            }]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value[0].GuildName.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Character MakeCharacter() => new()
    {
        Id            = CharacterId,
        Name          = "Arthas",
        UserDiscordId = DiscordId,
        AvatarUrl     = "https://cdn/avatar.jpg",
        Realm         = new Realm { Id = 1, Name = "Kazzak", Slug = "kazzak", Region = "eu", BranchId = 1 },
        Class         = new WowClass { Id = 2, Name = "Paladin", Color = "F58CBA" },
    };

    private static GuildMembership MakeMembership(DateTime joinedAt, string? activeGuildName, bool withActive)
    {
        var character = MakeCharacter();
        character.ExpansionStates = withActive && activeGuildName is not null
            ? [new CharacterExpansionState { IsActive = true, Level = 80, GuildName = activeGuildName }]
            : [];

        return new GuildMembership
        {
            CharacterId   = CharacterId,
            GuildId       = GuildId,
            CharacterRank = CharacterRank.Main,
            JoinedAt      = joinedAt,
            Character     = character,
        };
    }
}
