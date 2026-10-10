using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Contracts.Dev.Commands;

/// <summary>
/// Dev-only command that resets the calling user's onboarding progress for a guild, so the
/// get-started flow can be replayed from scratch without manually deleting the Battle.net
/// account/characters and unregistering the guild by hand. Never reachable outside a Development
/// environment — see <see cref="RaidOps.API.Controllers.v1.DevController"/>.
/// </summary>
public class ResetGuildOnboardingCommand : ICommandRequest
{
    /// <summary>The Discord snowflake ID of the user whose onboarding progress is reset. Set by the controller, not from the request body.</summary>
    public string UserDiscordId { get; set; } = string.Empty;

    /// <summary>The Discord snowflake ID of the guild to unregister as part of the reset. Set by the controller, not from the request body.</summary>
    public string GuildId { get; set; } = string.Empty;
}
