using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="ReorderGuildAttributionDefinitionsCommand"/> by validating the officer's access, then re-numbering the template's display order.</summary>
public class ReorderGuildAttributionDefinitionsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildAttributionDefinitionsRepository definitionsRepository) : ICommandHandlerAsync<ReorderGuildAttributionDefinitionsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(ReorderGuildAttributionDefinitionsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        await definitionsRepository.ReorderAsync(command.GuildId, command.GuildBranchId, command.OrderedIds, cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Attribution definitions reordered successfully."));
    }
}
