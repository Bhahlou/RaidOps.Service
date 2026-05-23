using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Infrastructure.Persistence.Contracts.Repositories;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.Infrastructure.Persistence.Implementations.Repositories;

namespace RaidOps.Registry;

internal static class PersistenceRegistry
{
    internal static IServiceCollection SetupPersistenceRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RaidOpsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IGuildsRepository, GuildsRepository>();
        services.AddScoped<IUserGuildsRepository, UserGuildsRepository>();
        services.AddScoped<IBnetAccountRepository, BnetAccountRepository>();

        return services;
    }
}
