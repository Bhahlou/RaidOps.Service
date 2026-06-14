using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot;

/// <summary>
/// Provides read access to Discord guild data from the bot's Gateway cache.
/// All methods operate on the in-memory snapshot maintained by the <see cref="GatewayClient"/>;
/// no REST calls are made.
/// </summary>
public class GuildService(GatewayClient gatewayClient) : IGuildService
{
    /// <inheritdoc/>
    public Guild Get(string guildId, CancellationToken cancellationToken = default)
    {
        if (gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            return guild;

        throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");
    }

    /// <inheritdoc/>
    public IEnumerable<GuildUser> GetUsers(string guildId, CancellationToken cancellationToken = default)
    {
        if (gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            return guild.Users.Values;
        
        
        throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");
    }

    /// <inheritdoc/>
    public IEnumerable<GuildUser> GetAdmins(string guildId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        var adminRoleIds = guild.Roles
            .Where(r => !r.Value.Managed && r.Value.Permissions.HasFlag(Permissions.Administrator))
            .Select(r => r.Key)
            .ToHashSet();

        var admins = guild.Users.Values
            .Where(u => u.RoleIds.Any(roleId => adminRoleIds.Contains(roleId)))
            .ToList();

        // The guild owner is always an admin even without an explicit role.
        var owner = guild.Users.Values.FirstOrDefault(u => u.Id == guild.OwnerId);
        if (owner is not null && admins.All(u => u.Id != owner.Id))
            admins.Add(owner);

        return admins;
    }

    /// <inheritdoc/>
    public IEnumerable<Role> GetRoles(string guildId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        // Exclude @everyone (same snowflake as the guild) and bot-managed integration roles.
        return [.. guild.Roles.Values
            .Where(r => r.Id != guild.Id && !r.Managed)
            .OrderByDescending(r => r.Position)];
    }
}
