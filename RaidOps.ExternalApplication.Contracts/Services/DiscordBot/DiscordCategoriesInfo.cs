namespace RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

/// <summary>The guild's Discord channel categories, along with the bot's channel-creation reach.</summary>
/// <param name="CanCreateRootChannel">
/// Whether the bot holds Manage Channels as a base guild permission — required to create a channel
/// outside of any category. A category can independently grant Manage Channels via its own
/// permission overwrite (see <see cref="DiscordCategoryInfo.CanCreateChannel"/>), so a bot scoped
/// to specific categories can still have this be false.
/// </param>
/// <param name="Categories">The guild's categories, ordered the same way Discord's channel list sorts them.</param>
public record DiscordCategoriesInfo(bool CanCreateRootChannel, IReadOnlyList<DiscordCategoryInfo> Categories);
