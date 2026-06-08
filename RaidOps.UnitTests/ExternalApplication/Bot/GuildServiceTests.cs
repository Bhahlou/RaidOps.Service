using FluentAssertions;
using NetCord;
using NetCord.Gateway;
using RaidOps.ExternalApplication.Implementations.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class GuildServiceTests
{
    private const ulong GuildId  = 111UL;
    private const ulong OwnerId  = 999UL;
    private const ulong UserId1  = 1UL;
    private const ulong UserId2  = 2UL;
    private const ulong AdminRoleId = 50UL;

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_GuildInCache_ReturnsGuild()
    {
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId, new Dictionary<ulong, GuildUser>());
        var sut   = MakeSut(guild);

        var result = sut.Get(GuildId.ToString());

        result.Should().BeSameAs(guild);
    }

    [Fact]
    public void Get_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(
            NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.Get(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    // ── GetUsers ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetUsers_GuildWithUsers_ReturnsUsers()
    {
        var user1 = NetCordTestHelpers.MakeGuildUser(UserId1, GuildId, []);
        var user2 = NetCordTestHelpers.MakeGuildUser(UserId2, GuildId, []);
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [UserId1] = user1, [UserId2] = user2 });
        var sut = MakeSut(guild);

        var result = sut.GetUsers(GuildId.ToString()).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetUsers_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(
            NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetUsers(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    // ── GetAdmins ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetAdmins_UserWithAdminRole_Included()
    {
        var adminRole = NetCordTestHelpers.MakeJsonRole(AdminRoleId, Permissions.Administrator, managed: false);
        var adminUser = NetCordTestHelpers.MakeGuildUser(UserId1, GuildId, [AdminRoleId]);

        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [UserId1] = adminUser },
            roles: [adminRole]);
        var sut = MakeSut(guild);

        var admins = sut.GetAdmins(GuildId.ToString()).ToList();

        admins.Should().ContainSingle(u => u.Id == UserId1);
    }

    [Fact]
    public void GetAdmins_UserWithManagedAdminRole_Excluded()
    {
        var managedRole = NetCordTestHelpers.MakeJsonRole(AdminRoleId, Permissions.Administrator, managed: true);
        var user = NetCordTestHelpers.MakeGuildUser(UserId1, GuildId, [AdminRoleId]);

        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [UserId1] = user },
            roles: [managedRole]);
        var sut = MakeSut(guild);

        var admins = sut.GetAdmins(GuildId.ToString()).ToList();

        admins.Should().BeEmpty();
    }

    [Fact]
    public void GetAdmins_GuildOwner_AlwaysIncluded()
    {
        // Owner has no admin role, but must always appear
        var owner = NetCordTestHelpers.MakeGuildUser(OwnerId, GuildId, []);

        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [OwnerId] = owner },
            roles: []);
        var sut = MakeSut(guild);

        var admins = sut.GetAdmins(GuildId.ToString()).ToList();

        admins.Should().ContainSingle(u => u.Id == OwnerId);
    }

    [Fact]
    public void GetAdmins_OwnerAlsoHasAdminRole_NotDuplicated()
    {
        var adminRole = NetCordTestHelpers.MakeJsonRole(AdminRoleId, Permissions.Administrator, managed: false);
        var owner = NetCordTestHelpers.MakeGuildUser(OwnerId, GuildId, [AdminRoleId]);

        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [OwnerId] = owner },
            roles: [adminRole]);
        var sut = MakeSut(guild);

        var admins = sut.GetAdmins(GuildId.ToString()).ToList();

        admins.Should().ContainSingle();
    }

    [Fact]
    public void GetAdmins_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(
            NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetAdmins(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    // ── GetRoles ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetRoles_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(
            NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetRoles(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    [Fact]
    public void GetRoles_EveryoneRole_Excluded()
    {
        // @everyone has the same snowflake as the guild itself
        var everyone = NetCordTestHelpers.MakeJsonRole(GuildId, (Permissions)0);
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser>(), roles: [everyone]);
        var sut = MakeSut(guild);

        var roles = sut.GetRoles(GuildId.ToString()).ToList();

        roles.Should().BeEmpty();
    }

    [Fact]
    public void GetRoles_ManagedRole_Excluded()
    {
        var managed = NetCordTestHelpers.MakeJsonRole(AdminRoleId, (Permissions)0, managed: true);
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser>(), roles: [managed]);
        var sut = MakeSut(guild);

        var roles = sut.GetRoles(GuildId.ToString()).ToList();

        roles.Should().BeEmpty();
    }

    [Fact]
    public void GetRoles_AssignableRole_Returned()
    {
        var assignable = NetCordTestHelpers.MakeJsonRole(AdminRoleId, (Permissions)0, managed: false);
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId,
            new Dictionary<ulong, GuildUser>(), roles: [assignable]);
        var sut = MakeSut(guild);

        var roles = sut.GetRoles(GuildId.ToString()).ToList();

        roles.Should().ContainSingle(r => r.Id == AdminRoleId);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static GuildService MakeSut(Guild guild)
    {
        var cache  = NetCordTestHelpers.CacheWith((GuildId, guild));
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object);
        return new GuildService(client);
    }
}
