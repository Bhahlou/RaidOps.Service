using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Plans.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Plans.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Plans.CommandHandlers;

/// <summary>
/// Handles <see cref="SaveRaidPlanPageElementsCommand"/> by validating the officer's access, the
/// target page, and every element's fields, then replacing the page's element list wholesale —
/// the one bulk mutation the canvas editor's Save button fires.
/// </summary>
public class SaveRaidPlanPageElementsCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidPlanPagesRepository pagesRepository,
    IRaidPlanElementsRepository elementsRepository,
    ISpellRepository spellRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<SaveRaidPlanPageElementsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SaveRaidPlanPageElementsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var page = await pagesRepository.GetByIdAsync(command.RaidPlanPageId, cancellationToken);
        if (page == null || page.RaidPlanId != command.RaidPlanId || page.RaidPlan.GuildId != command.GuildId)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidPlanPageNotFound, $"Page '{command.RaidPlanPageId}' does not exist on this plan.");

        var validation = await RaidPlanElementValidator.ValidateAsync(command.Elements, spellRepository, cancellationToken);
        if (validation != null)
            return Result<CommandResponse>.Fail(validation, "Invalid raid plan element fields.");

        await elementsRepository.SaveAsync(command.RaidPlanPageId, RaidPlanElementMapper.ToEntities(command.Elements), cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidPlanUpdated,
            new Dictionary<string, string> { ["raidPlanPageId"] = command.RaidPlanPageId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid plan page saved successfully."));
    }
}
