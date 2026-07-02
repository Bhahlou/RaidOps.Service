using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using RaidOps.Domain.Models.Character;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Resolves a requester's guild-derived access level on a character they don't own —
/// used to grant officers limited management rights over other players' characters.
/// </summary>
internal static class CharacterGuildAccessHelper
{
    /// <summary>
    /// Returns the highest <see cref="GuildAccessLevel"/> the requester holds across every guild
    /// the character is currently a roster member of (a character has at most one at a time, but
    /// this is not assumed). Returns <see cref="GuildAccessLevel.None"/> if the character is on no
    /// guild roster shared with the requester.
    /// </summary>
    internal static async Task<GuildAccessLevel> GetHighestAccessAsync(
        Character character,
        string requesterDiscordId,
        IGuildMembershipRepository membershipRepository,
        IGuildAccessService guildAccessService,
        CancellationToken cancellationToken)
    {
        var memberships = await membershipRepository.GetByCharacterIdAsync(character.Id, cancellationToken);

        var highest = GuildAccessLevel.None;
        foreach (var membership in memberships)
        {
            var level = await guildAccessService.GetAccessLevelAsync(requesterDiscordId, membership.GuildId, cancellationToken);
            if (level > highest)
                highest = level;
        }

        return highest;
    }
}
