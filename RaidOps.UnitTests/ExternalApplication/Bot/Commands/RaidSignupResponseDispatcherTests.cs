using FluentAssertions;
using Moq;
using NetCord.Rest;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Signups.Commands;
using RaidOps.Application.Contracts.Raids.Signups.Responses;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;
using RaidOps.UnitTests.ExternalApplication.Bot;

namespace RaidOps.UnitTests.ExternalApplication.Bot.Commands;

public class RaidSignupResponseDispatcherTests
{
    private readonly Mock<ICommandDispatcher> _commandDispatcher = new();

    private const string GuildId = "guild-1";
    private const string RequesterId = "7";
    private const int GuildBranchId = 10;
    private const int EventId = 5;

    private static SignupReplyContext MakeContext(SignupStatus status = SignupStatus.Accepted, int? characterId = 42, int? specId = 71, string language = "en", RaidSignupCharacterResponse? character = null) =>
        new(GuildId, RequesterId, GuildBranchId, EventId, status, characterId, specId, language, character);

    private (MessageOptions Options, Func<Action<MessageOptions>, Task> ModifyResponseAsync) MakeModifyResponseAsync()
    {
        var options = NetCordTestHelpers.MakeMessageOptions();
        Task modify(Action<MessageOptions> action)
        {
            action(options);
            return Task.CompletedTask;
        }
        return (options, modify);
    }

    [Fact]
    public async Task DispatchAndReplyAsync_DispatchesCommandWithContextFields()
    {
        var (_, modifyResponseAsync) = MakeModifyResponseAsync();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(_commandDispatcher.Object, modifyResponseAsync, "https://app", MakeContext());

        _commandDispatcher.Verify(d => d.DispatchAsync(
            It.Is<SetMyRaidSignupCommand>(c =>
                c.GuildId == GuildId &&
                c.GuildBranchId == GuildBranchId &&
                c.EventId == EventId &&
                c.RequesterDiscordId == RequesterId &&
                c.Status == SignupStatus.Accepted &&
                c.CharacterId == 42 &&
                c.SpecId == 71),
            default), Times.Once);
    }

    [Fact]
    public async Task DispatchAndReplyAsync_Success_SetsSuccessContentAndClearsComponents()
    {
        var (options, modifyResponseAsync) = MakeModifyResponseAsync();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(_commandDispatcher.Object, modifyResponseAsync, "https://app", MakeContext(language: "en"));

        options.Content.Should().Be("✅ Response saved!");
        options.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchAndReplyAsync_Failure_SetsLocalizedFailureContent()
    {
        var (options, modifyResponseAsync) = MakeModifyResponseAsync();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.CharacterNotOnRoster));

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(_commandDispatcher.Object, modifyResponseAsync, "https://app", MakeContext());

        options.Content.Should().Contain("no longer on this branch's roster");
    }

    [Fact]
    public async Task DispatchAndReplyAsync_NoCharacterInContext_NoProfileLinkAppended()
    {
        var (options, modifyResponseAsync) = MakeModifyResponseAsync();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.SpecRequiredForSignup));

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(_commandDispatcher.Object, modifyResponseAsync, "https://app", MakeContext(character: null));

        options.Content.Should().Contain("https://app/characters");
        options.Content.Should().NotContain("/characters/");
    }

    [Fact]
    public async Task DispatchAndReplyAsync_CharacterInContext_UsesCharacterProfileLink()
    {
        var (options, modifyResponseAsync) = MakeModifyResponseAsync();
        _commandDispatcher.Setup(d => d.DispatchAsync(It.IsAny<SetMyRaidSignupCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.SpecRequiredForSignup));
        var character = new RaidSignupCharacterResponse { CharacterId = 42, CharacterName = "Arthas", ClassId = 6, BranchName = "Classic Era", RealmSlug = "silvermoon", RaidSpecs = [] };

        await RaidSignupResponseDispatcher.DispatchAndReplyAsync(_commandDispatcher.Object, modifyResponseAsync, "https://app", MakeContext(character: character));

        options.Content.Should().Contain("https://app/characters/classic-era/silvermoon/arthas");
    }
}
