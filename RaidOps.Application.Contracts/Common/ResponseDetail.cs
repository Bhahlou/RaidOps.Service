using System.Diagnostics.CodeAnalysis;

namespace RaidOps.Application.Contracts.Common;

/// <summary>
/// Centralises all error codes returned by command and query handlers.
/// Use these constants with <see cref="Result{T}.Fail"/> instead of inline strings.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ResponseDetail
{
    // ── Generic ───────────────────────────────────────────────────────────
    public const string NotFound = nameof(NotFound);
    public const string Forbidden = nameof(Forbidden);
    public const string Unauthorized = nameof(Unauthorized);
    public const string InvalidRequest = nameof(InvalidRequest);

    // ── Auth ──────────────────────────────────────────────────────────────
    public const string UserNotFound = nameof(UserNotFound);
    public const string InvalidRefreshToken = nameof(InvalidRefreshToken);
    public const string InvalidTokenClaims = nameof(InvalidTokenClaims);

    // ── Battle.net ────────────────────────────────────────────────────────
    public const string BnetNotLinked = nameof(BnetNotLinked);
    public const string BnetTokenExpired = nameof(BnetTokenExpired);
    public const string BnetApiError = nameof(BnetApiError);
    public const string InvalidState = nameof(InvalidState);
    public const string StateMismatch = nameof(StateMismatch);

    // ── Guild ─────────────────────────────────────────────────────────────
    public const string GuildNotFound = nameof(GuildNotFound);
    public const string GuildBotNotPresent = nameof(GuildBotNotPresent);
    public const string GuildNotRegistered = nameof(GuildNotRegistered);

    // ── Branch ────────────────────────────────────────────────────────────
    public const string BranchNotFound = nameof(BranchNotFound);
}
