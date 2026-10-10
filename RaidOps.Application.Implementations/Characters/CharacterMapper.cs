using RaidOps.Application.Contracts.Characters.Responses;
using RaidOps.Application.Contracts.Guilds.Memberships.Responses;
using RaidOps.Domain.Models.Character;

namespace RaidOps.Application.Implementations.Characters;

/// <summary>
/// Centralises mapping from <see cref="Character"/> domain entities to response DTOs.
/// </summary>
internal static class CharacterMapper
{
    /// <summary>
    /// Maps a <see cref="Character"/> to a <see cref="CharacterDto"/>.
    /// Prefers the active expansion state; falls back to the highest-level state.
    /// </summary>
    internal static CharacterDto ToDto(Character c)
    {
        var activeState = c.ExpansionStates.FirstOrDefault(s => s.IsActive)
                       ?? c.ExpansionStates.OrderByDescending(s => s.Level).FirstOrDefault();

        return new CharacterDto
        {
            Id         = c.Id,
            Name       = c.Name,
            ClassId    = c.ClassId,
            ClassName  = c.Class.Name,
            ClassColor = "#" + c.Class.Color,
            RaceId     = c.RaceId,
            RaceName   = c.Race.Name,
            Faction    = c.Faction.ToString().ToUpperInvariant(),
            BranchName = c.Branch.Name,
            RealmName  = c.Realm.Name,
            RealmSlug  = c.Realm.Slug,
            Level      = activeState?.Level ?? 0,
            ItemLevel  = activeState?.ItemLevel,
            AvatarUrl  = c.AvatarUrl,
            GuildName  = activeState?.GuildName,
            BnetSpecs  = (activeState?.Specs ?? [])
                .OrderByDescending(s => s.IsMain)
                .Select(s => new BnetCharacterSpecDto
                {
                    SpecId  = s.SpecId,
                    Name    = s.Spec.Name,
                    IconUrl = s.Spec.IconUrl,
                    IsMain  = s.IsMain,
                })
                .ToList(),
            RaidSpecs  = c.RaidSpecs
                .OrderByDescending(rs => rs.IsMain)
                .Select(rs => new CharacterRaidSpecDto
                {
                    SpecId  = rs.SpecId,
                    Name    = rs.Spec.Name,
                    IconUrl = rs.Spec.IconUrl,
                    IsMain  = rs.IsMain,
                })
                .ToList(),
            GuildMemberships = c.GuildMemberships
                .Select(m => new GuildMembershipResponse
                {
                    GuildId       = m.GuildId,
                    GuildBranchId = m.GuildBranchId,
                    GuildName     = m.Guild.Name,
                    GuildIconHash = m.Guild.IconHash,
                    CharacterRank = m.CharacterRank,
                    JoinedAt      = m.JoinedAt,
                })
                .ToList(),
        };
    }
}
