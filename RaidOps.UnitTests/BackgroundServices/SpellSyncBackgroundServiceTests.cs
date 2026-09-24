using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RaidOps.API.BackgroundServices;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Spells.Commands;
using RaidOps.UnitTests.Helpers;
using System.Runtime.CompilerServices;

namespace RaidOps.UnitTests.BackgroundServices;

/// <summary>
/// Unit tests for <see cref="SpellSyncBackgroundService"/>. The service polls hourly, so every test
/// only observes the immediate first tick and then stops the service — nothing here waits on the timer.
/// </summary>
public class SpellSyncBackgroundServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Mock<ICommandDispatcher> _dispatcher = new();
    private readonly CapturingLogger<SpellSyncBackgroundService> _logger = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskCompletionSource<SyncSpellsCommand> _firstDispatch = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SpellSyncBackgroundServiceTests()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _dispatcher.Object);
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private void SetupDispatch(Func<SyncSpellsCommand, CancellationToken, Task<Result<CommandResponse>>> behavior)
        => _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<SyncSpellsCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncSpellsCommand command, CancellationToken ct) =>
            {
                _firstDispatch.TrySetResult(command);
                return behavior(command, ct);
            });

    private static Task<Result<CommandResponse>> Ok() => Task.FromResult(Result<CommandResponse>.Ok(new CommandResponse("done")));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task StartAsync_DispatchesAnUnforcedSyncImmediately()
    {
        SetupDispatch((_, _) => Ok());
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);

        await sut.StartAsync(CancellationToken.None);
        var command = await _firstDispatch.Task.WaitAsync(Timeout);
        await sut.StopAsync(CancellationToken.None);

        command.Force.Should().BeFalse();
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<SyncSpellsCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        _logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task StopAsync_AfterTheFirstTick_StopsTheLoopWithoutWaitingForTheNextTick()
    {
        SetupDispatch((_, _) => Ok());
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);

        await sut.StartAsync(CancellationToken.None);
        await _firstDispatch.Task.WaitAsync(Timeout);
        await sut.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        await WaitUntilAsync(() => sut.ExecuteTask!.IsCompleted);
        sut.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue("stopping while waiting for the next tick is a clean shutdown");
    }

    [Fact]
    public async Task StopAsync_WhileASyncIsInFlight_PropagatesCancellationToTheDispatcher()
    {
        SetupDispatch(async (_, ct) =>
        {
            await Task.Delay(Timeout, ct);
            return await Ok();
        });
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);

        await sut.StartAsync(CancellationToken.None);
        await _firstDispatch.Task.WaitAsync(Timeout);
        await sut.StopAsync(CancellationToken.None).WaitAsync(Timeout);

        await WaitUntilAsync(() => sut.ExecuteTask!.IsCompleted);
        sut.ExecuteTask!.IsCanceled.Should().BeTrue();
        _logger.Entries.Should().BeEmpty("a shutdown-driven cancellation is not a sync failure");
    }

    [Fact]
    public async Task ExecuteAsync_DispatcherThrows_LogsErrorAndKeepsRunning()
    {
        SetupDispatch((_, _) => throw new InvalidOperationException("wago is down"));
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);

        await sut.StartAsync(CancellationToken.None);
        await _firstDispatch.Task.WaitAsync(Timeout);
        await WaitUntilAsync(() => _logger.Entries.Count > 0);

        sut.ExecuteTask!.IsCompleted.Should().BeFalse("a failed poll must not end the hosted service");
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_FailedResult_LogsAWarningWithTheError()
    {
        SetupDispatch((_, _) => Task.FromResult(Result<CommandResponse>.Fail("boom")));
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);

        await sut.StartAsync(CancellationToken.None);
        await _firstDispatch.Task.WaitAsync(Timeout);
        await WaitUntilAsync(() => _logger.Entries.Count > 0);
        await sut.StopAsync(CancellationToken.None);

        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("boom"));
    }

    [Fact]
    public async Task ExecuteAsync_FailedResultWithWarningsDisabled_DoesNotLog()
    {
        var logger = new Mock<ILogger<SpellSyncBackgroundService>>();
        logger.Setup(l => l.IsEnabled(LogLevel.Warning)).Returns(false);
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<SyncSpellsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                dispatched.TrySetResult();
                return Task.FromResult(Result<CommandResponse>.Fail("boom"));
            });
        var sut = new SpellSyncBackgroundService(_scopeFactory, logger.Object);

        await sut.StartAsync(CancellationToken.None);
        await dispatched.Task.WaitAsync(Timeout);
        await WaitUntilAsync(() => logger.Invocations.Any(i => i.Method.Name == nameof(ILogger.IsEnabled)));
        await sut.StopAsync(CancellationToken.None);

        logger.Verify(l => l.IsEnabled(LogLevel.Warning), Times.Once);
        logger.Invocations.Should().NotContain(i => i.Method.Name == nameof(ILogger.Log));
    }

    // The poll interval is a private static readonly one hour; reach it with UnsafeAccessor so the
    // "next tick" path can be exercised in milliseconds instead of an hour. Only this class touches the
    // service, and the tests inside a class run sequentially, so swapping it is safe as long as it is restored.
    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "PollInterval")]
    private static extern ref TimeSpan PollIntervalField(SpellSyncBackgroundService? instance);

    private static TimeSpan SwapPollInterval(TimeSpan value)
    {
        ref var field = ref PollIntervalField(null);
        var previous = field;
        field = value;
        return previous;
    }

    [Fact]
    public async Task ExecuteAsync_KeepsDispatchingOnEveryTickUntilStopped()
    {
        var dispatches = 0;
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<SyncSpellsCommand>(), It.IsAny<CancellationToken>()))
            .Returns((SyncSpellsCommand command, CancellationToken _) =>
            {
                Interlocked.Increment(ref dispatches);
                command.Force.Should().BeFalse();
                return Ok();
            });
        var sut = new SpellSyncBackgroundService(_scopeFactory, _logger);
        var original = SwapPollInterval(TimeSpan.FromMilliseconds(20));
        try
        {
            await sut.StartAsync(CancellationToken.None);
            await WaitUntilAsync(() => Volatile.Read(ref dispatches) >= 3);
            await sut.StopAsync(CancellationToken.None);
        }
        finally
        {
            SwapPollInterval(original);
        }

        Volatile.Read(ref dispatches).Should().BeGreaterThanOrEqualTo(3);
        _logger.Entries.Should().BeEmpty();
    }
}
