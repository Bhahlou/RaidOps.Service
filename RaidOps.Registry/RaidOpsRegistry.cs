using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Application.Contracts.CQRS;
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

            services.ScanHandlers(assemblies);
            services.AddDispatchers();

            services.SetupApplicationRegistry();
            services.SetupExternalApplicationsRegistry(configuration);
            services.SetupPersistenceRegistry(configuration);

            return services;
        }

        private static void ScanHandlers(this IServiceCollection services, Assembly[] assemblies)
        {
            services.Scan(scan => scan
                .FromAssemblies(assemblies)
                .AddClasses(filter => filter.AssignableToAny(
                    typeof(ICommandHandlerAsync<>),
                    typeof(IQueryHandlerAsync<,>)))
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