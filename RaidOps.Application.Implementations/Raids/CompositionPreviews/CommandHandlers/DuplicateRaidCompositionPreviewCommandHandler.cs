using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;

/// <summary>
/// Handles <see cref="DuplicateRaidCompositionPreviewCommand"/> by verifying officer access and
/// cloning the source preview's grid shape and every slot under a new name.
/// </summary>
public class DuplicateRaidCompositionPreviewCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DuplicateRaidCompositionPreviewCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DuplicateRaidCompositionPreviewCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var source = await raidCompositionPreviewsRepository.GetByIdAsync(command.PreviewId, command.GuildBranchId, cancellationToken);
        if (source == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound, $"Raid composition preview '{command.PreviewId}' does not exist.");

        var clone = new RaidCompositionPreview
        {
            GuildBranchId = command.GuildBranchId,
            Name = command.NewName,
            GroupCount = source.GroupCount,
            SlotsPerGroup = source.SlotsPerGroup,
            CreatedByDiscordId = command.RequesterDiscordId,
            CreatedAt = DateTime.UtcNow,
            Slots = [.. source.Slots.Select(s => new RaidCompositionPreviewSlot
            {
                GroupNumber = s.GroupNumber,
                SlotNumber = s.SlotNumber,
                WowClassId = s.WowClassId,
                SpecId = s.SpecId,
                Note = s.Note,
            })],
        };

        var created = await raidCompositionPreviewsRepository.AddAsync(clone, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidCompositionPreviewUpdated,
            new Dictionary<string, string> { ["previewName"] = command.NewName, ["duplicatedFrom"] = source.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid composition preview duplicated successfully.", new { created.Id }));
    }
}
