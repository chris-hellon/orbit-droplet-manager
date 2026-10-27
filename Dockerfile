# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) — needs the central build/package files + all project files.
COPY global.json Directory.Build.props Directory.Packages.props NuGet.config ./
COPY src/Orbit/Orbit.csproj src/Orbit/
COPY src/Orbit.Client/Orbit.Client.csproj src/Orbit.Client/
COPY src/Orbit.Core/Orbit.Core.csproj src/Orbit.Core/
COPY src/Orbit.Shared/Orbit.Shared.csproj src/Orbit.Shared/
RUN dotnet restore src/Orbit/Orbit.csproj

COPY . .
RUN dotnet publish src/Orbit/Orbit.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

# Persist the SQLite database and Data-Protection key ring across restarts.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Orbit__DataDirectory=/data
VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "Orbit.dll"]
