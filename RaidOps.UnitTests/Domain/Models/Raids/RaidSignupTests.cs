using FluentAssertions;
using RaidOps.Domain.Models.Discord;
using RaidOps.Domain.Models.Raids;

namespace RaidOps.UnitTests.Domain.Models.Raids;

public class RaidSignupTests
{
    [Fact]
    public void NavigationProperties_CanBeSetAndRead()
    {
        var raidEvent = new RaidEvent();
        var user = new User();

        var signup = new RaidSignup { RaidEvent = raidEvent, User = user };

        signup.RaidEvent.Should().BeSameAs(raidEvent);
        signup.User.Should().BeSameAs(user);
    }
}
