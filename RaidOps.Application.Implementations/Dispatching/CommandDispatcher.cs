using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;


namespace RaidOps.Application.Implementations.Dispatching
{
    public class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task<Result<CommandResponse>> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommandRequest
        {
            var handler = _serviceProvider.GetRequiredService<ICommandHandlerAsync<TCommand>>();
            return await handler.HandleAsync(command, cancellationToken);
        }
    }
}