using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateRaidCompositionPreviewSlotCommand"/> by verifying officer access and
/// upserting (or, if everything ends up empty, clearing) a single grid coordinate. Note content is
/// never validated beyond the entity's max length — free text, no restriction.
/// </summary>
public class UpdateRaidCompositionPreviewSlotCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository,
    IWowClassRepository wowClassRepository,
    ISpecRepository specRepository) : ICommandHandlerAsync<UpdateRaidCompositionPreviewSlotCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateRaidCompositionPreviewSlotCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var preview = await raidCompositionPreviewsRepository.GetByIdAsync(command.PreviewId, command.GuildBranchId, cancellationToken);
        if (preview == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound, $"Raid composition preview '{command.PreviewId}' does not exist.");

        if (command.GroupNumber < 1 || command.GroupNumber > preview.GroupCount || command.SlotNumber < 1 || command.SlotNumber > preview.SlotsPerGroup)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupOrSlotNumber, "Group/slot number is out of the preview's grid bounds.");

        var (effectiveWowClassId, classResolutionFailure) = await ResolveEffectiveClassIdAsync(command, cancellationToken);
        if (classResolutionFailure != null)
            return classResolutionFailure;

        if (effectiveWowClassId == null && command.SpecId == null && string.IsNullOrWhiteSpace(command.Note))
        {
            await raidCompositionPreviewsRepository.ClearSlotAsync(command.PreviewId, command.GroupNumber, command.SlotNumber, cancellationToken);
            return Result<CommandResponse>.Ok(new CommandResponse("Slot cleared successfully."));
        }

        await raidCompositionPreviewsRepository.UpsertSlotAsync(new RaidCompositionPreviewSlot
        {
            RaidCompositionPreviewId = command.PreviewId,
            GroupNumber = command.GroupNumber,
            SlotNumber = command.SlotNumber,
            WowClassId = effectiveWowClassId,
            SpecId = command.SpecId,
            Note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note,
        }, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Slot updated successfully."));
    }

    /// <summary>
    /// Resolves the class ID that should end up on the slot, validating that the requested spec (if
    /// any) exists and belongs to the requested class (if any). Returns the failure result directly
    /// rather than a <see cref="Result{TSuccess}"/> of the class ID itself, since <c>null</c> is a
    /// valid resolved class (no class chosen) and <see cref="Result{TSuccess}.Ok"/> rejects nulls.
    /// </summary>
    private async Task<(int? ClassId, Result<CommandResponse>? Failure)> ResolveEffectiveClassIdAsync(UpdateRaidCompositionPreviewSlotCommand command, CancellationToken cancellationToken)
    {
        if (command.SpecId != null)
        {
            var specs = await specRepository.GetAllAsync(cancellationToken);
            var spec = specs.FirstOrDefault(s => s.Id == command.SpecId);
            if (spec == null)
                return (null, Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, $"Spec '{command.SpecId}' does not exist."));

            if (command.WowClassId != null && command.WowClassId != spec.ClassId)
                return (null, Result<CommandResponse>.Fail(ResponseDetail.SpecClassMismatch, "The chosen spec does not belong to the chosen class."));

            return (spec.ClassId, null);
        }

        if (command.WowClassId != null)
        {
            var classes = await wowClassRepository.GetAllAsync(cancellationToken);
            if (!classes.Any(c => c.Id == command.WowClassId))
                return (null, Result<CommandResponse>.Fail(ResponseDetail.InvalidRequest, $"Class '{command.WowClassId}' does not exist."));
        }

        return (command.WowClassId, null);
    }
}
