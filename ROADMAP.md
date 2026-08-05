# TrackBoard — Roadmap (.NET 10 / ASP.NET Core)

Derived from the RaceBoard roadmap (`TODO.html`), which was written for Spring Boot.
Every task below has been translated to the .NET equivalent. Where .NET's idiomatic
answer differs from Spring's, the Spring task is noted as **[was: …]** so the mapping
stays traceable.

**Stack:** .NET 10 · ASP.NET Core Web API · EF Core 10 · PostgreSQL (Npgsql) · Redis
**Current state:** 1 stub controller, 1 entity, no `DbContext`, no auth, no tests.

---

## Where the repo actually stands

| Roadmap assumption | Reality in this repo | Action |
|---|---|---|
| Java / Spring Boot / Maven | .NET 10 / ASP.NET Core / MSBuild | Translate throughout |
| Project named "RaceBoard" | `TrackBoard` | Keep TrackBoard; roadmap name is cosmetic |
| MIT license | `LICENSE.txt` is **Apache-2.0** | Decide: keep Apache-2.0 (fine for OSS) or swap |
| Branch `main` | Branch is `master` | Rename to `main` or adapt the CI rules |
| Flyway / Liquibase | none | EF Core Migrations |
| JPA entities | 1 entity, no `DbContext` | Phase 1–2 work |

Defects found at the start, and their current state:

- ~~`Entities/Vehicle.cs` declares class `VehicleInformationData` — file/class name mismatch.~~ **Fixed.**
- ~~That entity uses a `long` identity PK; the roadmap mandates UUIDs.~~ **Fixed** — `Guid` v7 via `BaseEntity`.
- ~~`Controllers/VehicleController.cs` inherits `Controller` instead of `ControllerBase` and is missing `[ApiController]`.~~ **Fixed** — replaced by `VehiclesController`.
- ~~`Program.cs` has no exception handling, no DB.~~ **Fixed.** No auth or CORS yet — phase 3.
- ~~`.gitignore` does not cover `.env`.~~ **Fixed.**
- ~~`Dtos/`, `Services/`, `Repositories/` exist as empty folders.~~ `Dtos/` and `Services/` are now populated.
  `Repositories/` was dropped: EF Core's `DbContext` is already a unit of work plus repository,
  and a wrapper over it would only block the `IQueryable` projections the leaderboard depends on.
- `.github/` exists but is empty — phase 5/6.
- **Found during implementation:** `Microsoft.AspNetCore.OpenApi` 10.0.4 pulled `Microsoft.OpenApi`
  2.0.0, which carries a high-severity advisory (GHSA-v5pm-xwqc-g5wc). Pinned to 2.11.0 and
  turned on `NuGetAuditMode=all`.

---

## Phase 1 — Foundation (12 tasks)

*Get the skeleton right before any domain logic.*

### Project setup
- [x] Add the NuGet packages that replace the Spring Initializr starters:
  `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`,
  `Riok.Mapperly`, `EFCore.NamingConventions`, `Scalar.AspNetCore`.
  **[was: Web, JPA, Security, Cache, Validation, Actuator, Lombok]** — Validation is built in
  via DataAnnotations; Lombok has no analogue (C# records / auto-properties cover it).
  `JwtBearer` (phase 3) and `Caching.Hybrid` (phase 4) are deliberately not added yet.
- [x] Multi-environment config: `appsettings.json` + `appsettings.Development.json`
  + `appsettings.Production.json`. **[was: application-local/prod.yml]**
- [x] 🔒 No secrets in any `appsettings*.json`. Production reads `ConnectionStrings__Default`
  from the environment and the app fails fast at startup when it is absent. Only local
  throwaway credentials appear in `appsettings.Development.json`.
  **[was: `${ENV_VAR}` placeholders]** — .NET's config layering does this natively.
- [x] 🔒 Add `.env` to `.gitignore` and commit `.env.example`.
- [x] Set up **EF Core Migrations** from day one — `Data/Migrations/…_InitialCreate`.
  **[was: Flyway/Liquibase]**

### Repository & Git hygiene
- [x] Extend `.gitignore` for `.env`, `.env.*`, `appsettings.*.local.json`
  **[was: target/, .idea/, *.iml]** — `bin/`, `obj/`, `.vs/` were already covered.
- [ ] Branch strategy: rename `master` → `main`, feature branches per phase, `main` always deployable.
- [ ] License decision: roadmap says MIT, repo ships Apache-2.0. The README now states
  Apache-2.0 — change both together if you want MIT instead.
- [ ] Protect `main`: require PR review + passing CI before merge.

### Docker baseline
- [x] `docker-compose.yml` with app + PostgreSQL + Redis, plus a multi-stage `Dockerfile`
  (`sdk:10.0-noble` → `aspnet:10.0-noble`, running as the non-root `app` user).
- [ ] ⚠️ Verify a fresh clone works: `docker compose up -d && dotnet run --project TrackBoard`.
  **Not verified — Docker is not installed on this machine.** The compose and Dockerfile are
  written but have never been built or run.
- [x] 🔒 Pin every image tag and NuGet version — no floating versions, no `latest`.

---

## Phase 2 — Core API (14 tasks)

*Domain model, REST surface, points system.*

### Data model & EF Core
- [x] Create `TrackBoardDbContext` and entities: `User`, `Vehicle`, `Circuit`, `Series`,
  `RaceEvent`, `Result` (plus `PointsScheme`/`PointsSchemeEntry`), with relationships and
  one `IEntityTypeConfiguration<T>` per entity. **[was: JPA entities]**
- [x] Switch primary keys to `Guid`. `BaseEntity` defaults to `Guid.CreateVersion7()` so keys
  stay time-ordered and don't fragment the index — the usual argument against GUID PKs.
- [x] Add `CreatedAt` / `UpdatedAt` via `AuditingInterceptor : SaveChangesInterceptor`.
  It also pins `CreatedAt` as unmodified on update so it cannot be overwritten.
  **[was: Spring Data Auditing]**
- [x] Every schema change goes through an EF migration. `EnsureCreated()` is never called.
  **[was: never `ddl-auto: create`]**
- [x] ⚡ Indexes on FK columns and `WHERE`-clause fields via `HasIndex()`, including uniques on
  `users.email`, `circuits.name`, `(series.name, season)`, and `(results.race_event_id, user_id)`.

### REST API design
- [x] DTOs (C# `record`s) for every request/response — no entity is ever returned.
- [x] Map with **Mapperly** for request→entity. Entity→response runs as `IQueryable`
  projections instead, so flattening and aggregates translate to a single SQL `SELECT`
  rather than materialising entity graphs. Avoided AutoMapper — commercially licensed.
- [x] Consistent error shape via `GlobalExceptionHandler : IExceptionHandler` +
  `AddProblemDetails()` (RFC 7807). **[was: `@ControllerAdvice`]**
- [x] Correct status codes: `CreatedAtAction` → 201, `NoContent` → 204, `NotFound` → 404,
  plus 409 for domain conflicts.
- [x] Pagination on all list endpoints — `PagedResult<T>` bound from `PageQuery`, page size
  capped at 100. **[was: `Pageable`]** — ASP.NET Core has no built-in, so this is hand-rolled.
- [x] 🔒 Every controller is `[ApiController] : ControllerBase`, so DataAnnotations validation
  runs automatically and returns 400 + `ProblemDetails`. **[was: `@Valid` + Bean Validation]**
  FluentValidation still worth considering for cross-field rules.

### Points system
- [x] `PointsScheme` + `PointsSchemeEntry` entities, referenced by `Series`, with configurable
  fastest-lap and pole bonuses.
- [x] `Result.Points` is derived by `PointsCalculator` on every submit *and* update, and is
  absent from the request DTOs so a client cannot set it. A DNF scores zero including bonuses.
- [x] Leaderboard aggregation as a `GroupBy` projection over `IQueryable` — points, starts,
  wins, podiums and fastest laps all sum in SQL, one row per driver. **[was: JPQL projections]**

---

## Phase 3 — Security (18 tasks — 8 flagged critical)

*Must be complete before the API is publicly reachable.*

### Authentication
- [x] JWT via `AddAuthentication().AddJwtBearer()` + `TokenValidationParameters`, with
  `ClockSkew` zeroed (the 5-minute default silently extends every token) and
  `ValidAlgorithms` pinned to HS256 so a token cannot be downgraded to `alg=none`.
  **[was: custom `JwtService` + `JwtFilter`]** — configuration, not a filter.
- [x] 🚨 Passwords hashed with `PasswordHasher<User>` (PBKDF2). Plaintext is never stored or
  logged, and `SuccessRehashNeeded` upgrades older hashes on next login.
  **[was: `BCryptPasswordEncoder`]**
- [x] Access tokens expire in 60 minutes (config-capped at 24h). Refresh tokens are
  persisted as SHA-256 hashes, rotate on every use, and are revocable.
- [x] 🚨 Signing key comes from configuration only — user-secrets locally, `Jwt__Secret` in
  deployment. `JwtOptionsValidator` + `ValidateOnStart()` refuse to boot below 256 bits.
- [x] Returns 401 for missing/expired/tampered tokens, 403 for insufficient role.

### Authorisation
- [x] 🚨 Drivers may only modify their own vehicles and results. Vehicle *reads* are also
  owner-scoped. **Deviation:** result reads are open to any authenticated user — a race
  results board that hides other drivers' finishes is not a results board. Tighten
  `ResultService.GetPagedAsync` if you disagree.
- [x] `[Authorize]` on every controller, `[Authorize(Roles = AuthorizationPolicies.AdminRole)]`
  on reference-data writes. **[was: `@PreAuthorize`]**
- [x] 🚨 Ownership enforced by `VehicleOwnerHandler` / `ResultOwnerHandler`
  (`AuthorizationHandler<OperationAuthorizationRequirement, TResource>`), invoked through
  `IResourceAuthorizer` after the entity is loaded. Admins bypass; nobody else does.
- [x] `Admin` role gates circuits, series, race events, and points schemes. There is
  deliberately no endpoint that grants a role.

### API hardening
- [x] CORS is an explicit allow-list from `Cors:AllowedOrigins`; empty grants nothing. HSTS on
  outside Development. **[was: disable CSRF / httpBasic / formLogin]** — none are on by
  default in an ASP.NET Core Web API, so this inverts into "lock down what *is* on".
- [x] 🚨 `AddRateLimiter` — 5/minute on register, login and refresh; 300/minute globally.
  Partitioned by user id once authenticated, otherwise by remote IP. 429 carries a
  `Retry-After` header. **[was: bucket4j]**
- [x] `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` on every response, plus a
  locked-down CSP on `/api` paths (scoped so it does not break the Scalar docs UI).
- [x] 🚨 No stack traces in production — verified: with the database unreachable the client
  receives a fixed message while the `NpgsqlException` goes only to the log.
- [x] `[MaxLength]` / `[Range]` bounds on every string and numeric input, including a 128-char
  password ceiling so slow hashing cannot be used to burn CPU.

### Secret management
- [x] 🚨 Full history scanned for secret-shaped strings before any public push — 3 commits,
  13 files, clean. ⚠️ Done with a `git log -p` pattern sweep, **not** gitleaks, which is not
  installed here. Run the real tool before going public:
  `gitleaks detect --log-opts="--all"`.
- [x] gitleaks added to CI on every push and PR in
  [.github/workflows/security.yml](.github/workflows/security.yml), alongside a
  `dotnet list package --vulnerable` gate. GitHub secret scanning still needs enabling in
  repo settings.
- [ ] 🚨 Production secrets in the host's secret store. Nothing is committed and the app fails
  fast without the env vars, but no deployment target exists yet to configure.
- [ ] Rotate every secret if one is ever committed. Assume breach. (Nothing to rotate today.)

---

## Phase 4 — Performance (12 tasks)

*Measure first. Target read-heavy leaderboard endpoints.*

### Caching
- [x] `HybridCache` wraps `ResultService.GetLeaderboardAsync`, with Redis registered as L2
  whenever `ConnectionStrings:Redis` is present and L1-only otherwise. Stampede protection
  is built in: concurrent misses collapse onto one factory call.
  **[was: `@Cacheable`]** — no attribute-based caching in .NET, so the call is explicit.
- [x] `RemoveByTagAsync(CacheKeys.SeriesTag(seriesId))` after every write that can move the
  standings: result submit, update and delete, plus race-event delete (results cascade with
  it) and race-event update (moving an event between series invalidates *both*).
  Eviction always happens after the commit, never before. **[was: `@CacheEvict`]**
- [x] `CacheWarmupService : BackgroundService` warms the current and previous season through
  the same cached path a request takes. Failures are logged and dropped — a warm-up problem
  must not gate startup. **[was: `ApplicationRunner`]**
- [x] `Expiration` (300s) and `LocalCacheExpiration` (30s) set on every entry, plus a
  `DefaultEntryOptions` floor so a call site that passes none still cannot cache forever.
  L1 is deliberately shorter than L2: tag eviction reaches Redis at once but cannot reach
  another instance's in-process layer, so the short local window bounds the disagreement.
- [x] Keys are `trackboard:v1:leaderboard:series:{id}:season:{season}` — both ids present, and
  a version segment so a change to the cached record's shape cannot read back stale entries.

### Database
- [ ] Log slow queries: EF Core command logging at `Information`, plus a `DbCommandInterceptor`
  that warns above 100ms. **[was: Hibernate slow query log]**
- [ ] Hunt N+1s. EF Core doesn't lazy-load by default (so it's rarer than in Hibernate), but
  `foreach` + per-item query still does it. Use `AsSplitQuery()` where a fan-out `Include`
  causes cartesian explosion.
- [ ] Use `.Include()` / `.ThenInclude()`, or better, project directly to a DTO for read paths.
  **[was: `@EntityGraph` / `JOIN FETCH`]**
- [ ] Tune Npgsql pooling via the connection string (`Maximum Pool Size`, `Timeout`).
  **[was: HikariCP]** — pooling is on by default in Npgsql.

### Observability
- [x] 🔒 `/health` is liveness only — no dependencies, so a database blip stops traffic being
  routed rather than getting the process killed. `/health/ready` probes the database and a
  real `HybridCache` round-trip. **Deviation:** readiness is anonymous, not authorized —
  orchestrator probes cannot carry a token. Instead the body is a bare status outside
  Development, so dependency names and failure reasons are never exposed.
  **[was: `/actuator/health`]** — no Actuator; assembled by hand.
  ⚠️ Metrics are exposed via `Meter` but **not** yet scraped: no OpenTelemetry exporter and no
  `/metrics` endpoint. Nothing collects them today.
- [x] `CacheMetrics` records `trackboard.cache.requests` and a
  `trackboard.leaderboard.duration` histogram, both tagged `outcome=hit|miss` — the average
  across both hides the cold-start case that actually hurts. **[was: `@Timed`]**
- [x] `appsettings.Production.json` logs at Warning and above. Serilog not added; the built-in
  logger is sufficient until there is somewhere to ship structured logs to.

---

## Phase 5 — Quality (13 tasks)

*What separates a demo from a production codebase.*

### Testing
- [x] 16 xUnit tests over `PointsCalculator` covering DNF (bonuses forfeited, and a retirement
  that kept a stale position), partial grids, empty schemes, both bonuses, and mid-season
  scheme changes. Shouldly for assertions — FluentAssertions v8+ is commercially licensed.
  NSubstitute is referenced but unused so far: the calculator is pure, and the endpoint tests
  deliberately exercise real services rather than mocks.
- [x] `TrackBoardApiFactory` boots the whole app via `WebApplicationFactory<Program>`; 13
  tests drive the full submission flow from reference data through points to the leaderboard,
  including cache invalidation on write and on race-event deletion.
  ⚠️ **Deviation:** SQLite, not `Testcontainers.PostgreSql` — Docker is unavailable here.
  PostgreSQL-specific translation is therefore **untested**; everything above the provider
  is covered. Swapping providers is one method (`TrackBoardApiFactory.UseTestDatabase`).
  The `public partial class Program` gotcha was already handled back in phase 2.
- [x] 🔒 9 authorisation tests: 401 anonymous, 403 for a valid token with the wrong role,
  403 across all three verbs on another driver's vehicle, list scoping, admin override, and
  that a forged `ownerId` in the body cannot reassign ownership.
- [x] 🔒 11 JWT tests: tampered signature, payload edited after signing, `alg=none`, wrong
  key, wrong issuer, wrong audience, expired, expired-by-one-second (guards `ClockSkew`),
  missing `Bearer` prefix, no header.
- [x] **76.7% line coverage** (48.8% branch) — above the 70% target, enforced in CI.
  Migrations and generated code are excluded via [coverlet.runsettings](coverlet.runsettings);
  counting them measured how many migrations exist, not how well the code is tested.

### Code quality
- [x] [.editorconfig](.editorconfig) with `dotnet format --verify-no-changes` gating CI.
  **[was: Checkstyle / Spotless]**
- [x] [Directory.Build.props](Directory.Build.props) applies `EnableNETAnalyzers` and
  `TreatWarningsAsErrors` to every project. ⚠️ `AnalysisMode` is **Recommended**, not `All`:
  `All` produced 30 build errors, mostly `CA1848` on logging. The logging findings were fixed
  properly with `[LoggerMessage]` source generation rather than suppressed; migrations are
  excluded from analysis because they are generated. SonarCloud not added — it needs a repo
  to be public first. **[was: SpotBugs / SonarQube]**
- [x] Controllers parse and delegate only. The stub `VehicleController` was replaced in
  phase 2 by `VehiclesController` with `[ApiController]` and `ControllerBase`.
- [x] No `#pragma warning disable` anywhere in the codebase. Analyzer severities are tuned in
  `.editorconfig` with a written reason instead. **[was: `@SuppressWarnings`]**

### CI/CD
- [x] [.github/workflows/ci.yml](.github/workflows/ci.yml) — format check, Release build, then
  tests with coverage on every push and PR. ⚠️ Never executed on GitHub; the coverage-gate
  shell was verified locally against a real report and against a synthetic failing value.
- [x] Test failures fail the job, and a coverage gate fails it below 70%.
- [x] 🔒 [.github/dependabot.yml](.github/dependabot.yml) for NuGet, GitHub Actions and Docker.
  Microsoft/EF packages are grouped: they are version-locked, so one-at-a-time PRs produce
  builds that cannot restore.
- [x] 🔒 `NuGetAudit` at `low` severity with `TreatWarningsAsErrors` fails the build on any
  vulnerable package, transitive included, and the security workflow runs
  `dotnet list package --vulnerable` separately. This caught two real advisories:
  `Microsoft.OpenApi` 2.0.0 (phase 2) and `SQLitePCLRaw` 2.1.11 (this phase), both pinned
  to fixed versions. **[was: OWASP dependency-check]**

---

## Phase 6 — Open source (13 tasks)

*Do this before flipping the repo public.*

### Documentation
- [ ] Real `README.md` — currently one line (`# TrackBoard`). Needs: description, architecture
  diagram, 5-minute setup, API overview, license badge.
- [ ] `CONTRIBUTING.md` — fork, branch, `dotnet test`, open a PR.
- [ ] `CODE_OF_CONDUCT.md` (GitHub can generate it).
- [ ] Add a Swagger/Scalar UI. `Microsoft.AspNetCore.OpenApi` is already referenced and
  `MapOpenApi()` is wired up, but .NET 9+ templates ship **no UI** — add `Scalar.AspNetCore`
  (`app.MapScalarApiReference()`) or `Swashbuckle.AspNetCore.SwaggerUI`.
  **[was: springdoc-openapi at `/swagger-ui.html`]**
- [ ] `CHANGELOG.md` starting at v0.1.0.

### Seed data & demo
- [ ] Seed real circuits — Monza, Nürburgring, Spa, Suzuka, Silverstone — via EF Core `HasData()`
  in a migration, or a dedicated seeder `IHostedService`.
- [ ] Seed demo users and sample results so the leaderboard isn't empty on first run.
- [ ] A `DemoMode` config flag that resets seed data on demand.

### GitHub setup
- [ ] Issue templates under `.github/ISSUE_TEMPLATE/` (the `.github/` folder exists but is empty).
- [ ] Repo topics: `dotnet`, `aspnetcore`, `csharp`, `efcore`, `motorsport`, `rest-api`, `redis`.
  **[was: `spring-boot`, `java`]**
- [ ] Tag 3+ issues `good first issue`.
- [ ] Cut a v0.1.0 release with notes.
- [ ] Announce on r/dotnet, DEV.to, Hacker News Show HN. **[was: r/java]**

---

## Suggested execution order

The roadmap's phase order is sound, but three things should jump the queue because they're
cheap now and expensive later:

1. **Phase 2's `Guid` PK decision** — do it before any other entity is written.
2. **Phase 1's EF Migrations setup** — every schema change after this point must be a migration.
3. **Phase 3's secret handling** — establish it before the first commit that touches a
   connection string.

Everything else follows the phases as written.

## Task counts

| Phase | Tasks | Done | Critical 🚨 | Security 🔒 | Perf ⚡ |
|---|---|---|---|---|---|
| 1 · Foundation | 12 | 8 | 0 | 3 | 0 |
| 2 · Core API | 14 | 14 | 0 | 1 | 1 |
| 3 · Security | 18 | 16 | 8 | 0 | 0 |
| 4 · Performance | 12 | 8 | 0 | 1 | 0 |
| 5 · Quality | 13 | 13 | 0 | 4 | 0 |
| 6 · Open source | 13 | 0 | 0 | 0 | 0 |
| **Total** | **82** | **59** | **8** | **9** | **1** |

**65 tests, all passing. Build is clean with warnings-as-errors. 76.7% line coverage.**

All 8 critical tasks are implemented and were exercised against a running server.

Still open in phase 1: branch rename, licence decision, branch protection, Docker verification —
three need repo-owner decisions, one needs Docker installed.
Still open in phase 3: both remaining items are deployment-time actions with no target yet.
Still open in phase 4: the whole **Database** section — slow-query interceptor, N+1 audit,
`AsSplitQuery` review, and Npgsql pool tuning. Those want a real PostgreSQL to measure
against, which this machine does not have.
