using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Plans;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="CreateRaidPlanPageCommand"/> by validating the officer's access and the target board, then appending a new page.</summary>
public class CreateRaidPlanPageCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidPlanPagesRepository pagesRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRaidPlanPageCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidPlanPageCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var plan = await plansRepository.GetByIdAsync(command.RaidPlanId, cancellationToken);
        if (plan == null || plan.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanNotFound, $"Raid plan '{command.RaidPlanId}' does not exist on this guild.");

        var page = new RaidPlanPage
        {
            RaidPlanId = command.RaidPlanId,
            Name = command.Name,
        };

        await pagesRepository.AddAsync(page, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidPlanUpdated,
            new Dictionary<string, string> { ["pageName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan page created successfully.", new { page.Id }));
    }
}
