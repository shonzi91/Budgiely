# syntax=docker/dockerfile:1

# ---- build ----------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# The Blazor WASM publish relinks the runtime with Emscripten (emcc), which needs python.
# The SDK image doesn't ship it, so the publish fails with "unable to find python in $PATH".
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python-is-python3 \
    && rm -rf /var/lib/apt/lists/*

# Publishing a Blazor WASM app needs the wasm-tools workload (IL trimming + native relink).
RUN dotnet workload install wasm-tools

# NuGet.config pins restore to nuget.org (the corporate feed is unreachable / slow).
COPY NuGet.config ./
COPY src/ ./src/

RUN dotnet restore src/FinApp.Server/FinApp.Server.csproj

# The server's ProjectReference to FinApp.App.Web bundles the published WASM client into wwwroot.
RUN dotnet publish src/FinApp.Server/FinApp.Server.csproj -c Release -o /app/publish --no-restore

# ---- runtime --------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# SQLite lives on a mounted volume so account data survives restarts/redeploys.
ENV ConnectionStrings__FinApp="Data Source=/data/finapp-server.db"
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"
# NOTE: set a real Jwt__Key (>= 32 chars) at runtime — the server refuses to start otherwise.

RUN mkdir -p /data
VOLUME ["/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "FinApp.Server.dll"]
