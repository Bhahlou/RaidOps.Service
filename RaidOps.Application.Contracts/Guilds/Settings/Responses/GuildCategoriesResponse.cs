namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>The guild's Discord channel categories, returned by <see cref="Queries.GetGuildCategoriesQuery"/>.</summary>
public class GuildCategoriesResponse
{
    /// <summary>Whether the bot holds Manage Channels as a base guild permission — required to create a channel outside of any category.</summary>
    public bool CanCreateRootChannel { get; set; }

    /// <summary>The guild's categories, ordered the same way Discord's channel list sorts them.</summary>
    public List<DiscordCategoryResponse> Categories { get; set; } = [];
}
