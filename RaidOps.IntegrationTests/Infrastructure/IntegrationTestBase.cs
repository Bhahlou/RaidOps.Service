using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RaidOps.Infrastructure.Persistence.Implementations;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests.
/// Provides an unauthenticated HTTP client and helpers for auth, DB access, and data seeding.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly RaidOpsWebApplicationFactory Factory;

    /// <summary>Unauthenticated HTTP client (follows redirects).</summary>
    protected readonly HttpClient Client;

    /// <summary>
    /// JSON options matching the API configuration (enums as strings).
    /// Use for ReadFromJsonAsync calls that involve enum-typed response fields.
    /// </summary>
    protected static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    protected IntegrationTestBase(RaidOpsWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Returns an HTTP client with a valid JWT Bearer token for the given Discord user.
    /// </summary>
    protected HttpClient CreateAuthenticatedClient(
        string discordId = "123456789012345678",
        string username = "TestUser")
    {
        var token = TestTokenBuilder.CreateAccessToken(discordId, username);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Returns an HTTP client that does NOT follow redirects.
    /// Use when you need to assert 302 status codes or redirect locations.
    /// </summary>
    protected HttpClient CreateNonRedirectingClient()
        => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Returns an authenticated HTTP client that does NOT follow redirects.
    /// </summary>
    protected HttpClient CreateAuthenticatedNonRedirectingClient(
        string discordId = "123456789012345678",
        string username = "TestUser")
    {
        var token = TestTokenBuilder.CreateAccessToken(discordId, username);
        var client = CreateNonRedirectingClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Inserts test data directly into the database, outside of the request pipeline.
    /// </summary>
    protected async Task SeedAsync(Func<RaidOpsDbContext, Task> seed)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();
        await seed(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Opens a scoped DbContext for direct database assertions.
    /// The caller is responsible for disposing the returned scope.
    /// </summary>
    protected (IServiceScope Scope, RaidOpsDbContext DbContext) CreateDbScope()
    {
        var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();
        return (scope, db);
    }
}
