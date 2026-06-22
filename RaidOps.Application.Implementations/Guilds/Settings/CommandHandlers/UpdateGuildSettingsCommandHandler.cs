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
/// Handles <see cref="UpdateGuildSettingsCommand"/> by verifying admin rights,
/// confirming the guild is registered, then persisting the settings.
/// </summary>
public class UpdateGuildSettingsCommandHandler(
    IUserGuildsRepository userGuildsRepository,
    IGuildsRepository guildsRepository,
    IDiscordBotService discordBotService,
    IAuditLogService auditLogService) : ICommandHandlerAsync<UpdateGuildSettingsCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(UpdateGuildSettingsCommand command, CancellationToken cancellationToken = default)
    {
        var userGuilds = await userGuildsRepository.GetByUserDiscordIdAsync(command.RequesterDiscordId, cancellationToken);
        var membership = userGuilds.FirstOrDefault(g => g.GuildId == command.GuildId);

        if (membership == null || !membership.IsAdmin)
            return Result<CommandResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        var guild = await guildsRepository.GetByIdAsync(command.GuildId, cancellationToken);
        if (guild == null)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotFound, $"Guild '{command.GuildId}' does not exist.");

        if (!guild.IsRegistered)
            return Result<CommandResponse>.Fail(ResponseDetail.GuildNotRegistered, "Guild is not registered in RaidOps.");

        // Captured before UpdateSettingsAsync: it mutates this same tracked entity in place.
        var oldTimezone = guild.Timezone;
        var oldRosterMode = guild.RosterMode;
        var oldMinRosterRoleId = guild.MinRosterRoleId;

        await guildsRepository.UpdateSettingsAsync(
            command.GuildId,
            command.Timezone,
            command.RosterMode,
            command.MinRosterRoleId,
            cancellationToken);

        // Mirrors GuildsRepository.UpdateSettingsAsync, which clears the role unless DiscordRoleOnly.
        var newMinRosterRoleId = command.RosterMode == RosterMode.DiscordRoleOnly ? command.MinRosterRoleId : null;

        var variables = new Dictionary<string, string>();
        var changedFields = new List<string>();

        if (oldTimezone != command.Timezone)
        {
            changedFields.Add("timezone");
            if (oldTimezone != null)
                variables["oldTimezone"] = oldTimezone;
            variables["newTimezone"] = command.Timezone;
        }

        if (oldRosterMode != command.RosterMode)
        {
            changedFields.Add("rosterMode");
            if (oldRosterMode != null)
                variables["oldRosterMode"] = oldRosterMode.ToString()!;
            variables["newRosterMode"] = command.RosterMode.ToString();
        }

        // Only meaningful when the role threshold still applies in the new state — switching to
        // Open makes any prior role threshold moot, so there's nothing worth logging about it.
        if (command.RosterMode == RosterMode.DiscordRoleOnly && oldMinRosterRoleId != newMinRosterRoleId)
        {
            changedFields.Add("minRosterRoleId");
            var roles = TryGetRoles(command.GuildId, cancellationToken);
            if (oldMinRosterRoleId != null)
                AddRoleVariables(variables, "old", roles, oldMinRosterRoleId);
            if (newMinRosterRoleId != null)
                AddRoleVariables(variables, "new", roles, newMinRosterRoleId);
        }

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

        return Result<CommandResponse>.Ok(new CommandResponse("Guild settings updated successfully."));
    }

    /// <summary>
    /// Fetches the guild's Discord roles from the bot's Gateway cache, or null if the bot isn't
    /// in the guild — callers fall back to a placeholder rather than failing the settings update.
    /// </summary>
    private List<Role>? TryGetRoles(string guildId, CancellationToken cancellationToken)
    {
        try
        {
            return discordBotService.Guilds.GetRoles(guildId, cancellationToken).ToList();
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

        variables[$"{prefix}MinRosterRoleName"] = role.Name;

        var color = role.Colors?.PrimaryColor.RawValue ?? 0;
        if (color != 0)
            variables[$"{prefix}MinRosterRoleColor"] = color.ToString();

        // Full CDN URL (not just the hash) so the front end never needs the role ID at all.
        if (role.IconHash != null)
            variables[$"{prefix}MinRosterRoleIconUrl"] = $"https://cdn.discordapp.com/role-icons/{role.Id}/{role.IconHash}.webp?size=32";
    }
}
