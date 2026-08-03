using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NetCord.Gateway;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Specs.Queries;
using RaidOps.Application.Contracts.Specs.Responses;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.ExternalApplication.Implementations.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

public class ReadyHandlerTests
{
    private readonly Mock<IEmojiService> _emojiService = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private readonly Mock<IServiceScope> _scope = new();
    private readonly Mock<IServiceProvider> _services = new();
    private readonly Mock<IQueryDispatcher> _dispatcher = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<ILogger<ReadyHandler>> _logger = new();
    private readonly ReadyHandler _sut;

    public ReadyHandlerTests()
    {
        // CreateAsyncScope() is an extension that wraps CreateScope() internally.
        _scopeFactory.Setup(f => f.CreateScope()).Returns(_scope.Object);
        _scope.Setup(s => s.ServiceProvider).Returns(_services.Object);
        _services.Setup(sp => sp.GetService(typeof(IQueryDispatcher))).Returns(_dispatcher.Object);
        _configuration.Setup(c => c["Discord:BlizzardClassIconBaseUrl"]).Returns("https://cdn.example.com/classes/");

        _sut = new ReadyHandler(_emojiService.Object, _scopeFactory.Object, _configuration.Object, _logger.Object);
    }

    private static ReadyEventArgs MakeEventArgs() =>
        // HandleAsync never reads anything off `arg` — a blank instance is enough, no need to
        // populate the JsonReadyEventArgs/RestClient the real (internal) constructor requires.
        (ReadyEventArgs)RuntimeHelpers.GetUninitializedObject(typeof(ReadyEventArgs));

    [Fact]
    public async Task HandleAsync_SpecsQuerySucceeds_SyncsClassAndSpecIcons()
    {
        var specs = new List<SpecDto>
        {
            new() { Id = 71, Name = "Arms", ClassId = 1, IconUrl = "https://cdn.example.com/specs/arms.jpg" },
            new() { Id = 72, Name = "Fury", ClassId = 1, IconUrl = null },
        };
        _dispatcher.Setup(d => d.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(It.IsAny<GetSpecsQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SpecDto>>.Ok(specs));
        List<(string Name, string SourceUrl)>? syncedEntries = null;
        _emojiService.Setup(e => e.SyncAsync(It.IsAny<IEnumerable<(string, string)>>(), default))
            .Callback<IEnumerable<(string, string)>, CancellationToken>((entries, _) => syncedEntries = entries.ToList())
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(MakeEventArgs());

        // 13 WoW class icons + the one spec that actually has a synced IconUrl (Fury has none).
        syncedEntries.Should().HaveCount(WowClassEmojiNames.ByClassId.Count + 1);
        syncedEntries.Should().Contain(e => e.Name == "class_warrior");
        syncedEntries.Should().Contain(e => e.Name == WowSpecEmojiNames.GetName(1, "Arms"));
        syncedEntries.Should().NotContain(e => e.Name == WowSpecEmojiNames.GetName(1, "Fury"));
    }

    [Fact]
    public async Task HandleAsync_SpecsQueryFails_SyncsOnlyClassIconsAndLogsWarning()
    {
        _dispatcher.Setup(d => d.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(It.IsAny<GetSpecsQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SpecDto>>.Fail("db unavailable"));
        List<(string Name, string SourceUrl)>? syncedEntries = null;
        _emojiService.Setup(e => e.SyncAsync(It.IsAny<IEnumerable<(string, string)>>(), default))
            .Callback<IEnumerable<(string, string)>, CancellationToken>((entries, _) => syncedEntries = entries.ToList())
            .Returns(Task.CompletedTask);

        await _sut.HandleAsync(MakeEventArgs());

        syncedEntries.Should().HaveCount(WowClassEmojiNames.ByClassId.Count);
    }

    [Fact]
    public async Task HandleAsync_EmojiServiceSyncThrows_DoesNotPropagateException()
    {
        _dispatcher.Setup(d => d.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(It.IsAny<GetSpecsQuery>(), default))
            .ReturnsAsync(Result<IEnumerable<SpecDto>>.Ok([]));
        _emojiService.Setup(e => e.SyncAsync(It.IsAny<IEnumerable<(string, string)>>(), default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.HandleAsync(MakeEventArgs()).AsTask();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_QueryDispatcherThrows_DoesNotPropagateException()
    {
        _dispatcher.Setup(d => d.DispatchAsync<GetSpecsQuery, IEnumerable<SpecDto>>(It.IsAny<GetSpecsQuery>(), default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = () => _sut.HandleAsync(MakeEventArgs()).AsTask();

        await act.Should().NotThrowAsync();
        _emojiService.Verify(e => e.SyncAsync(It.IsAny<IEnumerable<(string, string)>>(), default), Times.Never);
    }
}
