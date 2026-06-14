using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Guilds.Memberships.Queries;
using RaidOps.Application.Implementations.Guilds.Memberships.QueryHandlers;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Guilds.Memberships.QueryHandlers;

/// <summary>
/// Unit tests for <see cref="GetCharacterMembershipsQueryHandler"/>.
/// </summary>
public class GetCharacterMembershipsQueryHandlerTests
{
    private readonly Mock<ICharacterRepository>       _characters  = new();
    private readonly Mock<IGuildMembershipRepository> _memberships = new();
    private readonly GetCharacterMembershipsQueryHandler _sut;

    private const int    CharacterId = 1;
    private const string GuildId     = "guild-1";
    private const string DiscordId   = "user-1";

    private static readonly GetCharacterMembershipsQuery Query = new()
    {
        CharacterId        = CharacterId,
        RequesterDiscordId = DiscordId,
    };

    public GetCharacterMembershipsQueryHandlerTests()
    {
        _sut = new GetCharacterMembershipsQueryHandler(_characters.Object, _memberships.Object);
    }

    // ── CharacterNotFound ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotFound_ReturnsCharacterNotFound()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default)).ReturnsAsync((Character?)null);

        var result = await _sut.HandleAsync(Query);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotFound);
    }

    // ── CharacterNotOwned ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_CharacterNotOwned_ReturnsCharacterNotOwned()
    {
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = "other-user" });

        var result = await _sut.HandleAsync(Query);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.CharacterNotOwned);
    }

    // ── NoMemberships ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoMemberships_ReturnsEmptyList()
    {
        SetupCharacter();
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default)).ReturnsAsync([]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ── Success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_Success_ReturnsMappedMemberships()
    {
        var joinedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        SetupCharacter();
        _memberships.Setup(r => r.GetByCharacterIdAsync(CharacterId, default))
            .ReturnsAsync(
            [
                new GuildMembership
                {
                    CharacterId   = CharacterId,
                    GuildId       = GuildId,
                    CharacterRank = CharacterRank.Main,
                    JoinedAt      = joinedAt,
                    Guild = new Guild { Id = GuildId, Name = "RaidOps", IconHash = "abc123" },
                },
            ]);

        var result = await _sut.HandleAsync(Query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var m = result.Value[0];
        m.GuildId.Should().Be(GuildId);
        m.GuildName.Should().Be("RaidOps");
        m.GuildIconHash.Should().Be("abc123");
        m.CharacterRank.Should().Be(CharacterRank.Main);
        m.JoinedAt.Should().Be(joinedAt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetupCharacter() =>
        _characters.Setup(r => r.GetByIdAsync(CharacterId, default))
            .ReturnsAsync(new Character { Id = CharacterId, Name = "Arthas", UserDiscordId = DiscordId });
}
