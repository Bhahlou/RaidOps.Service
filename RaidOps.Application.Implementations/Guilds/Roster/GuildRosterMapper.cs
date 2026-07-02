using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Guilds.Roster.Responses;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Application.Implementations.Guilds.Roster;

/// <summary>
/// Maps <see cref="GuildMembership"/> domain entities to <see cref="GuildRosterMemberResponse"/> DTOs.
/// </summary>
internal static class GuildRosterMapper
{
    /// <summary>
    /// Maps a <see cref="GuildMembership"/> to a <see cref="GuildRosterMemberResponse"/>, enriched
    /// with the owning player's Discord display info from <paramref name="usersById"/>.
    /// </summary>
    internal static GuildRosterMemberResponse ToDto(GuildMembership membership, Dictionary<string, User> usersById)
    {
        var character = membership.Character;
        usersById.TryGetValue(character.UserDiscordId, out var player);

        var activeState = character.ExpansionStates.FirstOrDefault(s => s.IsActive)
                       ?? character.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

        return new GuildRosterMemberResponse
        {
            CharacterId      = character.Id,
            CharacterName    = character.Name,
            ClassId          = character.ClassId,
            ClassName        = character.Class.Name,
            ClassColor       = "#" + character.Class.Color,
            Level            = activeState?.Level ?? 0,
            BranchName       = character.Branch.Name,
            RealmSlug        = character.Realm.Slug,
            AvatarUrl        = character.AvatarUrl,
            PlayerDiscordId  = character.UserDiscordId,
            PlayerName       = player?.Name,
            PlayerAvatarHash = player?.AvatarHash,
            RaidSpecs        = character.RaidSpecs
                .OrderByDescending(rs => rs.IsMain)
                .Select(rs => new CharacterRaidSpecDto
                {
                    SpecId  = rs.SpecId,
                    Name    = rs.Spec.Name,
                    IconUrl = rs.Spec.IconUrl,
                    IsMain  = rs.IsMain,
                })
                .ToList(),
            CharacterRank    = membership.CharacterRank,
            JoinedAt         = membership.JoinedAt,
        };
    }
}
