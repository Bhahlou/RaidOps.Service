using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.CompositionPreviews;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;

/// <summary>
/// Handles <see cref="CreateRaidCompositionPreviewCommand"/> by verifying officer access and
/// persisting a new preview with an empty grid sized from the requested group count (groups of 5).
/// </summary>
public class CreateRaidCompositionPreviewCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateRaidCompositionPreviewCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateRaidCompositionPreviewCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        if (command.GroupCount < 1 || command.GroupCount > 8)
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidGroupCount, "Group count must be between 1 and 8.");

        var preview = new RaidCompositionPreview
        {
            GuildBranchId = command.GuildBranchId,
            Name = command.Name,
            GroupCount = command.GroupCount,
            SlotsPerGroup = 5,
            CreatedByDiscordId = command.RequesterDiscordId,
            CreatedAt = DateTime.UtcNow,
        };

        var created = await raidCompositionPreviewsRepository.AddAsync(preview, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.RaidCompositionPreviewUpdated,
            new Dictionary<string, string> { ["previewName"] = command.Name },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Raid composition preview created successfully.", new { created.Id }));
    }
}
