using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RaidOps.API.Controllers.v1;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Raids.Attributions.Queries;
using RaidOps.Application.Contracts.Raids.Attributions.Responses;
using RaidOps.Application.Contracts.Raids.Bosses.Queries;
using RaidOps.Application.Contracts.Raids.Bosses.Responses;
using RaidOps.Application.Contracts.Raids.Spells.Queries;
using RaidOps.Application.Contracts.Raids.Spells.Responses;
using RaidOps.Application.Contracts.Raids.Zones.Queries;
using RaidOps.Application.Contracts.Raids.Zones.Responses;
using RaidOps.Domain.Enums;

namespace RaidOps.UnitTests.Controllers;

public class GuildAttributionDefinitionsControllerTests
{
    private readonly Mock<ICommandDispatcher> _commands = new();
    private readonly Mock<IQueryDispatcher> _queries = new();
    private GuildAttributionDefinitionsController MakeSut(string? discordId = "user-1") => new(_commands.Object, _queries.Object)
    {
        ControllerContext = discordId is null ? ControllerTestHelpers.MakeAnonymousContext() : ControllerTestHelpers.MakeContext(discordId),
    };

    private static readonly List<AttributionCellRequest> Cells = [new() { Kind = AttributionCellKind.NameSlot }];

    // ── GetDefinitions ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitions_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetDefinitions("guild-1", null, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetDefinitions_QuerySucceeds_ReturnsOk()
    {
        var response = new List<GuildAttributionDefinitionResponse>();
        _queries.Setup(q => q.DispatchAsync<GetGuildAttributionDefinitionsQuery, List<GuildAttributionDefinitionResponse>>(
                It.Is<GetGuildAttributionDefinitionsQuery>(qr => qr.GuildId == "guild-1" && qr.RequesterDiscordId == "user-1"), default))
            .ReturnsAsync(Result<List<GuildAttributionDefinitionResponse>>.Ok(response));

        var result = await MakeSut().GetDefinitions("guild-1", null, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetDefinitions_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetGuildAttributionDefinitionsQuery, List<GuildAttributionDefinitionResponse>>(It.IsAny<GetGuildAttributionDefinitionsQuery>(), default))
            .ReturnsAsync(Result<List<GuildAttributionDefinitionResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().GetDefinitions("guild-1", null, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetRaidZonesForGuild ─────────────────────────────────────────────────

    [Fact]
    public async Task GetRaidZonesForGuild_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetRaidZonesForGuild("guild-1", default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetRaidZonesForGuild_QuerySucceeds_ReturnsOk()
    {
        var response = new List<RaidZoneResponse>();
        _queries.Setup(q => q.DispatchAsync<GetRaidZonesForGuildQuery, List<RaidZoneResponse>>(
                It.Is<GetRaidZonesForGuildQuery>(qr => qr.GuildId == "guild-1" && qr.RequesterDiscordId == "user-1"), default))
            .ReturnsAsync(Result<List<RaidZoneResponse>>.Ok(response));

        var result = await MakeSut().GetRaidZonesForGuild("guild-1", default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetRaidZonesForGuild_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidZonesForGuildQuery, List<RaidZoneResponse>>(It.IsAny<GetRaidZonesForGuildQuery>(), default))
            .ReturnsAsync(Result<List<RaidZoneResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().GetRaidZonesForGuild("guild-1", default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetBossesForZone ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetBossesForZone_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).GetBossesForZone("guild-1", 4, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetBossesForZone_QuerySucceeds_ReturnsOk()
    {
        var response = new List<RaidBossResponse>();
        _queries.Setup(q => q.DispatchAsync<GetRaidBossesForZoneQuery, List<RaidBossResponse>>(
                It.Is<GetRaidBossesForZoneQuery>(qr => qr.GuildId == "guild-1" && qr.RequesterDiscordId == "user-1" && qr.RaidZoneId == 4), default))
            .ReturnsAsync(Result<List<RaidBossResponse>>.Ok(response));

        var result = await MakeSut().GetBossesForZone("guild-1", 4, default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetBossesForZone_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<GetRaidBossesForZoneQuery, List<RaidBossResponse>>(It.IsAny<GetRaidBossesForZoneQuery>(), default))
            .ReturnsAsync(Result<List<RaidBossResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().GetBossesForZone("guild-1", 4, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── CreateDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefinition_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new CreateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut(null).CreateDefinition("guild-1", command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task CreateDefinition_SetsGuildIdAndRequesterFromRouteAndClaim()
    {
        CreateGuildAttributionDefinitionCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateGuildAttributionDefinitionCommand>(), default))
            .Callback<CreateGuildAttributionDefinitionCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new CreateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut("user-1").CreateDefinition("guild-1", command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task CreateDefinition_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<CreateGuildAttributionDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.NoCellsInDefinition));
        var command = new CreateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut().CreateDefinition("guild-1", command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdateDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDefinition_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new UpdateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut(null).UpdateDefinition("guild-1", 7, command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateDefinition_SetsGuildIdRequesterAndDefinitionIdFromRoute()
    {
        UpdateGuildAttributionDefinitionCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildAttributionDefinitionCommand>(), default))
            .Callback<UpdateGuildAttributionDefinitionCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new UpdateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut("user-1").UpdateDefinition("guild-1", 7, command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.RequesterDiscordId.Should().Be("user-1");
        dispatched.DefinitionId.Should().Be(7);
    }

    [Fact]
    public async Task UpdateDefinition_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<UpdateGuildAttributionDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound));
        var command = new UpdateGuildAttributionDefinitionCommand { Label = "Innervate", Cells = Cells };

        var result = await MakeSut().UpdateDefinition("guild-1", 7, command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── DeleteDefinition ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDefinition_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).DeleteDefinition("guild-1", 7, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteDefinition_Succeeds_ReturnsOk()
    {
        DeleteGuildAttributionDefinitionCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteGuildAttributionDefinitionCommand>(), default))
            .Callback<DeleteGuildAttributionDefinitionCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));

        var result = await MakeSut("user-1").DeleteDefinition("guild-1", 7, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.RequesterDiscordId.Should().Be("user-1");
        dispatched.DefinitionId.Should().Be(7);
    }

    [Fact]
    public async Task DeleteDefinition_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<DeleteGuildAttributionDefinitionCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound));

        var result = await MakeSut().DeleteDefinition("guild-1", 7, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── ReorderDefinitions ───────────────────────────────────────────────────

    [Fact]
    public async Task ReorderDefinitions_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new ReorderGuildAttributionDefinitionsCommand { OrderedIds = [1, 2] };

        var result = await MakeSut(null).ReorderDefinitions("guild-1", command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ReorderDefinitions_Succeeds_ReturnsOk()
    {
        ReorderGuildAttributionDefinitionsCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ReorderGuildAttributionDefinitionsCommand>(), default))
            .Callback<ReorderGuildAttributionDefinitionsCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new ReorderGuildAttributionDefinitionsCommand { OrderedIds = [3, 1, 2] };

        var result = await MakeSut("user-1").ReorderDefinitions("guild-1", command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.RequesterDiscordId.Should().Be("user-1");
        dispatched.OrderedIds.Should().Equal(3, 1, 2);
    }

    [Fact]
    public async Task ReorderDefinitions_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<ReorderGuildAttributionDefinitionsCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.Forbidden));
        var command = new ReorderGuildAttributionDefinitionsCommand { OrderedIds = [] };

        var result = await MakeSut().ReorderDefinitions("guild-1", command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SetSectionIcon ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetSectionIcon_NoDiscordId_ReturnsUnauthorized()
    {
        var command = new SetAttributionSectionIconCommand { Section = "Interrupts", IconSource = AttributionIconSource.RaidMarker, RaidMarker = RaidMarkerIcon.Skull };

        var result = await MakeSut(null).SetSectionIcon("guild-1", command, default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SetSectionIcon_SetsGuildIdAndRequesterFromRouteAndClaim()
    {
        SetAttributionSectionIconCommand? dispatched = null;
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetAttributionSectionIconCommand>(), default))
            .Callback<SetAttributionSectionIconCommand, CancellationToken>((c, _) => dispatched = c)
            .ReturnsAsync(Result<CommandResponse>.Ok(new CommandResponse("ok")));
        var command = new SetAttributionSectionIconCommand { Section = "Interrupts", IconSource = AttributionIconSource.RaidMarker, RaidMarker = RaidMarkerIcon.Skull };

        var result = await MakeSut("user-1").SetSectionIcon("guild-1", command, default);

        result.Should().BeOfType<OkObjectResult>();
        dispatched.Should().NotBeNull();
        dispatched!.GuildId.Should().Be("guild-1");
        dispatched.RequesterDiscordId.Should().Be("user-1");
    }

    [Fact]
    public async Task SetSectionIcon_CommandFails_ReturnsBadRequest()
    {
        _commands.Setup(c => c.DispatchAsync(It.IsAny<SetAttributionSectionIconCommand>(), default))
            .ReturnsAsync(Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound));
        var command = new SetAttributionSectionIconCommand { Section = "Interrupts", IconSource = AttributionIconSource.RaidMarker, RaidMarker = RaidMarkerIcon.Skull };

        var result = await MakeSut().SetSectionIcon("guild-1", command, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── SearchSpells ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchSpells_NoDiscordId_ReturnsUnauthorized()
    {
        var result = await MakeSut(null).SearchSpells("guild-1", 2, "frappe", "fr", default);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task SearchSpells_QuerySucceeds_ReturnsOk()
    {
        var response = new List<SpellResponse>();
        _queries.Setup(q => q.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(
                It.Is<SearchSpellsQuery>(qr => qr.GuildId == "guild-1" && qr.RequesterDiscordId == "user-1" && qr.ExpansionId == 2 && qr.SearchTerm == "frappe" && qr.Locale == "fr"), default))
            .ReturnsAsync(Result<List<SpellResponse>>.Ok(response));

        var result = await MakeSut().SearchSpells("guild-1", 2, "frappe", "fr", default);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task SearchSpells_QueryFails_ReturnsBadRequest()
    {
        _queries.Setup(q => q.DispatchAsync<SearchSpellsQuery, List<SpellResponse>>(It.IsAny<SearchSpellsQuery>(), default))
            .ReturnsAsync(Result<List<SpellResponse>>.Fail(ResponseDetail.Forbidden));

        var result = await MakeSut().SearchSpells("guild-1", 2, "frappe", "fr", default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
