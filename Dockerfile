# ── Étape 1 : Build .NET ─────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

WORKDIR /src

# Copier les csproj et restaurer les dépendances (layer cacheable)
COPY RaidOps.API/RaidOps.API.csproj                                                               RaidOps.API/
COPY RaidOps.Application.Contracts/RaidOps.Application.Contracts.csproj                          RaidOps.Application.Contracts/
COPY RaidOps.Application.Implementations/RaidOps.Application.Implementations.csproj              RaidOps.Application.Implementations/
COPY RaidOps.Domain/RaidOps.Domain.csproj                                                         RaidOps.Domain/
COPY RaidOps.ExternalApplication.Contracts/RaidOps.ExternalApplication.Contracts.csproj          RaidOps.ExternalApplication.Contracts/
COPY RaidOps.ExternalApplication.Implementations/RaidOps.ExternalApplication.Implementations.csproj RaidOps.ExternalApplication.Implementations/
COPY RaidOps.Infrastructure.Persistence.Contracts/RaidOps.Infrastructure.Persistence.Contracts.csproj RaidOps.Infrastructure.Persistence.Contracts/
COPY RaidOps.Infrastructure.Persistence.Implementations/RaidOps.Infrastructure.Persistence.Implementations.csproj RaidOps.Infrastructure.Persistence.Implementations/
COPY RaidOps.Registry/RaidOps.Registry.csproj                                                     RaidOps.Registry/

RUN dotnet restore RaidOps.API/RaidOps.API.csproj

# Copier le reste et publier
COPY . .
RUN dotnet publish RaidOps.API/RaidOps.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Étape 2 : Image de production ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

WORKDIR /app

ARG APP_VERSION=dev
ENV APP_VERSION=$APP_VERSION

COPY --from=build /app/publish .

RUN addgroup -S raidops && adduser -S raidops -G raidops \
    && mkdir -p /app/logs && chown -R raidops:raidops /app/logs
USER raidops

EXPOSE 8080

ENTRYPOINT ["dotnet", "RaidOps.API.dll"]
