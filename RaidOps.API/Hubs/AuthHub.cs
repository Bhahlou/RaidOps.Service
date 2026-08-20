using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RaidOps.API.Hubs;

/// <summary>
/// Push-only hub used to notify a connected user that their Discord data may have changed,
/// so the front-end can proactively re-fetch <c>/user/me</c>. Clients never call methods on
/// this hub — the server only ever pushes to it via <see cref="AuthNotifier"/>.
/// </summary>
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class AuthHub : Hub;
