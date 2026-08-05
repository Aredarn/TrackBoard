# TrackBoard

A motorsport results and championship-standings API. Drivers register vehicles, file
results against race events, and the points system produces series leaderboards from a
configurable scoring scheme.

Built with .NET 10 / ASP.NET Core, EF Core 10, and PostgreSQL. See [ROADMAP.md](ROADMAP.md)
for the phased plan and what is still outstanding.

> **Status:** phases 1–5 are implemented — core API, JWT authentication, authorisation,
> leaderboard caching, tests and CI. Open-source packaging (phase 6) is not done yet.

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

The API documentation UI is at `/scalar/v1` in Development and the OpenAPI document at
`/openapi/v1.json`.

`/health` is a liveness probe with no dependencies. `/health/ready` reports whether the
database and cache are actually reachable, with per-check detail in Development only.

### Configuration

| Key | Source |
|---|---|
| `ConnectionStrings:Default` | user-secrets locally; `ConnectionStrings__Default` environment variable when deployed |
| `ConnectionStrings:Redis` | optional. Present → Redis is the shared cache layer; absent → in-process only |
| `Cache:*` | TTLs and warm-up behaviour, in `appsettings.json` |
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

## Caching

Leaderboards are cached with `HybridCache` — in-process L1, plus Redis as a shared L2 when
`ConnectionStrings:Redis` is set. Entries live 300 seconds shared / 30 seconds local and are
tagged per series, so any write that can move the standings evicts them immediately: result
submit, update, delete, and race-event update or delete. Nothing is cached without an expiry.

The current and previous season are warmed at startup by a background service, so the first
request after a deploy does not pay for the aggregation. Warm-up never blocks startup.

Cache behaviour is instrumented on the `TrackBoard.Cache` meter — `trackboard.cache.requests`
and `trackboard.leaderboard.duration`, both tagged `outcome=hit|miss`. Note that nothing
currently exports these; wiring up OpenTelemetry is still outstanding.

`/api/auth/register`, `/login`, and `/refresh` are rate limited to 5 requests per minute per
client; everything else to 300.

## Tests

```bash
dotnet test
```

65 tests: unit tests over the points calculator, and integration tests that boot the whole
application through `WebApplicationFactory` — authentication, authorisation, JWT rejection,
the result-submission flow, leaderboard cache invalidation, and rate limiting.

With coverage:

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

Line coverage is 76.7% and CI fails below 70%. Migrations and generated code are excluded.

Integration tests run against SQLite rather than PostgreSQL, so provider-specific SQL
translation is not covered — see the note on `TrackBoardApiFactory`.

The build treats warnings as errors and audits packages for known CVEs, so `dotnet build`
failing on an analyzer finding is expected rather than a misconfiguration. Formatting is
enforced with `dotnet format --verify-no-changes`.

## Licence

Apache-2.0 — see [LICENSE.txt](LICENSE.txt).
