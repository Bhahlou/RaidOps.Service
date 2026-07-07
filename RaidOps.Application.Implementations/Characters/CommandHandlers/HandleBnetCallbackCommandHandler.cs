using Microsoft.Extensions.Logging;
using RaidOps.Application.Contracts.Characters.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Domain.Models.Character;
using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;

namespace RaidOps.Application.Implementations.Characters.CommandHandlers;

/// <summary>
/// Handles <see cref="HandleBnetCallbackCommand"/> by validating the CSRF state token,
/// exchanging the authorization code for a BNet token, fetching the user's BattleTag,
/// and persisting the linked account.
/// </summary>
public class HandleBnetCallbackCommandHandler(
    IJwtService jwtService,
    IBnetApiService bnetApiService,
    IBnetAccountRepository bnetAccountRepository,
    ILogger<HandleBnetCallbackCommandHandler> logger)
    : ICommandHandlerAsync<HandleBnetCallbackCommand>
{
    /// <inheritdoc/>
    public async Task<Result<CommandResponse>> HandleAsync(
        HandleBnetCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate the CSRF state token
        var stateData = jwtService.ValidateBnetStateToken(command.State);
        if (stateData is null)
        {
            logger.LogWarning(
                "BNet callback failed for discord user {DiscordId}: invalid or expired state token",
                command.DiscordId);
            return Result<CommandResponse>.Fail(ResponseDetail.InvalidState);
        }

        if (stateData.Value.DiscordId != command.DiscordId)
        {
            logger.LogWarning(
                "BNet callback failed: state token discord id {StateDiscordId} does not match request discord id {DiscordId}",
                stateData.Value.DiscordId, command.DiscordId);
            return Result<CommandResponse>.Fail(ResponseDetail.StateMismatch);
        }

        var region = stateData.Value.Region;

        // 2. Exchange code and link account
        try
        {
            var tokenResponse = await bnetApiService.ExchangeCodeAsync(
                command.Code, command.CallbackUrl, region, cancellationToken);

            var userInfo = await bnetApiService.GetUserInfoAsync(
                tokenResponse.AccessToken, region, cancellationToken);

            await bnetAccountRepository.UpsertAsync(new BattleNetAccount
            {
                UserDiscordId = command.DiscordId,
                BnetId = userInfo.Id.ToString(),
                BattleTag = userInfo.BattleTag,
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                Region = region
            }, cancellationToken);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "BNet account linked for discord user {DiscordId}: bnetId {BnetId}, region {Region}",
                    command.DiscordId, userInfo.Id, region);
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "BNet callback failed for discord user {DiscordId}: BNet API call failed for region {Region}",
                command.DiscordId, region);
            return Result<CommandResponse>.Fail(ResponseDetail.BnetApiError);
        }

        return Result<CommandResponse>.Ok(new CommandResponse("BNet account linked successfully."));
    }
}
