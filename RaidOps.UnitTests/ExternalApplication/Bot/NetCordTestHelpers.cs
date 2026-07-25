using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using NetCord;
using NetCord.Gateway;
using NetCord.JsonModels;
using NetCord.Rest;
using Moq;

namespace RaidOps.UnitTests.ExternalApplication.Bot;

/// <summary>
/// Reflection-based factory for NetCord concrete types that have no public constructors.
/// Required because GatewayClient, Guild, GuildUser and Role are all sealed/concrete
/// and cannot be mocked with Moq.
/// </summary>
internal static class NetCordTestHelpers
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // ── GatewayClient ────────────────────────────────────────────────────────

    internal static GatewayClient MakeGatewayClient(
        IGatewayClientCache cache,
        RestClient? restClient = null)
    {
        var client = Uninitialized<GatewayClient>();
        SetField(client, typeof(GatewayClient), "<Cache>k__BackingField", cache);
        if (restClient is not null)
            SetField(client, typeof(GatewayClient), "<Rest>k__BackingField", restClient);
        return client;
    }

    // ── RestClient ────────────────────────────────────────────────────────────

    internal static (RestClient rest, Mock<IRestRequestHandler> handler) MakeFakeRestClient()
    {
        var handler = new Mock<IRestRequestHandler>();

        // IRateLimitManager and related types are not in the public NetCord.Rest namespace
        // we're referenced against — set via reflection using Type.GetType to bypass this.
        var rateLimitManager = BuildRateLimitManager();

        var rest = Uninitialized<RestClient>();
        SetField(rest, typeof(RestClient), "_requestHandler",   handler.Object);
        SetField(rest, typeof(RestClient), "_rateLimitManager", rateLimitManager);
        SetField(rest, typeof(RestClient), "_baseUrl",          "https://discord.com/api/v10");
        return (rest, handler);
    }

    private static NoOpProxy BuildRateLimitManager()
    {
        var restAsm = typeof(RestClient).Assembly;

        var globalLimiterType = restAsm.GetTypes().First(t => t.Name == "IGlobalRateLimiter");
        var routeLimiterType  = restAsm.GetTypes().First(t => t.Name == "IRouteRateLimiter");
        var rlmType           = restAsm.GetTypes().First(t => t.Name == "IRateLimitManager");

        // Create no-op global limiter via DispatchProxy
        var globalLimiter = CreateNoOpProxy(globalLimiterType);
        var routeLimiter  = CreateNoOpProxy(routeLimiterType,
            getBool: name => name == "HasBucketInfo" ? false : (bool?)null);

        // Create no-op IRateLimitManager via DispatchProxy
        return CreateNoOpProxy(rlmType,
            getObject: name => name switch
            {
                "GetGlobalRateLimiterAsync" => globalLimiter,
                "GetRouteRateLimiterAsync"  => routeLimiter,
                _                           => null,
            });
    }

    /// <summary>
    /// Creates a DispatchProxy that returns defaults for all interface methods.
    /// ValueTask-returning methods return completed tasks; object-returning methods
    /// use the optional <paramref name="getObject"/> delegate.
    /// </summary>
    private static NoOpProxy CreateNoOpProxy(
        Type interfaceType,
        Func<string, bool?>? getBool = null,
        Func<string, object?>? getObject = null)
    {
        var proxy = (NoOpProxy)typeof(DispatchProxy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "Create" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(interfaceType, typeof(NoOpProxy))
            .Invoke(null, null)!;

        proxy.GetBoolResult  = getBool;
        proxy.GetObjectResult = getObject;
        return proxy;
    }

    internal static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // ── Guild ─────────────────────────────────────────────────────────────────

    internal static Guild MakeGuild(
        ulong guildId,
        ulong ownerId,
        IReadOnlyDictionary<ulong, GuildUser> users,
        JsonRole[]? roles = null,
        IReadOnlyDictionary<ulong, IGuildChannel>? channels = null,
        string? preferredLocale = null,
        Permissions? permissions = null)
    {
        var jsonGuild = Uninitialized<JsonGuild>();
        SetField(jsonGuild, typeof(JsonGuild).BaseType!, "<Id>k__BackingField", guildId);
        SetField(jsonGuild, typeof(JsonGuild), "<OwnerId>k__BackingField", ownerId);
        if (preferredLocale is not null)
            SetField(jsonGuild, typeof(JsonGuild), "<PreferredLocale>k__BackingField", preferredLocale);
        // NetCord's GetChannelPermissions(Guild) computes from this summary field (the acting
        // user's own guild-level permission bit), not from per-role aggregation — a null value
        // means "partial guild" and makes permission calculation throw.
        SetField(jsonGuild, typeof(JsonGuild), "<Permissions>k__BackingField", permissions);

        var guild = Uninitialized<Guild>();
        // _jsonModel is declared on RestGuild (Guild base) — GetField searches up hierarchy
        SetField(guild, typeof(Guild), "_jsonModel", jsonGuild);
        SetField(guild, typeof(Guild), "<Users>k__BackingField", users);
        SetField(guild, typeof(Guild), "<Channels>k__BackingField", channels ?? new Dictionary<ulong, IGuildChannel>());

        // <Roles>k__BackingField is on RestGuild — build IReadOnlyDictionary<ulong, Role>
        var rolesDict = (roles ?? [])
            .ToDictionary(r => r.Id, r => MakeRole(r, guildId))
            as IReadOnlyDictionary<ulong, Role>;
        SetField(guild, typeof(Guild), "<Roles>k__BackingField", rolesDict);

        return guild;
    }

    /// <summary>Builds a minimal text channel with no permission overwrites — pair with an Administrator role on the acting member to bypass overwrite computation entirely.</summary>
    internal static TextGuildChannel MakeTextChannel(ulong id, string name, ulong guildId, ulong? parentId = null)
    {
        var jsonChannelType = Type.GetType("NetCord.JsonModels.JsonChannel, NetCord")!;
        var jc = RuntimeHelpers.GetUninitializedObject(jsonChannelType);
        SetField(jc, jsonChannelType.BaseType!, "<Id>k__BackingField", id);
        SetField(jc, jsonChannelType, "<Name>k__BackingField", name);
        if (parentId.HasValue)
            SetField(jc, jsonChannelType, "<ParentId>k__BackingField", (ulong?)parentId.Value);

        var channel = Uninitialized<TextGuildChannel>();
        SetField(channel, typeof(TextGuildChannel).BaseType!.BaseType!, "_jsonModel", jc);
        SetField(channel, typeof(TextGuildChannel), "<GuildId>k__BackingField", guildId);
        SetField(channel, typeof(TextGuildChannel), "<PermissionOverwrites>k__BackingField", new Dictionary<ulong, PermissionOverwrite>());

        return channel;
    }

    // ── CurrentUser (bot's own user) ────────────────────────────────────────────

    internal static CurrentUser MakeCurrentUser(ulong userId)
    {
        var jsonUserType = Type.GetType("NetCord.JsonModels.JsonUser, NetCord")!;
        var ju = RuntimeHelpers.GetUninitializedObject(jsonUserType);
        SetField(ju, jsonUserType.BaseType!, "<Id>k__BackingField", userId);

        var user = Uninitialized<CurrentUser>();
        SetField(user, typeof(CurrentUser).BaseType!, "_jsonModel", ju);
        return user;
    }

    private static Role MakeRole(JsonRole jsonRole, ulong guildId)
    {
        var role = Uninitialized<Role>();
        SetField(role, typeof(Role), "_jsonModel", jsonRole);
        SetField(role, typeof(Role), "<GuildId>k__BackingField", guildId);

        // Role.Colors is a cached RoleColors class reference populated during construction.
        // GetUninitializedObject skips constructors, so we must mirror it manually.
        var jsonColors = typeof(JsonRole)
            .GetField("<Colors>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(jsonRole);
        if (jsonColors is not null)
        {
            var ctor = typeof(RoleColors)
                .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .First();
            SetField(role, typeof(Role), "<Colors>k__BackingField", ctor.Invoke([jsonColors]));
        }

        return role;
    }

    // ── GuildUser ─────────────────────────────────────────────────────────────

    internal static GuildUser MakeGuildUser(
        ulong userId,
        ulong guildId,
        ulong[] roleIds,
        string? username = null,
        string? nickname = null,
        string? globalName = null,
        string? avatarHash = null,
        string? guildAvatarHash = null)
    {
        var jsonGuildUserType = Type.GetType("NetCord.JsonModels.JsonGuildUser, NetCord")!;
        var jsonUserType      = Type.GetType("NetCord.JsonModels.JsonUser, NetCord")!;

        // JsonGuildUser: set RoleIds
        var jgu = RuntimeHelpers.GetUninitializedObject(jsonGuildUserType);
        SetField(jgu, jsonGuildUserType, "<RoleIds>k__BackingField", roleIds);
        if (nickname is not null)
            SetField(jgu, jsonGuildUserType, "<Nickname>k__BackingField", nickname);
        if (guildAvatarHash is not null)
            SetField(jgu, jsonGuildUserType, "<GuildAvatarHash>k__BackingField", guildAvatarHash);

        // JsonUser: set Id (via JsonEntity base)
        var ju = RuntimeHelpers.GetUninitializedObject(jsonUserType);
        SetField(ju, jsonUserType.BaseType!, "<Id>k__BackingField", userId);
        if (username is not null)
            SetField(ju, jsonUserType, "<Username>k__BackingField", username);
        if (globalName is not null)
            SetField(ju, jsonUserType, "<GlobalName>k__BackingField", globalName);
        if (avatarHash is not null)
            SetField(ju, jsonUserType, "<AvatarHash>k__BackingField", avatarHash);

        // GuildUser: set both _jsonModel fields and guildId
        var user   = Uninitialized<GuildUser>();
        SetField(user, typeof(GuildUser), "<guildId>P", guildId);

        // GuildUser has two fields named _jsonModel — disambiguate by FieldType
        var allFields = typeof(GuildUser)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

        allFields.First(f => f.Name == "_jsonModel" && f.FieldType == jsonGuildUserType)
            .SetValue(user, jgu);
        allFields.First(f => f.Name == "_jsonModel" && f.FieldType == jsonUserType)
            .SetValue(user, ju);

        return user;
    }

    // ── Role ──────────────────────────────────────────────────────────────────

    internal static JsonRole MakeJsonRole(ulong id, Permissions permissions, bool managed = false, int? primaryColor = null, int position = 0, string? name = null, string? iconHash = null)
    {
        var r = Uninitialized<JsonRole>();
        SetField(r, typeof(JsonRole).BaseType!, "<Id>k__BackingField", id);
        SetField(r, typeof(JsonRole), "<Permissions>k__BackingField", permissions);
        SetField(r, typeof(JsonRole), "<Managed>k__BackingField", managed);
        SetField(r, typeof(JsonRole), "<Position>k__BackingField", position);
        if (name is not null)
            SetField(r, typeof(JsonRole), "<Name>k__BackingField", name);
        if (iconHash is not null)
            SetField(r, typeof(JsonRole), "<IconHash>k__BackingField", iconHash);

        if (primaryColor.HasValue)
        {
            var jsonRoleColorsType = Type.GetType("NetCord.JsonModels.JsonRoleColors, NetCord")!;
            var jsonColors = RuntimeHelpers.GetUninitializedObject(jsonRoleColorsType);
            SetField(jsonColors, jsonRoleColorsType, "<PrimaryColor>k__BackingField", new Color(primaryColor.Value));
            // JsonRoleColors is a reference type — set directly (no Nullable wrapper needed)
            SetField(r, typeof(JsonRole), "<Colors>k__BackingField", jsonColors);
        }

        return r;
    }

    // ── IGatewayClientCache mock ───────────────────────────────────────────────

    internal static Mock<IGatewayClientCache> CacheWith(params (ulong id, Guild guild)[] guilds)
    {
        var dict  = guilds.ToDictionary(g => g.id, g => g.guild);
        var cache = new Mock<IGatewayClientCache>();
        cache.Setup(c => c.Guilds).Returns(dict);
        return cache;
    }

    /// <summary>Same as <see cref="CacheWith"/>, but also stubs <c>Cache.User</c> with the bot's own current user — required by <c>GuildService.GetChannels</c>.</summary>
    internal static Mock<IGatewayClientCache> CacheWith(CurrentUser botUser, params (ulong id, Guild guild)[] guilds)
    {
        var cache = CacheWith(guilds);
        cache.Setup(c => c.User).Returns(botUser);
        return cache;
    }

    internal static Mock<IGatewayClientCache> EmptyCache()
    {
        var cache = new Mock<IGatewayClientCache>();
        cache.Setup(c => c.Guilds)
             .Returns(new Dictionary<ulong, Guild>());
        return cache;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static T Uninitialized<T>() =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void SetField(object target, Type startType, string name, object? value)
    {
        // GetField with NonPublic does NOT traverse to base class private fields automatically.
        // Walk up the hierarchy manually.
        var t = startType;
        while (t != null && t != typeof(object))
        {
            var field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field is not null)
            {
                field.SetValue(target, value);
                return;
            }
            t = t.BaseType;
        }
        throw new InvalidOperationException($"Field '{name}' not found in hierarchy of {startType.Name}");
    }
}

/// <summary>
/// Generic DispatchProxy that returns sensible defaults for all interface methods,
/// used to stub out NetCord internal interfaces (IRateLimitManager, IGlobalRateLimiter,
/// IRouteRateLimiter) whose types are not accessible at compile time.
/// </summary>
internal class NoOpProxy : DispatchProxy
{
    internal Func<string, bool?>?   GetBoolResult   { get; set; }
    internal Func<string, object?>? GetObjectResult { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null) return null;

        var name = targetMethod.Name;

        // Property getter returning bool
        if (GetBoolResult is not null && name.StartsWith("get_"))
        {
            var result = GetBoolResult(name["get_".Length..]);
            if (result.HasValue) return result.Value;
        }

        var returnType = targetMethod.ReturnType;

        // ValueTask<T> — wrap the inner result
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var innerType = returnType.GetGenericArguments()[0];
            var innerValue = GetObjectResult?.Invoke(name);

            // ValueTask<T>.FromResult(value)
            return typeof(ValueTask).GetMethod(nameof(ValueTask.FromResult))!
                .MakeGenericMethod(innerType)
                .Invoke(null, [innerValue ?? (innerType.IsValueType
                    ? RuntimeHelpers.GetUninitializedObject(innerType)
                    : null)]);
        }

        // ValueTask (non-generic)
        if (returnType == typeof(ValueTask))
            return ValueTask.CompletedTask;

        // Task<T>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            return Task.FromResult<object?>(null);

        // Task
        if (returnType == typeof(Task))
            return Task.CompletedTask;

        return null;
    }
}
