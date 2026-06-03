using RaidOps.Application.Contracts.Authentication.Commands;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;

namespace RaidOps.Application.Implementations.Authentication.CommandHandlers;

/// <summary>
/// Handles <see cref="SignupCommand"/> by delegating to <see cref="IRaidOpsAuthService"/>
/// and wrapping the result in a <see cref="CommandResponse"/>.
/// </summary>
public class SignupCommandHandler(IRaidOpsAuthService authService) : ICommandHandlerAsync<SignupCommand>
{
    /// <summary>
    /// Executes the sign-up flow and returns a command response that embeds the
    /// <see cref="Contracts.Authentication.Responses.AuthenticationResponse"/> in its body on success.
    /// </summary>
    /// <param name="command">The sign-up command to process.</param>
    /// <param name="cancellationToken">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> with a <see cref="CommandResponse"/> containing
    /// the authentication payload, or a failed result with the error message.
    /// </returns>
    public async Task<Result<CommandResponse>> HandleAsync(SignupCommand command, CancellationToken cancellationToken = default)
    {
        var result = await authService.HandleSignupAsync(command, cancellationToken);

        return result.IsSuccess
            ? Result<CommandResponse>.Ok(new CommandResponse("Authentication successful", result.Value))
            : Result<CommandResponse>.Fail(result.Error!);
    }
}
