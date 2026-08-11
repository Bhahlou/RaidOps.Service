using Asp.Versioning;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NetCord.Hosting.Services.ApplicationCommands;
using RaidOps.API.Hubs;
using RaidOps.Application.Contracts.Configuration;
using RaidOps.Application.Contracts.Services;
using RaidOps.ExternalApplication.Contracts.Services.Discord;
using RaidOps.ExternalApplication.Implementations.Bot.Commands;
using RaidOps.Infrastructure.Persistence.Implementations;
using RaidOps.Registry;
using Serilog;
using Serilog.Enrichers.ShortTypeName;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace RaidOps.API
{
    [ExcludeFromCodeCoverage]
    public static class Program
    {
        private static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                await RunAsync(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "RaidOps.API terminated unexpectedly");
                Environment.ExitCode = 1;
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        private static async Task RunAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithShortTypeName());

            var frontendUrl = builder.Configuration["FrontendUrl"] ?? string.Empty;

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy
                        .WithOrigins(
                            "http://localhost:4200",
                            "https://localhost:4200",
                            "https://localhost:7174",
                            frontendUrl)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services
                .AddApiVersioning(options =>
                {
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.ReportApiVersions = true;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            builder.Services.AddControllers()
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;

            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(options =>
                {
                    options.Cookie.Name = ".RaidOps.Auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.None;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                })
                .AddDiscord(options =>
                {
                    options.ClientId = builder.Configuration["Discord:ClientId"]!;
                    options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
                    options.SaveTokens = true;
                    options.Scope.Add("identify");
                    options.Scope.Add("guilds");
                    options.Events = new OAuthEvents
                    {
                        // Discord redirects here with ?error=access_denied when the user cancels
                        // the consent screen — without this, the OAuth middleware throws and the
                        // request ends on the ASP.NET dev exception page instead of back on the
                        // frontend. returnTo was stashed in the state by DiscordAuthController.Signup,
                        // so the user lands back wherever they started the login from (home or
                        // get-started) instead of a fixed page.
                        OnRemoteFailure = context =>
                        {
                            context.HandleResponse();
                            var returnTo = context.Properties?.Items.TryGetValue("returnTo", out var value) == true
                                ? value
                                : null;
                            context.Response.Redirect($"{frontendUrl}/{returnTo ?? "home"}?error=access_denied");
                            return Task.CompletedTask;
                        }
                    };
                })
                .AddJwtBearer(options =>
                {
                    // Preserve short JWT claim names ("sub", "name", etc.) instead of mapping
                    // them to long XML URI types (ClaimTypes.NameIdentifier etc.).
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            context.Token = context.Request.Cookies["access_token"];
                            return Task.CompletedTask;
                        },
                        // All RaidOps-issued JWTs (access, refresh, OAuth state tokens) share the
                        // same signing key/issuer/audience, so without this check a leaked refresh
                        // or state token could be replayed as an access token via the cookie above.
                        OnTokenValidated = context =>
                        {
                            var type = context.Principal?.FindFirst("typ")?.Value;
                            if (type != "access")
                            {
                                context.Fail("Token is not an access token.");
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddSignalR();
            builder.Services.AddSingleton<IUserIdProvider, JwtSubUserIdProvider>();
            builder.Services.AddSingleton<IAuthNotifier, AuthNotifier>();

            builder.Services.AddRaidOps(builder.Configuration);

            var app = builder.Build();

            app.AddApplicationCommandModule<RaidCommandModule>();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            });

            app.UseSerilogRequestLogging();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RaidOpsDbContext>();
                await db.Database.MigrateAsync();
            }

            var deployNotifier = app.Services.GetRequiredService<IDiscordDeployNotifier>();
            await deployNotifier.NotifyDeployedAsync();

            var apiVersions = app.DescribeApiVersions();

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var groupName in apiVersions.Select(d => d.GroupName))
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{groupName}/swagger.json",
                        $"RaidOps API {groupName}");
                }
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseCors("AllowFrontend");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapHub<AuthHub>("/hubs/auth");

            await app.RunAsync();
        }
    }
}