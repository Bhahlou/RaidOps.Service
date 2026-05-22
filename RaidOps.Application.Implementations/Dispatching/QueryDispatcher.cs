using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.Common;
using RaidOps.Application.Contracts.CQRS;

namespace RaidOps.Application.Implementations.Dispatching
{
    public class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        public async Task<Result<TResponse>> DispatchAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQueryRequest<TResponse>
        {
            var handler = _serviceProvider.GetRequiredService<IQueryHandlerAsync<TQuery, TResponse>>();
            return await handler.HandleAsync(query, cancellationToken);
        }
    }
}