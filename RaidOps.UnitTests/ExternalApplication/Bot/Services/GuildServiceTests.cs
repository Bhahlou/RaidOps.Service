using FluentAssertions;
using NetCord;
using NetCord.Gateway;
using NetCord.JsonModels;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot.Services;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Services;

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

    // ── GetUser ───────────────────────────────────────────────────────────────

    [Fact]
    public void GetUser_UserInGuild_ReturnsUser()
    {
        var user = NetCordTestHelpers.MakeGuildUser(UserId1, GuildId, []);
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId, new Dictionary<ulong, GuildUser> { [UserId1] = user });
        var sut = MakeSut(guild);

        var result = sut.GetUser(GuildId.ToString(), UserId1.ToString());

        result.Should().BeSameAs(user);
    }

    [Fact]
    public void GetUser_UserNotInGuild_ReturnsNull()
    {
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId, new Dictionary<ulong, GuildUser>());
        var sut = MakeSut(guild);

        var result = sut.GetUser(GuildId.ToString(), UserId1.ToString());

        result.Should().BeNull();
    }

    [Fact]
    public void GetUser_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetUser(GuildId.ToString(), UserId1.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    // ── GetPreferredLocale ────────────────────────────────────────────────────

    [Fact]
    public void GetPreferredLocale_GuildHasLocale_ReturnsIt()
    {
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId, new Dictionary<ulong, GuildUser>(), preferredLocale: "fr");
        var sut = MakeSut(guild);

        var result = sut.GetPreferredLocale(GuildId.ToString());

        result.Should().Be("fr");
    }

    [Fact]
    public void GetPreferredLocale_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetPreferredLocale(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    // ── GetChannels ───────────────────────────────────────────────────────────

    private const ulong BotUserId = 777UL;
    private const ulong ChannelId1 = 300UL;
    private const ulong ChannelId2 = 301UL;
    private const ulong CategoryId = 400UL;

    [Fact]
    public void GetChannels_GuildNotInCache_ThrowsInvalidOperationException()
    {
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(NetCordTestHelpers.EmptyCache().Object));

        var act = () => sut.GetChannels(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{GuildId}*");
    }

    [Fact]
    public void GetChannels_BotUserNotYetCached_ThrowsInvalidOperationException()
    {
        var guild = NetCordTestHelpers.MakeGuild(GuildId, OwnerId, new Dictionary<ulong, GuildUser>());
        var cache = NetCordTestHelpers.CacheWith((GuildId, guild)); // no .User stubbed
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(cache.Object));

        var act = () => sut.GetChannels(GuildId.ToString());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Bot user*");
    }

    /// <summary>
    /// NetCord's permission calculation always starts from the @everyone role (same snowflake as
    /// the guild itself) — a guild snapshot missing that entry is treated as "partial" and throws,
    /// so every <c>GetChannels</c> fixture below must include it even when it grants nothing.
    /// </summary>
    private static JsonRole EveryoneRole => NetCordTestHelpers.MakeJsonRole(GuildId, (Permissions)0);

    [Fact]
    public void GetChannels_BotHasAdministratorRole_ReturnsChannelsWithNoMissingPermissionsAndCategoryResolved()
    {
        var adminRole = NetCordTestHelpers.MakeJsonRole(AdminRoleId, Permissions.Administrator, managed: false);
        var botMember = NetCordTestHelpers.MakeGuildUser(BotUserId, GuildId, [AdminRoleId]);
        var category = NetCordTestHelpers.MakeTextChannel(CategoryId, "Raid", GuildId);
        var channel = NetCordTestHelpers.MakeTextChannel(ChannelId1, "general", GuildId, parentId: CategoryId);

        var guild = NetCordTestHelpers.MakeGuild(
            GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [BotUserId] = botMember },
            roles: [EveryoneRole, adminRole],
            channels: new Dictionary<ulong, IGuildChannel> { [CategoryId] = category, [ChannelId1] = channel });

        var cache = NetCordTestHelpers.CacheWith(NetCordTestHelpers.MakeCurrentUser(BotUserId), (GuildId, guild));
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(cache.Object));

        var result = sut.GetChannels(GuildId.ToString()).ToList();

        result.Should().ContainSingle(c => c.ChannelId == ChannelId1 && c.Name == "general" && c.MissingPermissions.Count == 0 && c.CategoryName == "Raid");
    }

    [Fact]
    public void GetChannels_BotUserNotInGuildMemberCache_ReturnsChannelsWithAllThreeMissingPermissions()
    {
        // Bot user is cached at the Gateway level (no "Bot user not yet available" exception), but
        // its own GuildUser member entry hasn't synced into this guild's cache yet — distinct from
        // "has no roles", this exercises the `botMember is not null ? ... : default` fallback itself.
        var channel = NetCordTestHelpers.MakeTextChannel(ChannelId2, "mod-only", GuildId);

        var guild = NetCordTestHelpers.MakeGuild(
            GuildId, OwnerId,
            new Dictionary<ulong, GuildUser>(), // no entry for BotUserId
            roles: [EveryoneRole],
            channels: new Dictionary<ulong, IGuildChannel> { [ChannelId2] = channel });

        var cache = NetCordTestHelpers.CacheWith(NetCordTestHelpers.MakeCurrentUser(BotUserId), (GuildId, guild));
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(cache.Object));

        var result = sut.GetChannels(GuildId.ToString()).ToList();

        var found = result.Should().ContainSingle(c => c.ChannelId == ChannelId2 && c.CategoryName == null).Subject;
        found.MissingPermissions.Should().BeEquivalentTo(
        [
            DiscordChannelPermissionFlag.ViewChannel,
            DiscordChannelPermissionFlag.SendMessages,
            DiscordChannelPermissionFlag.EmbedLinks,
        ]);
    }

    [Fact]
    public void GetChannels_BotHasNoRoles_ReturnsChannelsWithAllThreeMissingPermissions()
    {
        var botMember = NetCordTestHelpers.MakeGuildUser(BotUserId, GuildId, []);
        var channel = NetCordTestHelpers.MakeTextChannel(ChannelId2, "mod-only", GuildId);

        var guild = NetCordTestHelpers.MakeGuild(
            GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [BotUserId] = botMember },
            roles: [EveryoneRole],
            channels: new Dictionary<ulong, IGuildChannel> { [ChannelId2] = channel });

        var cache = NetCordTestHelpers.CacheWith(NetCordTestHelpers.MakeCurrentUser(BotUserId), (GuildId, guild));
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(cache.Object));

        var result = sut.GetChannels(GuildId.ToString()).ToList();

        var found = result.Should().ContainSingle(c => c.ChannelId == ChannelId2 && c.CategoryName == null).Subject;
        found.MissingPermissions.Should().BeEquivalentTo(
        [
            DiscordChannelPermissionFlag.ViewChannel,
            DiscordChannelPermissionFlag.SendMessages,
            DiscordChannelPermissionFlag.EmbedLinks,
        ]);
    }

    [Fact]
    public void GetChannels_BotCanViewAndSendButNotEmbedLinks_ReturnsChannelsWithOnlyEmbedLinksMissing()
    {
        var role = NetCordTestHelpers.MakeJsonRole(AdminRoleId, Permissions.ViewChannel | Permissions.SendMessages, managed: false);
        var botMember = NetCordTestHelpers.MakeGuildUser(BotUserId, GuildId, [AdminRoleId]);
        var channel = NetCordTestHelpers.MakeTextChannel(ChannelId1, "general", GuildId);

        var guild = NetCordTestHelpers.MakeGuild(
            GuildId, OwnerId,
            new Dictionary<ulong, GuildUser> { [BotUserId] = botMember },
            roles: [EveryoneRole, role],
            channels: new Dictionary<ulong, IGuildChannel> { [ChannelId1] = channel });

        var cache = NetCordTestHelpers.CacheWith(NetCordTestHelpers.MakeCurrentUser(BotUserId), (GuildId, guild));
        var sut = new GuildService(NetCordTestHelpers.MakeGatewayClient(cache.Object));

        var result = sut.GetChannels(GuildId.ToString()).ToList();

        var found = result.Should().ContainSingle(c => c.ChannelId == ChannelId1).Subject;
        found.MissingPermissions.Should().BeEquivalentTo([DiscordChannelPermissionFlag.EmbedLinks]);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static GuildService MakeSut(Guild guild)
    {
        var cache  = NetCordTestHelpers.CacheWith((GuildId, guild));
        var client = NetCordTestHelpers.MakeGatewayClient(cache.Object);
        return new GuildService(client);
    }
}
