using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildNotificationChannelsQuery"/> by verifying admin rights then
/// returning the guild's text-postable Discord channels from the bot's Gateway cache.
/// </summary>
public class GetGuildNotificationChannelsQueryHandler(
    IGuildAccessService guildAccessService,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetGuildNotificationChannelsQuery, List<DiscordChannelResponse>>
{
    /// <inheritdoc/>
    public async Task<Result<List<DiscordChannelResponse>>> HandleAsync(GetGuildNotificationChannelsQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<List<DiscordChannelResponse>>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        try
        {
            var channels = discordBotService.Guilds.GetChannels(query.GuildId, cancellationToken)
                .Select(c => new DiscordChannelResponse
                {
                    Id = c.ChannelId.ToString(),
                    Name = c.Name,
                    MissingPermissions = [.. c.MissingPermissions],
                    CategoryName = c.CategoryName,
                })
                .ToList();

            return Result<List<DiscordChannelResponse>>.Ok(channels);
        }
        catch (InvalidOperationException)
        {
            return Result<List<DiscordChannelResponse>>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }
    }
}
