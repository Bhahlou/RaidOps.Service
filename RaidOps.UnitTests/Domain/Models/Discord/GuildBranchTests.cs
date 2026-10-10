using FluentAssertions;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.UnitTests.Domain.Models.Discord;

/// <summary>
/// Unit tests for <see cref="GuildBranch"/>.
/// </summary>
public class GuildBranchTests
{
    [Fact]
    public void Guild_SetThenGet_ReturnsTheAssignedGuild()
    {
        var guild = new Guild { Id = "guild-1", Name = "RaidOps" };
        var branch = new GuildBranch { Guild = guild };

        branch.Guild.Should().BeSameAs(guild);
    }
}
