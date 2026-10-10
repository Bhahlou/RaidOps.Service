using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using RaidOps.ExternalApplication.Contracts.Services.DiscordBot;

namespace RaidOps.ExternalApplication.Implementations.Bot.Services;

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

    /// <inheritdoc/>
    public IEnumerable<DiscordChannelInfo> GetChannels(string guildId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        var botUser = gatewayClient.Cache.User
            ?? throw new InvalidOperationException("Bot user not yet available in the Gateway cache.");
        guild.Users.TryGetValue(botUser.Id, out var botMember);

        return [.. guild.Channels.Values
            .Where(c => c is TextGuildChannel or AnnouncementGuildChannel)
            .Select(c =>
            {
                var permissions = botMember is not null
                    ? PartialGuildUserExtensions.GetChannelPermissions(botMember, guild, c.Id)
                    : default;

                List<DiscordChannelPermissionFlag> missingPermissions = [];
                if (!permissions.HasFlag(Permissions.ViewChannel)) missingPermissions.Add(DiscordChannelPermissionFlag.ViewChannel);
                if (!permissions.HasFlag(Permissions.SendMessages)) missingPermissions.Add(DiscordChannelPermissionFlag.SendMessages);
                if (!permissions.HasFlag(Permissions.EmbedLinks)) missingPermissions.Add(DiscordChannelPermissionFlag.EmbedLinks);

                var categoryName = c is TextGuildChannel { ParentId: { } parentId }
                    && guild.Channels.TryGetValue(parentId, out var parent)
                        ? parent.Name
                        : null;

                return new DiscordChannelInfo(c.Id, c.Name, missingPermissions, categoryName);
            })];
    }

    /// <inheritdoc/>
    public DiscordCategoriesInfo GetCategories(string guildId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        var botUser = gatewayClient.Cache.User
            ?? throw new InvalidOperationException("Bot user not yet available in the Gateway cache.");
        guild.Users.TryGetValue(botUser.Id, out var botMember);

        var basePermissions = GetBasePermissions(guild, botMember);

        // Manage Channels at the base guild level is required to create a channel outside of any
        // category — a category-scoped overwrite (checked per-category below) doesn't cover this.
        var canCreateRootChannel = basePermissions.HasFlag(Permissions.Administrator) || basePermissions.HasFlag(Permissions.ManageChannels);

        var categories = guild.Channels.Values
            .OfType<CategoryGuildChannel>()
            .OrderBy(c => c.Position)
            .Select(c =>
            {
                var permissions = botMember is not null
                    ? PartialGuildUserExtensions.GetChannelPermissions(botMember, guild, c.Id)
                    : default;

                var canCreateChannel = permissions.HasFlag(Permissions.Administrator) || permissions.HasFlag(Permissions.ManageChannels);
                return new DiscordCategoryInfo(c.Id, c.Name, canCreateChannel);
            })
            .ToList();

        return new DiscordCategoriesInfo(canCreateRootChannel, categories);
    }

    /// <inheritdoc/>
    public async Task<DiscordChannelInfo> CreateTextChannelAsync(string guildId, string name, string? categoryId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        var properties = new GuildChannelProperties(name, ChannelType.TextGuildChannel);
        if (categoryId is not null)
            properties.ParentId = ulong.Parse(categoryId);

        var channel = await gatewayClient.Rest.CreateGuildChannelAsync(ulong.Parse(guildId), properties, cancellationToken: cancellationToken);

        var botUser = gatewayClient.Cache.User
            ?? throw new InvalidOperationException("Bot user not yet available in the Gateway cache.");
        guild.Users.TryGetValue(botUser.Id, out var botMember);

        // Computed from the REST response's own overwrites rather than the Gateway cache, which
        // hasn't necessarily caught up with a channel created a moment ago.
        var permissions = botMember is not null
            ? PartialGuildUserExtensions.GetChannelPermissions(botMember, GetBasePermissions(guild, botMember), channel)
            : default;

        List<DiscordChannelPermissionFlag> missingPermissions = [];
        if (!permissions.HasFlag(Permissions.ViewChannel)) missingPermissions.Add(DiscordChannelPermissionFlag.ViewChannel);
        if (!permissions.HasFlag(Permissions.SendMessages)) missingPermissions.Add(DiscordChannelPermissionFlag.SendMessages);
        if (!permissions.HasFlag(Permissions.EmbedLinks)) missingPermissions.Add(DiscordChannelPermissionFlag.EmbedLinks);

        var categoryName = categoryId is not null && guild.Channels.TryGetValue(ulong.Parse(categoryId), out var category)
            ? category.Name
            : null;

        return new DiscordChannelInfo(channel.Id, channel.Name, missingPermissions, categoryName);
    }

    /// <inheritdoc/>
    public async Task DeleteChannelAsync(string channelId, CancellationToken cancellationToken = default) =>
        await gatewayClient.Rest.DeleteChannelAsync(ulong.Parse(channelId), cancellationToken: cancellationToken);

    /// <summary>
    /// Computes the bot's base guild permissions (its roles' permissions OR'd together, before any
    /// channel-specific overwrite is applied) — the same base Discord itself starts from when
    /// resolving effective per-channel permissions.
    /// </summary>
    private static Permissions GetBasePermissions(Guild guild, GuildUser? botMember)
    {
        var permissions = default(Permissions);
        if (botMember is null)
            return permissions;

        if (guild.Roles.TryGetValue(guild.Id, out var everyoneRole))
            permissions |= everyoneRole.Permissions;
        foreach (var roleId in botMember.RoleIds)
        {
            if (guild.Roles.TryGetValue(roleId, out var role))
                permissions |= role.Permissions;
        }

        return permissions;
    }

    /// <inheritdoc/>
    public GuildUser? GetUser(string guildId, string userId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        return guild.Users.TryGetValue(ulong.Parse(userId), out var user) ? user : null;
    }

    /// <inheritdoc/>
    public string? GetPreferredLocale(string guildId, CancellationToken cancellationToken = default)
    {
        if (!gatewayClient.Cache.Guilds.TryGetValue(ulong.Parse(guildId), out var guild))
            throw new InvalidOperationException($"Guild '{guildId}' not found in bot cache.");

        return guild.PreferredLocale;
    }
}
