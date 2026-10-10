using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;

namespace RaidOps.UnitTests.Controllers;

public class RaidCompositionPreviewsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();

    private RaidCompositionPreviewsController MakeSut(string? discordId = "user-1") => new(_commands.Object, _queries.Object)
    {
        ControllerContext = discordId is null ? ControllerTestHelpers.MakeAnonymousContext() : ControllerTestHelpers.MakeContext(discordId),
    };

    // ── GetPreviews ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreviews_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetPreviews("guild-1", 10, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPreviews_QuerySucceeds_ReturnsOk()
    {
        var response = new List<RaidCompositionPreviewSummaryResponse>();
        _queries.Setup(q => q.DispatchAsync<GetRaidCompositionPreviewsQuery, List<RaidCompositionPreviewSummaryResponse>>(
                It.Is<GetRaidCompositionPreviewsQuery>(qr => qr.GuildId == "guild-1" && qr.GuildBranchId == 10 && qr.RequesterDiscordId == "user-1"), default))
            .ReturnsAsync(Result<List<RaidCompositionPreviewSummaryResponse>>.Ok(response));

        var result = await MakeSut().GetPreviews("guild-1", 10, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetPreviews_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidCompositionPreviewsQuery, List<RaidCompositionPreviewSummaryResponse>>(It.IsAny<GetRaidCompositionPreviewsQuery>(), default))
            .ReturnsAsync(Result<List<RaidCompositionPreviewSummaryResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().GetPreviews("guild-1", 10, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetPreview ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPreview_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetPreview("guild-1", 10, 5, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPreview_QuerySucceeds_ReturnsOk()
    {
        var response = new RaidCompositionPreviewResponse { Id = 5, Name = "40-man", GroupCount = 8, SlotsPerGroup = 5, Slots = [] };
        _queries.Setup(q => q.DispatchAsync<GetRaidCompositionPreviewQuery, RaidCompositionPreviewResponse>(
                It.Is<GetRaidCompositionPreviewQuery>(qr => qr.GuildId == "guild-1" && qr.GuildBranchId == 10 && qr.PreviewId == 5 && qr.RequesterDiscordId == "user-1"), default))
            .ReturnsAsync(Result<RaidCompositionPreviewResponse>.Ok(response));

        var result = await MakeSut().GetPreview("guild-1", 10, 5, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetPreview_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidCompositionPreviewQuery, RaidCompositionPreviewResponse>(It.IsAny<GetRaidCompositionPreviewQuery>(), default))
            .ReturnsAsync(Result<RaidCompositionPreviewResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound));

        var result = await MakeSut().GetPreview("guild-1", 10, 5, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── CreatePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePreview_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new CreateRaidCompositionPreviewCommand { Name = "40-man", GroupCount = 8 };

        var result = await MakeSut(null).CreatePreview("guild-1", 10, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreatePreview_SetsGuildIdBranchIdAndRequesterFromRouteAndClaim()
    {
        CreateRaidCompositionPreviewCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateRaidCompositionPreviewCommand>(), default))
            .Callback<CreateRaidCompositionPreviewCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new CreateRaidCompositionPreviewCommand { Name = "40-man", GroupCount = 8 };

        var result = await MakeSut("user-1").CreatePreview("guild-1", 10, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(10);
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task CreatePreview_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateRaidCompositionPreviewCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupCount));
        var command = new CreateRaidCompositionPreviewCommand { Name = "40-man", GroupCount = 9 };

        var result = await MakeSut().CreatePreview("guild-1", 10, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── RenamePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task RenamePreview_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new RenameRaidCompositionPreviewCommand { Name = "New name" };

        var result = await MakeSut(null).RenamePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RenamePreview_SetsIdsFromRouteAndClaim()
    {
        RenameRaidCompositionPreviewCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RenameRaidCompositionPreviewCommand>(), default))
            .Callback<RenameRaidCompositionPreviewCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new RenameRaidCompositionPreviewCommand { Name = "New name" };

        var result = await MakeSut("user-1").RenamePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(10);
        dispatched.PreviewId.Should().Be(5);
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task RenamePreview_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<RenameRaidCompositionPreviewCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound));
        var command = new RenameRaidCompositionPreviewCommand { Name = "New name" };

        var result = await MakeSut().RenamePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DuplicatePreview ─────────────────────────────────────────────────────

    [Fact]
    public async Task DuplicatePreview_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new DuplicateRaidCompositionPreviewCommand { NewName = "Copy" };

        var result = await MakeSut(null).DuplicatePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DuplicatePreview_SetsIdsFromRouteAndClaim()
    {
        DuplicateRaidCompositionPreviewCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DuplicateRaidCompositionPreviewCommand>(), default))
            .Callback<DuplicateRaidCompositionPreviewCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new DuplicateRaidCompositionPreviewCommand { NewName = "Copy" };

        var result = await MakeSut("user-1").DuplicatePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(10);
        dispatched.PreviewId.Should().Be(5);
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task DuplicatePreview_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DuplicateRaidCompositionPreviewCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound));
        var command = new DuplicateRaidCompositionPreviewCommand { NewName = "Copy" };

        var result = await MakeSut().DuplicatePreview("guild-1", 10, 5, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DeletePreview ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeletePreview_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).DeletePreview("guild-1", 10, 5, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeletePreview_SetsIdsFromRouteAndClaim()
    {
        DeleteRaidCompositionPreviewCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRaidCompositionPreviewCommand>(), default))
            .Callback<DeleteRaidCompositionPreviewCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut("user-1").DeletePreview("guild-1", 10, 5, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(10);
        dispatched.PreviewId.Should().Be(5);
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task DeletePreview_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteRaidCompositionPreviewCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound));

        var result = await MakeSut().DeletePreview("guild-1", 10, 5, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateSlot ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateSlot_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new UpdateRaidCompositionPreviewSlotCommand { GroupNumber = 1, SlotNumber = 1 };

        var result = await MakeSut(null).UpdateSlot("guild-1", 10, 5, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateSlot_SetsIdsFromRouteAndClaim()
    {
        UpdateRaidCompositionPreviewSlotCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRaidCompositionPreviewSlotCommand>(), default))
            .Callback<UpdateRaidCompositionPreviewSlotCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new UpdateRaidCompositionPreviewSlotCommand { GroupNumber = 2, SlotNumber = 3, WowClassId = 1, SpecId = 71, Note = "Bob" };

        var result = await MakeSut("user-1").UpdateSlot("guild-1", 10, 5, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.GuildBranchId.Should().Be(10);
        dispatched.PreviewId.Should().Be(5);
        dispatched.RequesterDiscordId.Should().Be("user-1");
        dispatched.GroupNumber.Should().Be(2);
        dispatched.SlotNumber.Should().Be(3);
        dispatched.WowClassId.Should().Be(1);
        dispatched.SpecId.Should().Be(71);
        dispatched.Note.Should().Be("Bob");
    }

    [Fact]
    public async Task UpdateSlot_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateRaidCompositionPreviewSlotCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupOrSlotNumber));
        var command = new UpdateRaidCompositionPreviewSlotCommand { GroupNumber = 1, SlotNumber = 1 };

        var result = await MakeSut().UpdateSlot("guild-1", 10, 5, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
