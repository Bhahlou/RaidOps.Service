using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateGuildSettingsCommand"/> by verifying admin rights,
/// confirming the guild is registered, then persisting the guild-level identity settings.
/// </summary>
public class UpdateGuildSettingsCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IAuditLogService auditLogService,
    ILogger<UpdateGuildSettingsCommandHandler> logger) : ICommandHandlerAsync<UpdateGuildSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        // Captured before UpdateSettingsAsync: it mutates this same tracked entity in place.
        var oldTimezone = guild.Timezone;
        var oldLanguage = guild.Language;

        await guildsRepository.UpdateSettingsAsync(
            command.GuildId,
            command.Timezone,
            command.Language,
            cancellationToken);

        var variables = new Dictionary<string, string>();
        var changedFields = new List<string>();

        RecordChange(variables, changedFields, "timezone", "Timezone", oldTimezone, command.Timezone);
        RecordChange(variables, changedFields, "language", "Language", oldLanguage, command.Language);

        if (changedFields.Count > 0)
        {
            variables["changedFields"] = string.Join(',', changedFields);
            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.SettingsUpdated,
                variables,
                cancellationToken);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Guild {GuildId} settings updated by discord user {DiscordId}: fields [{ChangedFields}]",
                command.GuildId, command.RequesterDiscordId, string.Join(", ", changedFields));
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Guild settings updated successfully."));
    }

    /// <summary>
    /// Records a scalar field change: adds <paramref name="changedFieldKey"/> to
    /// <paramref name="changedFields"/> and the old/new values to <paramref name="variables"/>,
    /// or does nothing if the value didn't change. The old value is omitted when null
    /// (first-time configuration), so the audit log doesn't show a meaningless "from nothing".
    /// </summary>
    private static void RecordChange<T>(
        Dictionary<string, string> variables,
        List<string> changedFields,
        string changedFieldKey,
        string variableSuffix,
        T? oldValue,
        T newValue)
    {
        if (Equals(oldValue, newValue))
            return;

        changedFields.Add(changedFieldKey);
        if (oldValue is not null)
            variables[$"old{variableSuffix}"] = oldValue.ToString()!;
        variables[$"new{variableSuffix}"] = newValue!.ToString()!;
    }
}
