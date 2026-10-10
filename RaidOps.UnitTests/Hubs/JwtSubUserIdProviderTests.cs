using System.Security.Claims;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using RaidOps.API.Hubs;
using Xunit;

namespace RaidOps.UnitTests.Hubs;

public class JwtSubUserIdProviderTests
{
    private readonly JwtSubUserIdProvider _sut = new();

    [Fact]
    public void GetUserId_ClaimPresent_ReturnsSubClaimValue()
    {
        var connection = MakeConnection(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "511624657162731533")])));

        var result = _sut.GetUserId(connection);

        result.Should().Be("511624657162731533");
    }

    [Fact]
    public void GetUserId_NoSubClaim_ReturnsNull()
    {
        var connection = MakeConnection(new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Bhahlou")])));

        var result = _sut.GetUserId(connection);

        result.Should().BeNull();
    }

    [Fact]
    public void GetUserId_NoUser_ReturnsNull()
    {
        var connection = MakeConnection(null);

        var result = _sut.GetUserId(connection);

        result.Should().BeNull();
    }

    private static HubConnectionContext MakeConnection(ClaimsPrincipal? user)
    {
        var connectionContext = new Mock<ConnectionContext>();
        connectionContext.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());
        connectionContext.Setup(c => c.Features).Returns(new FeatureCollection());
        connectionContext.Setup(c => c.Items).Returns(new Dictionary<object, object?>());

        var mock = new Mock<HubConnectionContext>(
            connectionContext.Object,
            new HubConnectionContextOptions(),
            NullLoggerFactory.Instance)
        {
            CallBase = true,
        };
        mock.Setup(c => c.User).Returns(user!);
        return mock.Object;
    }
}
