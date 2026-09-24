using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;

/// <summary>
/// Handles <see cref="DeleteRaidCompositionPreviewCommand"/> by verifying officer access and
/// permanently removing a preview — its slots are cascade-deleted with it at the database level.
/// </summary>
public class DeleteRaidCompositionPreviewCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteRaidCompositionPreviewCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteRaidCompositionPreviewCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var existing = await raidCompositionPreviewsRepository.GetByIdAsync(command.PreviewId, command.GuildBranchId, cancellationToken);
        if (existing == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound, $"Raid composition preview '{command.PreviewId}' does not exist.");

        await raidCompositionPreviewsRepository.DeleteAsync(command.PreviewId, command.GuildBranchId, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidCompositionPreviewUpdated,
            new Dictionary<string, string> { ["previewName"] = existing.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid composition preview deleted successfully."));
    }
}
