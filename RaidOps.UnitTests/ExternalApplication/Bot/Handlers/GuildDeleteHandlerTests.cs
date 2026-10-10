using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetCord.Gateway;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Registration.Commands;
using RaidOps.ExternalApplication.Implementations.Bot.Handlers;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Handlers;

public class GuildDeleteHandlerTests
{
    private readonly Mock<IServiceScopeFactory>        _scopeFactory = new();
    private readonly Mock<IServiceScope>               _scope        = new();
    private readonly Mock<IServiceProvider>            _services     = new();
    private readonly Mock<ICommandDispatcher>          _dispatcher   = new();
    private readonly Mock<ILogger<GuildDeleteHandler>> _logger       = new();
    private readonly GuildDeleteHandler                _sut;

    private const ulong GuildId = 123456789UL;

    public GuildDeleteHandlerTests()
    {
        // CreateAsyncScope() is an extension that wraps CreateScope() internally.
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_services.Object);
        _services.Setup(sp => sp.GetService(typeof(ICommandDispatcher))).Returns(_dispatcher.Object);

        _sut = new GuildDeleteHandler(_scopeFactory.Object, _logger.Object);
    }

    // ── Unavailable (outage) ──────────────────────────────────────────────────

    // loggerEnabled paramètre les deux branches des guards IsEnabled (CA1873).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_IsUnavailable_SkipsDispatch(bool loggerEnabled)
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(loggerEnabled);
        var args = MakeEventArgs(GuildId, isUnavailable: true);

        await _sut.HandleAsync(args);

        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<UnregisterGuildCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Bot removed ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_BotRemoved_DispatchesUnregisterCommand(bool loggerEnabled)
    {
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(loggerEnabled);
        var args = MakeEventArgs(GuildId, isUnavailable: false);
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<UnregisterGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await _sut.HandleAsync(args);

        _dispatcher.Verify(d => d.DispatchAsync(
            It.Is<UnregisterGuildCommand>(c => c.GuildId == GuildId.ToString()),
            default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_DispatchFails_DoesNotThrow()
    {
        var args = MakeEventArgs(GuildId, isUnavailable: false);
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<UnregisterGuildCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail("some-error"));

        var act = () => _sut.HandleAsync(args).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_DispatchThrows_DoesNotPropagateException()
    {
        var args = MakeEventArgs(GuildId, isUnavailable: false);
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<UnregisterGuildCommand>(), default))
            .ThrowsAsync(new Exception("unexpected"));

        var act = () => _sut.HandleAsync(args).AsTask();

        await act.Should().NotThrowAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GuildDeleteEventArgs MakeEventArgs(ulong guildId, bool isUnavailable)
    {
        // GuildDeleteEventArgs has a single field <jsonModel>P of type JsonGuild.
        // JsonGuild inherits from JsonEntity which holds <Id>k__BackingField.
        // Both types use internal constructors — we bypass them via GetUninitializedObject.
        var jsonGuild = RuntimeHelpers.GetUninitializedObject(
            Type.GetType("NetCord.JsonModels.JsonGuild, NetCord")!);

        // Set Id on JsonEntity base class
        var idField = jsonGuild.GetType().BaseType!
            .GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
        idField.SetValue(jsonGuild, guildId);

        // Set IsUnavailable on JsonGuild
        var unavailableField = jsonGuild.GetType()
            .GetField("<IsUnavailable>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
        unavailableField.SetValue(jsonGuild, isUnavailable);

        var args = (GuildDeleteEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(GuildDeleteEventArgs));
        var jsonModelField = typeof(GuildDeleteEventArgs)
            .GetField("<jsonModel>P", BindingFlags.NonPublic | BindingFlags.Instance)!;
        jsonModelField.SetValue(args, jsonGuild);

        return args;
    }
}
