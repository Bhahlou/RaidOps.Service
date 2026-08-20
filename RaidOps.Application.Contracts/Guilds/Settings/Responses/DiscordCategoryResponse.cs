namespace RaidOps.Application.Contracts.Guilds.Settings.Responses;

/// <summary>A Discord channel category, returned by <see cref="Queries.GetGuildCategoriesQuery"/>.</summary>
public class DiscordCategoryResponse
{
    /// <summary>Discord snowflake ID of the category.</summary>
    public required string Id { get; set; }

    /// <summary>Display name of the category.</summary>
    public required string Name { get; set; }

    /// <summary>Whether the bot currently holds Manage Channels on this category — false blocks channel creation there.</summary>
    public bool CanCreateChannel { get; set; }
}
