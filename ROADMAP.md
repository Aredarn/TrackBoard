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
- [ ] Use **`HybridCache`** (`Microsoft.Extensions.Caching.Hybrid`, .NET 9+) backed by Redis:
  in-process L1 + Redis L2 with built-in stampede protection.
  **[was: `@Cacheable`]** — .NET has no attribute-based caching, so caching is explicit
  `GetOrCreateAsync(key, factory, tags)` calls inside the service. More code, more control.
- [ ] Invalidate on write with `RemoveByTagAsync("leaderboard:{seriesId}")` from
  `ResultService.SubmitResultAsync`. **[was: `@CacheEvict`]**
- [ ] Pre-populate hot leaderboards on startup with a `BackgroundService` / `IHostedService`.
  **[was: `ApplicationRunner`]**
- [ ] Always set `Expiration` / `LocalCacheExpiration` — never cache indefinitely.
- [ ] Cache keys must include `seriesId` and `season` to prevent cross-series pollution.

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
- [ ] 🔒 `MapHealthChecks("/health")` (liveness, anonymous) and `/health/ready` (DB + Redis,
  authorized). Metrics via OpenTelemetry. **[was: `/actuator/health`, `/actuator/metrics`]** —
  there is no Actuator; you assemble this yourself.
- [ ] Instrument leaderboard endpoints with `System.Diagnostics.Metrics.Meter` histograms to
  compare cache hit vs miss latency. **[was: `@Timed`]**
- [ ] Log at Warning/Error in production via `appsettings.Production.json`. Consider Serilog for
  structured logs.

---

## Phase 5 — Quality (13 tasks)

*What separates a demo from a production codebase.*

### Testing
- [ ] xUnit unit tests for all service logic, especially points-calculation edge cases
  (ties, DNF, partial grids, scheme changes mid-season). Mock with NSubstitute.
  Assert with `Shouldly` or plain xUnit — FluentAssertions v8+ is commercially licensed.
- [ ] Integration-test the full result-submission flow with `WebApplicationFactory<Program>` +
  `Testcontainers.PostgreSql`. **[was: `@SpringBootTest` + Testcontainers]**
  ⚠️ Gotcha: `Program.cs` uses top-level statements, so add `public partial class Program { }`
  at the bottom of it or `WebApplicationFactory<Program>` won't compile.
- [ ] 🔒 Security tests: 401 on missing token, 403 on wrong owner, 200 on valid.
- [ ] 🔒 JWT tests: expired token, tampered signature, missing `Bearer` prefix, wrong audience.
- [ ] 70%+ coverage on services and controllers via
  `dotnet test --collect:"XPlat Code Coverage"` (coverlet) + ReportGenerator.

### Code quality
- [ ] `.editorconfig` + `dotnet format --verify-no-changes` enforced in CI.
  **[was: Checkstyle / Spotless]**
- [ ] Enable `<EnableNETAnalyzers>`, `<AnalysisMode>All</AnalysisMode>`,
  `<TreatWarningsAsErrors>` in the csproj; add SonarCloud (free for public repos).
  **[was: SpotBugs / SonarQube]**
- [ ] Thin controllers — parsing and delegation only, no business logic. Fix `VehicleController`
  as the reference example.
- [ ] No `#pragma warning disable` without a justification comment. **[was: `@SuppressWarnings`]**

### CI/CD
- [ ] GitHub Actions: `actions/setup-dotnet` → `dotnet restore/build/test` on every push and PR.
- [ ] Fail the build on test failure or coverage below threshold (coverlet `/p:Threshold=70`).
- [ ] 🔒 Dependabot for NuGet (`.github/dependabot.yml`).
- [ ] 🔒 `dotnet list package --vulnerable --include-transitive` as a CI gate. NuGetAudit is on by
  default in .NET 8+ — promote its warnings to errors. **[was: OWASP dependency-check]**

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
| 4 · Performance | 12 | 0 | 0 | 1 | 0 |
| 5 · Quality | 13 | 0 | 0 | 4 | 0 |
| 6 · Open source | 13 | 0 | 0 | 0 | 0 |
| **Total** | **82** | **38** | **8** | **9** | **1** |

All 8 critical tasks are implemented and were exercised against a running server.

Still open in phase 1: branch rename, licence decision, branch protection, Docker verification —
three need repo-owner decisions, one needs Docker installed.
Still open in phase 3: both remaining items are deployment-time actions with no target yet.
