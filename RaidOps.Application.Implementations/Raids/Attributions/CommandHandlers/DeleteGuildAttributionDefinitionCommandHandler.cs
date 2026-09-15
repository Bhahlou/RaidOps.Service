using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="DeleteGuildAttributionDefinitionCommand"/> by validating the officer's access, then permanently removing the template row and its per-event fills.</summary>
public class DeleteGuildAttributionDefinitionCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<DeleteGuildAttributionDefinitionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(DeleteGuildAttributionDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var deleted = await definitionsRepository.DeleteAsync(command.DefinitionId, command.GuildId, cancellationToken);
        if (!deleted)
            return Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound, $"Definition '{command.DefinitionId}' does not exist on this guild.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.AttributionTemplateUpdated,
            new Dictionary<string, string> { ["definitionId"] = command.DefinitionId.ToString() },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Attribution definition deleted successfully."));
    }
}
