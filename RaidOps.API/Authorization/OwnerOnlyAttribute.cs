using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.IdentityModel.Tokens.Jwt;

namespace RaidOps.API.Authorization;

/// <summary>
/// Restricts a controller or action to the application owner(s): the requester's own Discord ID (the JWT
/// <c>sub</c> claim) must be listed in <c>Admin:OwnerDiscordIds</c>. Used for operational endpoints that
/// aren't tied to any one guild, so no guild permission applies. Still relies on the normal JWT auth —
/// there is no separate admin login — and answers 401 without a <c>sub</c> claim, 403 for anyone else.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OwnerOnlyAttribute : Attribute, IAuthorizationFilter
{
    /// <inheritdoc/>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var discordId = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (discordId == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var ownerDiscordIds = (configuration["Admin:OwnerDiscordIds"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (!ownerDiscordIds.Contains(discordId))
            context.Result = new ForbidResult();
    }
}
