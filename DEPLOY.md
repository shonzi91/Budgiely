# Deploying FinApp

FinApp deploys as a **single one-origin container**: `FinApp.Server` hosts the REST API, the SignalR
hub, **and** the Blazor WASM web UI on one origin — so there's no CORS to configure in production.
(CORS is only wired up for local two-terminal dev.)

## What's in the image
- Multi-stage [`Dockerfile`](Dockerfile): the SDK stage installs the `wasm-tools` workload and runs
  `dotnet publish` on `FinApp.Server`. Because the server references `FinApp.App.Web`, the published
  WASM client (its `_framework` + assets) is bundled into the server's `wwwroot` automatically.
- The runtime stage serves everything from `http://+:8080`.

## Required runtime configuration
| Setting | How to set | Notes |
| --- | --- | --- |
| **JWT signing key** | `Jwt__Key` env var | **Required.** Must be ≥ 32 chars. The server *refuses to start* in Production with the dev placeholder. Generate e.g. `openssl rand -base64 48`. |
| **Database path** | `ConnectionStrings__FinApp` | Defaults to `Data Source=/data/finapp-server.db` (the mounted volume). |
| **Bind URL/port** | `ASPNETCORE_URLS` | Defaults to `http://+:8080`. Put TLS termination at your load balancer / reverse proxy. |
| JWT issuer/audience/expiry | `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryHours` | Optional; sensible defaults in `appsettings.json`. |

## Database & persistence
SQLite file at `/data/finapp-server.db`. **Mount a persistent volume at `/data`** or the data is lost on
redeploy. EF migrations apply automatically on startup (`AccountStore`/`Database.Migrate()`).
Back up by snapshotting the volume or copying the `.db` file. For multi-instance/scale later, migrate the
EF provider to Postgres/SQL Server (the model is mostly portable; `MoneyConverter` stores text).

## Build & run locally with Docker
```bash
docker build -t finapp .
docker run -d --name finapp -p 8080:8080 \
  -e Jwt__Key="$(openssl rand -base64 48)" \
  -v finapp-data:/data \
  finapp
# open http://localhost:8080
```

## Platform notes
- **Fly.io:** `fly launch` (no buildpacks — it'll use the Dockerfile), add a volume (`fly volumes create finapp_data`)
  mounted at `/data`, set `fly secrets set Jwt__Key=...`. Fly provides TLS on the edge.
- **Render:** new Web Service from the repo (Docker env), add a Disk mounted at `/data`, set `Jwt__Key`
  as an env var. TLS is automatic.
- **Azure Container Apps / App Service (container):** push the image to a registry, set `Jwt__Key` as a
  secret, attach Azure Files (or a managed disk) at `/data`. Enable HTTPS-only.
- **VPS:** `docker run` as above behind nginx/Caddy for TLS.

## Local development (unchanged, two origins)
```powershell
dotnet run --project src\FinApp.Server\FinApp.Server.csproj    # :5179 (Development → CORS for :5080)
dotnet run --project src\FinApp.App.Web\FinApp.App.Web.csproj  # :5080 → talks to :5179
```
In Development the web client reads `ApiBaseUrl` from `wwwroot/appsettings.Development.json`
(`http://localhost:5179`); in Production it's empty, so the client uses its own origin.
