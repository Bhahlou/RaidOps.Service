using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Queries;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.QueryHandlers;

/// <summary>Handles <see cref="GetRaidCompositionPreviewQuery"/> by returning a single preview with its full sparse slot grid.</summary>
public class GetRaidCompositionPreviewQueryHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository) : IQueryHandlerAsync<GetRaidCompositionPreviewQuery, RaidCompositionPreviewResponse>
{
    /// <inheritdoc/>
    public async Task<Result<RaidCompositionPreviewResponse>> HandleAsync(GetRaidCompositionPreviewQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, query.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<RaidCompositionPreviewResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var preview = await raidCompositionPreviewsRepository.GetByIdAsync(query.PreviewId, query.GuildBranchId, cancellationToken);
        if (preview == null)
            return Result<RaidCompositionPreviewResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound, $"Raid composition preview '{query.PreviewId}' does not exist.");

        return Result<RaidCompositionPreviewResponse>.Ok(new RaidCompositionPreviewResponse
        {
            Id = preview.Id,
            Name = preview.Name,
            GroupCount = preview.GroupCount,
            SlotsPerGroup = preview.SlotsPerGroup,
            Slots = [.. preview.Slots.Select(MapSlot)],
        });
    }

    private static RaidCompositionPreviewSlotResponse MapSlot(RaidCompositionPreviewSlot slot) => new()
    {
        GroupNumber = slot.GroupNumber,
        SlotNumber = slot.SlotNumber,
        WowClassId = slot.WowClassId,
        WowClassName = slot.WowClass?.Name,
        WowClassColor = slot.WowClass != null ? "#" + slot.WowClass.Color : null,
        SpecId = slot.SpecId,
        SpecName = slot.Spec?.Name,
        SpecIconUrl = slot.Spec?.IconUrl,
        Note = slot.Note,
    };
}
