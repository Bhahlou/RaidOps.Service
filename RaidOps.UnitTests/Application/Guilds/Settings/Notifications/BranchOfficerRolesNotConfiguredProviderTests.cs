using FluentAssertions;
using RaidOps.Application.Implementations.Guilds.Settings.Notifications;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.UnitTests.Application.Guilds.Settings.Notifications;

public class BranchOfficerRolesNotConfiguredProviderTests
{
    private readonly BranchOfficerRolesNotConfiguredProvider _sut = new();

    private const string DiscordId = "user-1";

    private static UserGuild MakeUserGuild(string guildId, bool isAdmin, bool isRegistered, string name = "Guild Name", List<GuildBranch>? branches = null) => new()
    {
        UserDiscordId = DiscordId,
        GuildId = guildId,
        IsAdmin = isAdmin,
        Guild = new Guild { Id = guildId, Name = name, IsRegistered = isRegistered, Branches = branches ?? [] },
    };

    private static GuildBranch MakeBranch(bool isActive = true, List<string>? officerRoleIds = null) => new()
    {
        Id = 1, GuildId = "g1", BranchId = 1, IsActive = isActive, OfficerRoleIds = officerRoleIds ?? [],
    };

    [Fact]
    public async Task GetActiveAsync_AdminWithActiveBranchMissingOfficerRoles_ReturnsNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, name: "RaidOps", branches: [MakeBranch(officerRoleIds: [])]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().ContainSingle(n =>
            n.Type == NotificationType.BranchOfficerRolesNotConfigured && n.GuildId == "g1" && n.GuildName == "RaidOps");
    }

    [Fact]
    public async Task GetActiveAsync_AllActiveBranchesConfigured_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, branches: [MakeBranch(officerRoleIds: ["role-1"])]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_MixOfConfiguredAndUnconfiguredBranches_ReturnsNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, branches:
        [
            MakeBranch(officerRoleIds: ["role-1"]),
            MakeBranch(officerRoleIds: []),
        ]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task GetActiveAsync_NoActiveBranches_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, branches: []);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_OnlyDeactivatedBranchMissingRoles_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: true, branches: [MakeBranch(isActive: false, officerRoleIds: [])]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_NotAdmin_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: false, isRegistered: true, branches: [MakeBranch(officerRoleIds: [])]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActiveAsync_GuildNotRegistered_NoNotification()
    {
        var guild = MakeUserGuild("g1", isAdmin: true, isRegistered: false, branches: [MakeBranch(officerRoleIds: [])]);

        var result = await _sut.GetActiveAsync(DiscordId, [guild], default);

        result.Should().BeEmpty();
    }
}
