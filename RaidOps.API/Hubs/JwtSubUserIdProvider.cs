using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Hubs;

/// <summary>
/// Maps a hub connection to a SignalR user ID using the JWT <c>sub</c> claim (the Discord ID),
/// instead of SignalR's default <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/> —
/// RaidOps JWTs keep the raw short claim names (see <c>Program.MapInboundClaims = false</c>), so
/// the default provider would never find a match and <c>Clients.User(discordId)</c> would be a no-op.
/// </summary>
public class JwtSubUserIdProvider : IUserIdProvider
{
    /// <inheritdoc/>
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
