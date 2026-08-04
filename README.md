# TrackBoard

A motorsport results and championship-standings API. Drivers register vehicles, file
results against race events, and the points system produces series leaderboards from a
configurable scoring scheme.

Built with .NET 10 / ASP.NET Core, EF Core 10, and PostgreSQL. See [ROADMAP.md](ROADMAP.md)
for the phased plan and what is still outstanding.

> **Status:** phases 1–3 are implemented — core API, JWT authentication, and authorisation.
> Caching, tests, and CI (phases 4–6) are not built yet.

## Getting started

Requires the .NET 10 SDK and a PostgreSQL instance.

### 1. Configure secrets

Nothing sensitive is committed, so a fresh clone needs two local values. Generate a signing
key of at least 32 bytes — the app refuses to start on anything weaker:

```bash
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 48)" --project TrackBoard
```

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=trackboard;Username=trackboard;Password=trackboard" --project TrackBoard
```

### 2. Start PostgreSQL and apply the schema

```bash
docker compose up -d db
```

```bash
dotnet ef database update --project TrackBoard
```

### 3. Run

```bash
dotnet run --project TrackBoard
```

The API documentation UI is at `/scalar/v1` in Development, the OpenAPI document at
`/openapi/v1.json`, and an anonymous liveness probe at `/health`.

### Configuration

| Key | Source |
|---|---|
| `ConnectionStrings:Default` | user-secrets locally; `ConnectionStrings__Default` environment variable when deployed |
| `Jwt:Secret` | user-secrets locally; `Jwt__Secret` environment variable when deployed. Minimum 256 bits |
| `Jwt:Issuer`, `Jwt:Audience`, token lifetimes | `appsettings.json` — not secret |
| `Cors:AllowedOrigins` | array of permitted origins. Empty means no cross-origin access is granted |

For Docker Compose, copy [.env.example](.env.example) to `.env` and set `JWT_SECRET`.

## Authentication

Register or log in, then send the access token as `Authorization: Bearer <accessToken>`.

```bash
curl -X POST http://localhost:5000/api/auth/register -H "Content-Type: application/json" -d '{"email":"you@example.com","displayName":"You","password":"a-long-enough-password"}'
```

Access tokens last 60 minutes. Refresh tokens last 14 days, are stored only as a SHA-256
hash, and **rotate on every use** — presenting an already-rotated token is treated as
evidence of theft and revokes every session for that user.

Registration always creates a `Driver`. Promote an account to `Admin` directly in the
database; there is deliberately no endpoint that grants roles.

## API overview

| Resource | Route | Access |
|---|---|---|
| Auth | `/api/auth` | anonymous (`register`, `login`, `refresh`) |
| Vehicles | `/api/vehicles` | owner only; admins see all |
| Results | `/api/results` | any authenticated user reads; owner or admin writes |
| Leaderboard | `/api/series/{id}/leaderboard` | authenticated |
| Circuits | `/api/circuits` | authenticated reads, **admin** writes |
| Series | `/api/series` | authenticated reads, **admin** writes |
| Race events | `/api/race-events` | authenticated reads, **admin** writes |
| Points schemes | `/api/points-schemes` | authenticated reads, **admin** writes |

All list endpoints are paginated with `?page=` and `?pageSize=` (capped at 100). Errors use
RFC 7807 `ProblemDetails`. Enums are serialised as strings.

Result points are always derived server-side from the series' points scheme and are ignored
if a client supplies them. A DNF scores zero, bonuses included.

`/api/auth/register`, `/login`, and `/refresh` are rate limited to 5 requests per minute per
client; everything else to 300.

## Licence

Apache-2.0 — see [LICENSE.txt](LICENSE.txt).
