using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Plans;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>Handles <see cref="CreateRaidPlanCommand"/> by validating the officer's access and the target boss, then creating a new strategy board.</summary>
public class CreateRaidPlanCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlansRepository plansRepository,
    IRaidBossRepository raidBossRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRaidPlanCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidPlanCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        if (await raidBossRepository.GetByIdAsync(command.RaidBossId, cancellationToken) == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidBossNotFound, $"Raid boss '{command.RaidBossId}' does not exist.");

        var plan = new RaidPlan
        {
            GuildId = command.GuildId,
            RaidBossId = command.RaidBossId,
            Name = command.Name,
            CreatedAt = DateTime.UtcNow,
            CreatedByDiscordId = command.RequesterDiscordId,
        };

        await plansRepository.AddAsync(plan, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidPlanUpdated,
            new Dictionary<string, string> { ["name"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan created successfully.", new { plan.Id }));
    }
}
