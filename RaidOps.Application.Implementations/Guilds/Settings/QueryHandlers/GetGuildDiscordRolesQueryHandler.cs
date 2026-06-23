using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildDiscordRolesQuery"/> by verifying admin rights then
/// returning the guild's assignable Discord roles from the bot's Gateway cache.
/// </summary>
public class GetGuildDiscordRolesQueryHandler(
    IGuildAccessService guildAccessService,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetGuildDiscordRolesQuery, List<DiscordRoleResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<DiscordRoleResponse>>> HandleAsync(GetGuildDiscordRolesQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<DiscordRoleResponse>>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        try
        {
            var roles = discordBotService.Guilds.GetRoles(query.GuildId, cancellationToken)
                .Select(r => new DiscordRoleResponse
                {
                    Id = r.Id.ToString(),
                    Name = r.Name,
                    Color = r.Colors?.PrimaryColor.RawValue ?? 0,
                    IconHash = r.IconHash
                })
                .ToList();

            return Result<List<DiscordRoleResponse>>.Ok(roles);
        }
        catch (InvalidOperationException)
        {
            return Result<List<DiscordRoleResponse>>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }
    }
}
