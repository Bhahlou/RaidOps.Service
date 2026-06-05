using FluentAssertions;
using RaidOps.ExternalApplication.Contracts.Services.Discord.Responses;

namespace RaidOps.UnitTests.ExternalApplication.Contracts;

public class GetDiscordUserGuildResponseTests
{
    [Fact]
    public void IsAdmin_Owner_ReturnsTrue()
    {
        var guild = Make(owner: true, permissions: "0");
        guild.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_NotOwnerButHasAdminPermission_ReturnsTrue()
    {
        // 0x8 = Administrator bit
        var guild = Make(owner: false, permissions: "8");
        guild.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_NotOwnerWithAdminBitInLargerBitfield_ReturnsTrue()
    {
        // Administrator bit set alongside other permissions
        var guild = Make(owner: false, permissions: "2147483647");
        guild.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public void IsAdmin_NotOwnerWithoutAdminPermission_ReturnsFalse()
    {
        // 0x4 = some non-admin permission, no admin bit
        var guild = Make(owner: false, permissions: "4");
        guild.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_NotOwnerWithUnparseablePermissions_ReturnsFalse()
    {
        var guild = Make(owner: false, permissions: "not-a-number");
        guild.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public void IsAdmin_NotOwnerWithNullPermissions_ReturnsFalse()
    {
        var guild = Make(owner: false, permissions: null);
        guild.IsAdmin.Should().BeFalse();
    }

    private static GetDiscordUserGuildResponse Make(bool owner, string? permissions) => new()
    {
        Id          = "g1",
        Name        = "Guild",
        Owner       = owner,
        Permissions = permissions,
    };
}
