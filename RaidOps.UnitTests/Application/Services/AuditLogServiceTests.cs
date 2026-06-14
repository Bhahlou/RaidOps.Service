using System.Text.Json;
using FluentAssertions;
using Moq;
using RaidOps.Application.Implementations.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.UnitTests.Application.Services;

/// <summary>
/// Unit tests for <see cref="AuditLogService"/>.
/// </summary>
public class AuditLogServiceTests
{
    private readonly Mock<IGuildAuditLogRepository> _repo = new();
    private readonly AuditLogService                _sut;

    private const string GuildId   = "guild-1";
    private const string ActorId   = "user-1";

    public AuditLogServiceTests()
    {
        _sut = new AuditLogService(_repo.Object);
    }

    // ── Entry fields ──────────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_MapsRequiredFields()
    {
        GuildAuditLog? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default))
            .Callback<GuildAuditLog, CancellationToken>((entry, _) => captured = entry);

        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.GuildRegistered);

        captured.Should().NotBeNull();
        captured!.GuildId.Should().Be(GuildId);
        captured.ActorDiscordId.Should().Be(ActorId);
        captured.ActionType.Should().Be(GuildAuditAction.GuildRegistered);
    }

    [Fact]
    public async Task LogAsync_OccurredAtIsApproximatelyNow()
    {
        var before = DateTime.UtcNow;
        GuildAuditLog? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default))
            .Callback<GuildAuditLog, CancellationToken>((entry, _) => captured = entry);

        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.MemberJoined);

        captured!.OccurredAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
    }

    // ── Variables / Details serialization ────────────────────────────────

    [Fact]
    public async Task LogAsync_NullVariables_DetailsIsNull()
    {
        GuildAuditLog? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default))
            .Callback<GuildAuditLog, CancellationToken>((entry, _) => captured = entry);

        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.GuildRegistered, variables: null);

        captured!.Details.Should().BeNull();
    }

    [Fact]
    public async Task LogAsync_WithVariables_SerializesAsJson()
    {
        GuildAuditLog? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default))
            .Callback<GuildAuditLog, CancellationToken>((entry, _) => captured = entry);

        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.MemberJoined,
            new Dictionary<string, string> { ["characterName"] = "Arthas" });

        captured!.Details.Should().NotBeNull();
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(captured.Details!);
        dict.Should().ContainKey("characterName").WhoseValue.Should().Be("Arthas");
    }

    [Fact]
    public async Task LogAsync_MultipleVariables_AllSerializedToJson()
    {
        GuildAuditLog? captured = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default))
            .Callback<GuildAuditLog, CancellationToken>((entry, _) => captured = entry);

        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.MemberRankUpdated,
            new Dictionary<string, string> { ["oldRank"] = "Main", ["newRank"] = "Alt" });

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(captured!.Details!);
        dict.Should().HaveCount(2)
            .And.ContainKey("oldRank").WhoseValue.Should().Be("Main");
        dict.Should().ContainKey("newRank").WhoseValue.Should().Be("Alt");
    }

    // ── Repository call ───────────────────────────────────────────────────

    [Fact]
    public async Task LogAsync_CallsAddAsyncExactlyOnce()
    {
        await _sut.LogAsync(GuildId, ActorId, GuildAuditAction.MemberLeft);

        _repo.Verify(r => r.AddAsync(It.IsAny<GuildAuditLog>(), default), Times.Once);
    }
}
