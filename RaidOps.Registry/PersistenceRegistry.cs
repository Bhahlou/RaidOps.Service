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
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<ISpecRepository, SpecRepository>();
        services.AddScoped<IRealmRepository, RealmRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IGuildMembershipRepository, GuildMembershipRepository>();
        services.AddScoped<IGuildAuditLogRepository, GuildAuditLogRepository>();
        services.AddScoped<INotificationDismissalRepository, NotificationDismissalRepository>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();

        return services;
    }
}
