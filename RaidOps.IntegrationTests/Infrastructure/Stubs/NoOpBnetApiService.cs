using RaidOps.ExternalApplication.Contracts.Services.BNet;
using RaidOps.ExternalApplication.Contracts.Services.BNet.Responses;

namespace RaidOps.IntegrationTests.Infrastructure.Stubs;

/// <summary>
/// Stub implementation of <see cref="IBnetApiService"/> for integration tests.
/// Returns one fake WoW character (Human Mage on Argent Dawn — all seeded in the DB)
/// so Sync and Activate flows exercise real handler code without a live BNet connection.
/// </summary>
internal class NoOpBnetApiService : IBnetApiService
{
    public string BuildAuthorizationUrl(string region, string redirectUri, string state)
        => $"https://oauth.battle.net/authorize?region={region}&redirect_uri={redirectUri}&state={state}";

    public Task<BnetTokenResponse> ExchangeCodeAsync(
        string code, string redirectUri, string region,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetTokenResponse
        {
            AccessToken = "stub-bnet-access-token",
            TokenType = "Bearer",
            ExpiresIn = 86400,
        });

    public Task<BnetUserInfoResponse> GetUserInfoAsync(
        string accessToken, string region,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetUserInfoResponse
        {
            BattleTag = "StubUser#9999",
            Id = 123456789,
        });

    public Task<BnetWowAccountsResponse> GetWowCharactersAsync(
        string accessToken, string region, string profileNamespace,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetWowAccountsResponse
        {
            WowAccounts =
            [
                new BnetWowAccountDto
                {
                    Id = 1,
                    Characters =
                    [
                        new BnetWowCharacterDto
                        {
                            Id    = 88001,
                            Name  = "Stubmage",
                            Level = 80,
                            Realm         = new BnetRealmRefDto { Slug = "argent-dawn", Name = "Argent Dawn" },
                            PlayableClass = new BnetIdRefDto   { Id = 8, Name = "Mage"    },
                            PlayableRace  = new BnetIdRefDto   { Id = 1, Name = "Human"   },
                            Gender        = new BnetTypeRefDto { Type = "MALE"             },
                            Faction       = new BnetTypeRefDto { Type = "ALLIANCE"         },
                        },
                        new BnetWowCharacterDto
                        {
                            Id    = 88002,
                            Name  = "Stublady",
                            Level = 70,
                            Realm         = new BnetRealmRefDto { Slug = "argent-dawn", Name = "Argent Dawn" },
                            PlayableClass = new BnetIdRefDto   { Id = 1, Name = "Warrior"  },
                            PlayableRace  = new BnetIdRefDto   { Id = 2, Name = "Orc"      },
                            Gender        = new BnetTypeRefDto { Type = "FEMALE"            },
                            Faction       = new BnetTypeRefDto { Type = "HORDE"             },
                        },
                        new BnetWowCharacterDto
                        {
                            Id    = 88003,
                            Name  = "Stubneutral",
                            Level = 10,
                            Realm         = new BnetRealmRefDto { Slug = "argent-dawn", Name = "Argent Dawn" },
                            PlayableClass = new BnetIdRefDto   { Id = 8, Name = "Mage"     },
                            PlayableRace  = new BnetIdRefDto   { Id = 1, Name = "Human"    },
                            Gender        = new BnetTypeRefDto { Type = "MALE"              },
                            Faction       = new BnetTypeRefDto { Type = "NEUTRAL"           },
                        },
                    ]
                }
            ]
        });

    public Task<string> GetAppTokenAsync(string region, CancellationToken cancellationToken = default)
        => Task.FromResult("stub-app-token");

    public Task<BnetCharacterDetailResponse> GetCharacterAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetCharacterDetailResponse
        {
            Level = 80,
            EquippedItemLevel = 600,
            AverageItemLevel = 610,
            Guild = null,
        });

    public Task<BnetCharacterMediaResponse> GetCharacterMediaAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetCharacterMediaResponse
        {
            Assets = [new BnetMediaAssetDto { Key = "avatar", Value = "https://example.com/avatar.jpg" }]
        });

    public Task<BnetCharacterSpecializationsResponse> GetCharacterSpecializationsAsync(
        string accessToken, string region, string profileNamespace,
        string realmSlug, string characterName,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new BnetCharacterSpecializationsResponse
        {
            ActiveSpecialization = new BnetIdRefDto { Id = 62, Name = "Arcane" },
        });
}
