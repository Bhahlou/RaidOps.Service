using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Raids.Attributions;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="CreateGuildAttributionDefinitionCommand"/> by validating the officer's access, the target boss (if any), and the icon-source fields, then appending a new template row.</summary>
public class CreateGuildAttributionDefinitionCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildBranchesRepository guildBranchesRepository,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    ISpellRepository spellRepository,
    IRaidBossRepository raidBossRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<CreateGuildAttributionDefinitionCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(CreateGuildAttributionDefinitionCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, command.GuildBranchId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild branch.");

        var expansionId = await guildBranchesRepository.GetCurrentExpansionIdAsync(command.GuildId, command.GuildBranchId, cancellationToken);
        if (expansionId is null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildBranchNotFound, "Guild branch not found.");

        if (command.RaidBossId != null && await raidBossRepository.GetByIdAsync(command.RaidBossId.Value, cancellationToken) == null)
            return Result<CommandResponse>.Fail(ResponseDetail.RaidBossNotFound, $"Raid boss '{command.RaidBossId}' does not exist.");

        var validation = await AttributionDefinitionValidator.ValidateAsync(command.Cells, expansionId.Value, spellRepository, cancellationToken);
        if (validation != null)
            return Result<CommandResponse>.Fail(validation, "Invalid attribution definition fields.");

        var definition = new GuildAttributionDefinition
        {
            GuildId = command.GuildId,
            GuildBranchId = command.GuildBranchId,
            Label = command.Label,
            Section = command.Section,
            IsRepeatable = command.IsRepeatable,
            RaidBossId = command.RaidBossId,
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
