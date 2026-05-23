using Microsoft.EntityFrameworkCore;
using RaidOps.Domain.Models.Discord;

namespace RaidOps.Infrastructure.Persistence.Implementations;

/// <summary>
/// Entity Framework Core database context for RaidOps, exposing the core domain sets
/// and configuring the relational model for composite keys and navigation properties.
/// </summary>
public class RaidOpsDbContext(DbContextOptions<RaidOpsDbContext> options) : DbContext(options)
{
    /// <summary>Gets the <see cref="User"/> table.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Gets the <see cref="Guild"/> table.</summary>
    public DbSet<Guild> Guilds => Set<Guild>();

    /// <summary>Gets the <see cref="UserGuild"/> join table that links users to Discord guilds.</summary>
    public DbSet<UserGuild> UserGuilds => Set<UserGuild>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserGuild>()
            .HasKey(ug => new { ug.UserDiscordId, ug.GuildId });

        modelBuilder.Entity<UserGuild>()
            .HasOne(ug => ug.User)
            .WithMany(u => u.UserGuilds)
            .HasForeignKey(ug => ug.UserDiscordId);

        modelBuilder.Entity<UserGuild>()
            .HasOne(ug => ug.Guild)
            .WithMany(g => g.UserGuilds)
            .HasForeignKey(ug => ug.GuildId);
    }
}
