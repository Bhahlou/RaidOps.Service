using NetCord;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Commands;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Guilds.Settings.CommandHandlers;

/// <summary>
/// Handles <see cref="UpdateOfficerThresholdCommand"/> by verifying admin rights, confirming the
/// guild is registered, then persisting the Officer role threshold. Mirrors
/// <see cref="UpdateGuildSettingsCommandHandler"/>'s handling of the roster role threshold —
/// no strict validation against the bot's role list, best-effort audit log resolution only.
/// </summary>
public class UpdateOfficerThresholdCommandHandler(
    IGuildAccessService guildAccessService,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateOfficerThresholdCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateOfficerThresholdCommand command, CancellationToken cancellationToken = default)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(command.RequesterDiscordId, command.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        var oldMinOfficerRoleId = guild.MinOfficerRoleId;

        await guildsRepository.UpdateOfficerThresholdAsync(command.GuildId, command.MinOfficerRoleId, cancellationToken);

        if (oldMinOfficerRoleId != command.MinOfficerRoleId)
        {
            var roles = TryGetRoles(command.GuildId, cancellationToken);
            var variables = new Dictionary<string, string> { ["changedFields"] = "minOfficerRoleId" };
            if (oldMinOfficerRoleId != null)
                AddRoleVariables(variables, "old", roles, oldMinOfficerRoleId);
            AddRoleVariables(variables, "new", roles, command.MinOfficerRoleId);

            await auditLogService.LogAsync(
                command.GuildId,
                command.RequesterDiscordId,
                GuildAuditAction.OfficerThresholdUpdated,
                variables,
                cancellationToken);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("Officer threshold updated successfully."));
    }

    /// <summary>
    /// Fetches the guild's Discord roles from the bot's Gateway cache, or null if the bot isn't
    /// in the guild — callers fall back to a placeholder rather than failing the update.
    /// </summary>
    private List<Role>? TryGetRoles(string guildId, CancellationToken cancellationToken)
    {
        try
        {
            return [.. discordBotService.Guilds.GetRoles(guildId, cancellationToken)];
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a Discord role's display info (name, color, icon) and adds it to
    /// <paramref name="variables"/> under the given prefix — a raw role ID means nothing to a
    /// human reading the audit log.
    /// </summary>
    private static void AddRoleVariables(Dictionary<string, string> variables, string prefix, List<Role>? roles, string roleId)
    {
        var role = roles?.FirstOrDefault(r => r.Id.ToString() == roleId);
        if (role == null)
            return;

        variables[$"{prefix}MinOfficerRoleName"] = role.Name;

        var color = role.Colors?.PrimaryColor.RawValue ?? 0;
        if (color != 0)
            variables[$"{prefix}MinOfficerRoleColor"] = color.ToString();

        if (role.IconHash != null)
            variables[$"{prefix}MinOfficerRoleIconUrl"] = $"https://cdn.discordapp.com/role-icons/{role.Id}/{role.IconHash}.webp?size=32";
    }
}
