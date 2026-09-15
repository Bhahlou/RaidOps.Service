using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="CreateGuildAttributionDefinitionCommand"/> by validating the officer's access and the icon-source fields, then appending a new template row.</summary>
public class CreateGuildAttributionDefinitionCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    ISpellRepository spellRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateGuildAttributionDefinitionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateGuildAttributionDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var validation = await AttributionDefinitionValidator.ValidateAsync(command.Cells, spellRepository, cancellationToken);
        if (validation != null)
            return Result<CommandResponse>.Fail(validation, "Invalid attribution definition fields.");

        var definition = new GuildAttributionDefinition
        {
            GuildId = command.GuildId,
            Label = command.Label,
            Section = command.Section,
            IsRepeatable = command.IsRepeatable,
            Cells = AttributionCellMapper.ToEntities(command.Cells),
            CreatedAt = DateTime.UtcNow,
            CreatedByDiscordId = command.RequesterDiscordId,
        };

        await definitionsRepository.AddAsync(definition, cancellationToken);

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.AttributionTemplateUpdated,
            new Dictionary<string, string> { ["label"] = command.Label },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Attribution definition created successfully."));
    }
}
