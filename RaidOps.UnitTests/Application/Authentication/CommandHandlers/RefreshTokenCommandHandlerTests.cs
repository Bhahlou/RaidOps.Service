using FluentAssertions;
using Moq;
using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Authentication.Responses;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Authentication.CommandHandlers;

namespace RaidOps.UnitTests.Application.Authentication.CommandHandlers;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRaidOpsAuthService>   _authService = new();
    private readonly RefreshTokenCommandHandler  _sut;

    private static readonly RefreshTokenCommand Command = new()
    {
        RefreshToken = "old-refresh-jwt",
    };

    private static readonly AuthenticationResponse AuthResponse = new()
    {
        AccessToken  = "new-jwt-access",
        RefreshToken = "new-jwt-refresh",
        AccessTokenExpiration  = DateTime.UtcNow.AddMinutes(15),
        RefreshTokenExpiration = DateTime.UtcNow.AddDays(30),
    };

    public RefreshTokenCommandHandlerTests()
    {
        _sut = new RefreshTokenCommandHandler(_authService.Object);
    }

    [Fact]
    public async Task HandleAsync_ServiceSucceeds_ReturnsOkWithNewTokens()
    {
        _authService.Setup(s => s.HandleRefreshTokenAsync(Command, default))
            .ReturnsAsync(Result<AuthenticationResponse>.Ok(AuthResponse));

        var result = await _sut.HandleAsync(Command);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Body.Should().Be(AuthResponse);
    }

    [Fact]
    public async Task HandleAsync_ServiceFails_PropagatesError()
    {
        _authService.Setup(s => s.HandleRefreshTokenAsync(Command, default))
            .ReturnsAsync(Result<AuthenticationResponse>.Fail(ResponseDetail.Unauthorized));

        var result = await _sut.HandleAsync(Command);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().Be(ResponseDetail.Unauthorized);
    }
}
