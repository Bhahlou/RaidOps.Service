using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.CQRS;
using RaidOps.Application.Contracts.Services;
using RaidOps.Application.Implementations.Dispatching;
using Scrutor;
using System.Reflection;

namespace RaidOps.Registry
{
    public static class RaidOpsRegistry
    {
        public static IServiceCollection AddRaidOps(this IServiceCollection services, IConfiguration configuration)
        {
            var assemblies = GetAssemblies();

            services.ScanHandlersAndProviders(assemblies);
            services.AddDispatchers();

            services.SetupApplicationRegistry();
            services.SetupExternalApplicationsRegistry(configuration);
            services.SetupPersistenceRegistry(configuration);

            return services;
        }

        // Also scans INotificationSignalProvider implementations: each domain drops in its own
        // provider class and it's picked up automatically, exactly like a new CQRS handler —
        // callers (IUserNotificationService) never need to know which domains exist.
        private static void ScanHandlersAndProviders(this IServiceCollection services, Assembly[] assemblies)
        {
            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(filter => filter.AssignableToAny(
                    typeof(ICommandHandlerAsync<>),
                    typeof(IQueryHandlerAsync<,>),
                    typeof(INotificationSignalProvider)))
                .UsingRegistrationStrategy(RegistrationStrategy.Append)
                .AsImplementedInterfaces()
                .WithScopedLifetime());
        }
        private static void AddDispatchers(this IServiceCollection services)
        {
            services.AddScoped<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<IQueryDispatcher, QueryDispatcher>();
        }

        private static Assembly[] GetAssemblies() =>
        [
            Assembly.Load("RaidOps.Application.Contracts"),
            Assembly.Load("RaidOps.Application.Implementations"),
        ];
    }
}