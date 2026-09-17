using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Raids.Attributions.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Raids.Attributions.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Raids.Attributions.CommandHandlers;

/// <summary>Handles <see cref="SetAttributionSectionIconCommand"/> by validating the officer's access and the icon fields, then applying it to every row sharing that section.</summary>
public class SetAttributionSectionIconCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildAttributionDefinitionsRepository definitionsRepository,
    ISpellRepository spellRepository,
    IAuditLogService auditLogService) : ICommandHandlerAsync<SetAttributionSectionIconCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(SetAttributionSectionIconCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an officer of this guild.");

        var validation = await AttributionDefinitionValidator.ValidateIconAsync(
            command.IconSource, command.SpellId, command.RaidMarker, command.StaticRole, spellRepository, cancellationToken, allowNone: true);
        if (validation != null)
            return Result<CommandResponse>.Fail(validation, "Invalid section icon fields.");

        var icon = new SectionIconFields(command.IconSource, command.SpellId, command.RaidMarker, command.StaticRole);
        var updated = await definitionsRepository.SetSectionIconAsync(command.GuildId, command.RaidBossId, command.Section, icon, cancellationToken);
        if (updated == 0)
            return Result<CommandResponse>.Fail(ResponseDetail.AttributionDefinitionNotFound, $"No row uses section '{command.Section}' in this scope.");

        await auditLogService.LogAsync(
            command.GuildId,
            command.RequesterDiscordId,
            GuildAuditAction.AttributionTemplateUpdated,
            new Dictionary<string, string> { ["section"] = command.Section },
            cancellationToken);

        return Result<CommandResponse>.Ok(new CommandResponse("Section icon updated successfully."));
    }
}
