namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>A Discord channel category, as seen from the bot's Gateway cache.</summary>
/// <param name="CategoryId">Discord snowflake ID of the category.</param>
/// <param name="Name">Category name.</param>
/// <param name="CanCreateChannel">
/// Whether the bot currently holds Manage Channels on this category — Discord lets that permission
/// be granted via an overwrite scoped to just one category, so this can be true even when
/// <see cref="DiscordCategoriesInfo.CanCreateRootChannel"/> is false.
/// </param>
public record DiscordCategoryInfo(ulong CategoryId, string Name, bool CanCreateChannel);
