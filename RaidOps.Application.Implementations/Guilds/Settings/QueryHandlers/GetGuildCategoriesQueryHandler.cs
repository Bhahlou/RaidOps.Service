using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Guilds.Settings.Queries;
using RaidOps.Application.Contracts.Guilds.Settings.Responses;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.Application.Implementations.Guilds.Settings.QueryHandlers;

/// <summary>
/// Handles <see cref="GetGuildCategoriesQuery"/> by verifying admin rights then returning the
/// guild's Discord channel categories from the bot's Gateway cache.
/// </summary>
public class GetGuildCategoriesQueryHandler(
    IGuildAccessService guildAccessService,
    IDiscordBotService discordBotService) : IQueryHandlerAsync<GetGuildCategoriesQuery, GuildCategoriesResponse>
{
    /// <inheritdoc/>
    public async Task<Result<GuildCategoriesResponse>> HandleAsync(GetGuildCategoriesQuery query, CancellationToken cancellationToken)
    {
        var accessLevel = await guildAccessService.GetAccessLevelAsync(query.RequesterDiscordId, query.GuildId, cancellationToken);
        if (accessLevel != GuildAccessLevel.Officer)
            return Result<GuildCategoriesResponse>.Fail(ResponseDetail.Forbidden, "User is not an admin of this guild.");

        try
        {
            var info = discordBotService.Guilds.GetCategories(query.GuildId, cancellationToken);
            var response = new GuildCategoriesResponse
            {
                CanCreateRootChannel = info.CanCreateRootChannel,
                Categories = [.. info.Categories.Select(c => new DiscordCategoryResponse
                {
                    Id = c.CategoryId.ToString(),
                    Name = c.Name,
                    CanCreateChannel = c.CanCreateChannel,
                })],
            };

            return Result<GuildCategoriesResponse>.Ok(response);
        }
        catch (InvalidOperationException)
        {
            return Result<GuildCategoriesResponse>.Fail(ResponseDetail.GuildBotNotPresent, "The RaidOps bot is not present in this guild.");
        }
    }
}
