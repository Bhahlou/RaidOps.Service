namespace RaidOps.Application.Contracts.Common;

/// <summary>
/// Centralises all error codes returned by command and query handlers.
/// Use these constants with <see cref="Result{T}.Fail"/> instead of inline strings.
/// </summary>
public static class ResponseDetail
{
    // ── Generic ───────────────────────────────────────────────────────────
    public const string NotFound = nameof(NotFound);
    public const string Forbidden = nameof(Forbidden);

    // ── Auth ──────────────────────────────────────────────────────────────
    public const string UserNotFound = nameof(UserNotFound);
    public const string InvalidRefreshToken = nameof(InvalidRefreshToken);
    public const string InvalidTokenClaims = nameof(InvalidTokenClaims);

    // ── Battle.net ────────────────────────────────────────────────────────
    public const string BnetNotLinked = nameof(BnetNotLinked);
    public const string BnetTokenExpired = nameof(BnetTokenExpired);

    // ── Guild ─────────────────────────────────────────────────────────────
    public const string GuildNotFound = nameof(GuildNotFound);
    public const string GuildBotNotPresent = nameof(GuildBotNotPresent);

    // ── Branch ────────────────────────────────────────────────────────────
    public const string BranchNotFound = nameof(BranchNotFound);
}
