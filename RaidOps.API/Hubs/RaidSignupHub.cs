using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Hubs;

/// <summary>
/// Group-scoped hub a raid board client joins per Signup-mode event it's currently displaying, so
/// it gets pushed a live nudge whenever anyone's response changes — from the web or from the
/// Discord signup-call embed's buttons alike, since both paths funnel through the same
/// <c>SetMyRaidSignupCommandHandler</c>, which calls <see cref="RaidSignupNotifier"/>. Unlike
/// <see cref="AuthHub"/>'s implicit per-user targeting (via the JWT <c>sub</c> claim), a raid
/// event's scope isn't derivable from the token, so the client explicitly joins/leaves via
/// <see cref="JoinRaidEvent"/>/<see cref="LeaveRaidEvent"/> — each re-checked against
/// <see cref="IGuildAccessService"/> so a connection can't join a group for a branch its user
/// isn't on the roster of.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class RaidSignupHub(IGuildAccessService guildAccessService) : Hub
{
    /// <summary>Group name for one raid event's signup pushes — shared by <see cref="RaidSignupHub"/> and <see cref="RaidSignupNotifier"/>.</summary>
    public static string GroupName(int guildBranchId, int eventId) => $"raid-signup:{guildBranchId}:{eventId}";

    /// <summary>Joins the calling connection to <paramref name="eventId"/>'s push group, if the caller holds at least Roster access on <paramref name="guildBranchId"/>.</summary>
    public async Task JoinRaidEvent(string guildId, int guildBranchId, int eventId)
    {
        var discordId = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
            return;

        var accessLevel = await guildAccessService.GetAccessLevelAsync(discordId, guildId, guildBranchId);
        if (accessLevel < GuildAccessLevel.Roster)
            return;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(guildBranchId, eventId));
    }

    /// <summary>Leaves <paramref name="eventId"/>'s push group — called when the client stops displaying that event (panel scrolled out of the visible range, navigated away).</summary>
    public Task LeaveRaidEvent(int guildBranchId, int eventId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(guildBranchId, eventId));
}
