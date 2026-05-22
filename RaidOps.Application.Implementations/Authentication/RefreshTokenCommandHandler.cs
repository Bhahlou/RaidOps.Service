using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.Application.Implementations.Authentication;

/// <summary>
/// Handles <see cref="RefreshTokenCommand"/> by delegating to <see cref="IRaidOpsAuthService"/>
/// and wrapping the result in a <see cref="CommandResponse"/>.
/// </summary>
public class RefreshTokenCommandHandler(IRaidOpsAuthService authService) : ICommandHandlerAsync<RefreshTokenCommand>
{
    /// <summary>
    /// Executes the token-refresh flow and returns a command response that embeds
    /// the new <see cref="RaidOps.Application.Contracts.Authentication.Responses.AuthenticationResponse"/> on success.
    /// </summary>
    /// <param name="command">The refresh-token command containing the existing refresh JWT.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> with a <see cref="CommandResponse"/> containing
    /// the new token pair, or a failed result with the error message.
    /// </returns>
    public async Task<Result<CommandResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        var result = await authService.HandleRefreshTokenAsync(command, cancellationToken);

        return result.IsSuccess
            ? Result<CommandResponse>.Ok(new CommandResponse("Token refreshed", result.Value))
            : Result<CommandResponse>.Fail(result.Error!);
    }
}
