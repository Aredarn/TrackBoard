# TrackBoard

The backend for the [TrackPro](https://github.com/Aredarn/trackpro) lap-timing app. Drivers
publish the tracks they build, upload their sessions, and compare best laps on public
per-track leaderboards. The API contract with the app is in
[docs/trackpro-api](docs/trackpro-api/README.md).

It also contains a championship model (series, race events, results and points). That part
is parked: it works and is tested, but the app does not use it.

Built with .NET 10 / ASP.NET Core, EF Core 10, and PostgreSQL. See [ROADMAP.md](ROADMAP.md)
for the phased plan and what is still outstanding.

> **Status:** the TrackPro v1 backend is implemented and tested against the contract, but
> has **not yet run against a real PostgreSQL database** — tests use SQLite. The app side of
> the integration has not been started.

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
curl -X POST http://localhost:5000/api/v1/auth/register -H "Content-Type: application/json" -d '{"email":"you@example.com","displayName":"You","password":"a-long-enough-password"}'
```

Access tokens last 60 minutes. Refresh tokens last 90 days, are stored only as a SHA-256
hash, and **rotate on every use** — presenting an already-rotated token is treated as
evidence of theft and revokes every session for that user.

Registration always creates a `Driver`. Promote an account to `Admin` directly in the
database; there is deliberately no endpoint that grants roles.

## API overview

Every route is under `/api/v1`. The full TrackPro contract is
[docs/trackpro-api/openapi.yaml](docs/trackpro-api/openapi.yaml).

**Used by TrackPro**

| Resource | Route | Access |
|---|---|---|
| Auth | `/api/v1/auth` | anonymous (`register`, `login`, `refresh`) |
| Vehicles | `/api/v1/vehicles` | owner only; `PUT` creates or replaces under an app-generated id |
| Tracks | `/api/v1/tracks` | **public** reads of published tracks; owner writes |
| Track leaderboard | `/api/v1/tracks/{id}/leaderboard` | **public**; a token adds your own `me` entry |
| Sessions | `/api/v1/sessions` | owner only; `PUT` uploads a whole session with its laps |

On the public endpoints signing in is optional, but a token that is sent and fails
validation gets a 401 rather than being treated as anonymous, so the app notices it needs
to refresh.

A lap reaches its track's leaderboard when its session is `Ranked` and not voided, the lap
had no GPS signal gap, and the track is published. **Lap times are not verified** — see the
contract's decision 5. Once a track has a ranked lap its geometry is frozen, because TrackPro
derives the timing gates from those points.

**Parked championship model**

| Resource | Route | Access |
|---|---|---|
| Results | `/api/v1/results` | any authenticated user reads; owner or admin writes |
| Series leaderboard | `/api/v1/series/{id}/leaderboard` | authenticated |
| Circuits, series, race events, points schemes | `/api/v1/...` | authenticated reads, **admin** writes |

All list endpoints are paginated with `?page=` and `?pageSize=` (capped at 100). Errors use
RFC 7807 `ProblemDetails`. Enums are serialised as strings.

Result points are always derived server-side from the series' points scheme and are ignored
if a client supplies them. A DNF scores zero, bonuses included.

## Caching

Both kinds of leaderboard are cached with `HybridCache` — in-process L1, plus Redis as a shared L2 when
`ConnectionStrings:Redis` is set. Entries live 300 seconds shared / 30 seconds local and are
tagged per track or series, so any write that can move the standings evicts them
immediately. For tracks that is a session upload or delete, a track's visibility changing,
or a vehicle being renamed; for series, result writes and race-event changes. Nothing is
cached without an expiry. A track's cached standings hold every driver, so the top of the
board, your own `me` entry and each lap's rank in a session all come from one entry.

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
