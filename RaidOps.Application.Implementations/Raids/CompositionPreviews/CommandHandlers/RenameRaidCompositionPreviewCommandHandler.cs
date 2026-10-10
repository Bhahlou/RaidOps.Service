using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.CompositionPreviews.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.CompositionPreviews.CommandHandlers;

/// <summary>Handles <see cref="RenameRaidCompositionPreviewCommand"/> by verifying officer access and renaming the preview.</summary>
public class RenameRaidCompositionPreviewCommandHandler(
    IGuildAccessService guildAccessService,
    IRaidCompositionPreviewsRepository raidCompositionPreviewsRepository) : ICommandHandlerAsync<RenameRaidCompositionPreviewCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(RenameRaidCompositionPreviewCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var renamed = await raidCompositionPreviewsRepository.RenameAsync(command.PreviewId, command.GuildBranchId, command.Name, cancellationToken);
        if (!renamed)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidCompositionPreviewNotFound, $"Raid composition preview '{command.PreviewId}' does not exist.");

        return Result<CommandResponse>.Ok(new CommandResponse("Raid composition preview renamed successfully."));
    }
}
