using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.ExternalApplication.Implementations.Services;
using RaidOps.UnitTests.Helpers;
using System.Net;

namespace RaidOps.UnitTests.ExternalApplication.Services;

public class DiscordDeployNotifierTests
{
    private readonly Mock<IConfiguration>                       _config      = new();
    private readonly Mock<IHostEnvironment>                     _environment = new();
    private readonly Mock<ILogger<DiscordDeployNotifier>>       _logger      = new();

    public DiscordDeployNotifierTests()
    {
        _environment.Setup(e => e.EnvironmentName).Returns("Acceptance");
    }

    [Fact]
    public async Task NotifyDeployedAsync_NoWebhookConfigured_DoesNotSendRequest()
    {
        _config.Setup(c => c["Discord:DeployWebhookUrl"]).Returns((string?)null);
        var (sut, handler) = MakeSut(HttpStatusCode.OK);

        await sut.NotifyDeployedAsync();

        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task NotifyDeployedAsync_WebhookConfigured_PostsEmbedWithEnvironmentAndVersion()
    {
        _config.Setup(c => c["Discord:DeployWebhookUrl"]).Returns("https://discord.com/api/webhooks/1/abc");
        _config.Setup(c => c["APP_VERSION"]).Returns("v1.2.3-acceptance.abc1234");
        var (sut, handler) = MakeSut(HttpStatusCode.OK);

        await sut.NotifyDeployedAsync();

        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Be("https://discord.com/api/webhooks/1/abc");
        handler.LastRequestBody.Should().Contain("Acceptance v1.2.3-acceptance.abc1234 is live");
    }

    [Fact]
    public async Task NotifyDeployedAsync_NoVersionConfigured_DefaultsToDev()
    {
        _config.Setup(c => c["Discord:DeployWebhookUrl"]).Returns("https://discord.com/api/webhooks/1/abc");
        var (sut, handler) = MakeSut(HttpStatusCode.OK);

        await sut.NotifyDeployedAsync();

        handler.LastRequestBody.Should().Contain("Acceptance dev is live");
    }

    [Fact]
    public async Task NotifyDeployedAsync_NonSuccessStatus_DoesNotThrow()
    {
        _config.Setup(c => c["Discord:DeployWebhookUrl"]).Returns("https://discord.com/api/webhooks/1/abc");
        var (sut, _) = MakeSut(HttpStatusCode.InternalServerError);

        var act = () => sut.NotifyDeployedAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyDeployedAsync_HttpClientThrows_DoesNotThrow()
    {
        _config.Setup(c => c["Discord:DeployWebhookUrl"]).Returns("https://discord.com/api/webhooks/1/abc");
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, exceptionToThrow: new HttpRequestException("network down"));
        var sut = new DiscordDeployNotifier(new HttpClient(handler), _config.Object, _environment.Object, _logger.Object);

        var act = () => sut.NotifyDeployedAsync();

        await act.Should().NotThrowAsync();
    }

    private (DiscordDeployNotifier Sut, FakeHttpMessageHandler Handler) MakeSut(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler(status);
        var sut = new DiscordDeployNotifier(new HttpClient(handler), _config.Object, _environment.Object, _logger.Object);
        return (sut, handler);
    }
}
