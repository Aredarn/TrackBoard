# TrackPro ↔ TrackBoard API contract — v1

The contract between the TrackPro Android app and this backend, for the first vertical
slice: **publish a track, upload a session's laps, see the best-lap leaderboard.**

The machine-readable spec is [openapi.yaml](openapi.yaml). This page records the decisions
behind it, how it maps onto TrackPro's Room schema, and what each side has to change.

> **Status: backend implemented (2026-09-28); app side not started.** Every operation in
> the spec exists and its request and response shapes match field for field. Verified by 52
> integration tests against SQLite; **not yet run against real PostgreSQL**. See
> [Decisions made](#decisions-made-2026-09-28) for the product choices behind it.

---

## Scope

**In v1**

- Sign in (the existing auth endpoints, relocated under `/api/v1`)
- Vehicles, tracks, sessions with laps and sector splits
- A public best-lap leaderboard per track

**Deliberately not in v1**

| Left out | Why |
|---|---|
| Lap verification / anti-cheat | Decided against for now. Lap times are trusted as uploaded (see decision 5). |
| GPS traces (`LapInfoData`) | Their only v1 purpose was verification. They can come back with it, or with features like ghost laps. |
| Drag runs | `DerivedData` is never written by the app today, so there's no persisted drag result to upload. Drag timing needs its own storage design on the device first. |
| Raw session GPS (`RawGPSData`, `SmoothedGPSData`) | Too large, and not needed for ranking. |
| Restoring data to a new device | The phone stays the source of truth and the server is a one-way mirror. Two-way sync is a much larger problem. |
| Series / championship points | Already built, but it doesn't match what TrackPro records. It's parked, not deleted. |

---

## Core decisions

### 1. The client generates the IDs, and uploads are `PUT`

Every syncable record (vehicle, track, session) is created and updated with
`PUT /api/v1/{collection}/{id}`, where **`id` is a UUID the app generates** and stores
alongside the Room row.

- **Retries are safe.** A timed-out upload retried three times still produces one row.
- **No ID mapping table.** The app never has to learn a server-assigned ID.
- **Ownership still holds.** A `PUT` to an ID owned by someone else returns `403`. It
  never overwrites.

`201` means created and `200` means replaced. Either way the body is the stored record.

### 2. A session is uploaded whole

`PUT /sessions/{id}` carries the session **with all of its laps and sector splits**, and
replaces whatever was stored before. That gives the app one small idempotent call per
session rather than a per-lap state machine, and re-uploading after a local edit (voiding
the session, say) is the same call.

### 3. Track geometry is the ordered point list, exactly as the app stores it

TrackPro doesn't store its start, finish or sector gates. It derives them on the device
from the ordered points in `TrackCoordinatesData` (`calculateFinishLine`,
`calculateSectorLines`, `calculateSprintLines`). So the server stores **the same ordered
list with the same flags** and never re-derives gates for timing.

This carries a real consequence. **Two laps are only comparable if both were timed
against identical gates.** So:

- **A track's geometry freezes once it has a ranked lap.** A later `PUT` that changes
  `points` returns `409 TrackGeometryLocked`. Changing the geometry means publishing a new
  track, while name and country stay editable.
- **Gate derivation is part of the contract.** If a future app version changes how gates
  are calculated from points, old and new laps on the same track stop being comparable.
  `appVersion` goes up with every session so this can be detected afterwards.

### 4. Tracks are shared by publishing, not matched by geometry

Matching independently built tracks automatically ("these two Hungaroring tracks are the
same") is unreliable: every user places points differently, and a wrong match puts
incomparable laps on one leaderboard.

Instead:

1. A user **publishes** a track (`visibility: Published`). Private tracks still sync but
   are never listed or ranked.
2. Before publishing, the app calls `GET /tracks?near={lat},{lon}` and offers
   *"Hungaroring already exists, drive that one instead?"*
3. Driving a published track means **downloading it** (`GET /tracks/{id}`) into Room, so
   everyone on that leaderboard is timed against the same gates.

### 5. Which laps rank

There is no verification: a lap time is trusted as the app uploads it. A lap counts toward
the leaderboard when **all** of these hold, and each lap in a session response says so
with `countsForLeaderboard`:

- its session is `visibility: Ranked` and not `voided`
- the lap has no `signalGap`
- the track is `Published`

Because nothing checks the times, anyone who calls the API directly can post any lap time
they like. That's an accepted trade-off for v1. If it becomes a problem, the fix is to
reintroduce GPS-trace uploads with plausibility checks. The contract can add that as a new
endpoint without breaking existing clients.

### 6. One leaderboard, with the GPS source shown

Phone-GPS and ESP32 laps share **one leaderboard**. Every session records `gpsSource`
(`Wifi` or `Bluetooth` for the ESP32 module, `PhoneGps` for the phone), and each
leaderboard entry shows it, so viewers can tell a phone-timed lap apart from an ESP32 one.

### 7. Units and formats

| Kind | Format | Example |
|---|---|---|
| Durations (laps, sectors) | integer **milliseconds** | `92345` |
| Wall-clock times | ISO-8601, UTC | `2026-09-28T13:04:00Z` |
| Coordinates | WGS84 decimal degrees | `47.5789` |
| Enums | PascalCase strings | `Circuit`, `PhoneGps` |
| Errors | RFC 7807 `ProblemDetails` | unchanged from today |

### 8. Versioning

Every endpoint moves under **`/api/v1`**, auth included. Users update a phone app on their
own schedule, so the server will have to serve old clients for a long time. Moving the
routes costs nothing today and a great deal once apps are in the wild.

---

## Mapping from TrackPro's Room schema (v8)

| Room | Contract | Notes |
|---|---|---|
| `VehicleInformationData` | `Vehicle` | Same fields. Adds `id` (UUID). |
| `TrackMainData` | `Track` | `type` is `Circuit` or `Sprint` (exactly the app's values). `totalLength` → `lengthMeters`. Adds `id`, `visibility`. |
| `TrackCoordinatesData` | `Track.points[]` | **Order matters.** `seq` is the list position. Flags unchanged. |
| `SessionData` | `Session` | `startTime`/`endTime` (epoch ms) → ISO-8601. Weather fields grouped under `weather`. `eventType` → `name`. Adds `gpsSource`, `visibility`, `appVersion`. |
| `LapTimeData` | `Session.laps[]` | **`laptime` `"MM:SS.hh"` → `timeMs`.** Laps still `IN PROGRESS` or `INVALID` are not uploaded. A sprint run is a lap. |
| `SectorTimeData` | `Lap.sectors[]` | `splitTimeMs` is the time for **that sector alone**, not cumulative. That matches `CircuitTiming`, so no change. |
| `LapInfoData`, `RawGPSData`, `SmoothedGPSData`, `DerivedData` | — | Not uploaded in v1. |

---

## What each side has to build

### Backend (TrackBoard)

- New entities: `Track` + `TrackPoint`, `Session`, `Lap` + `LapSector`.
  `Circuit`/`Series`/`RaceEvent`/`Result` stay, unused by the app.
- `PUT` upsert handlers with ownership checks, reusing the existing resource-authorization
  handlers.
- Optional authentication on the three public read endpoints. ASP.NET Core authorization
  is all-or-nothing by default, so these need `[AllowAnonymous]`, and they read the caller
  only when a token is actually present.
- Leaderboard query: best ranked lap **per driver** per track, cached with HybridCache and
  tagged per track, the same pattern as today's series leaderboard.
- A route prefix of `/api/v1`.
- Real PostgreSQL. This slice is what finally runs the migrations against it.

### App (TrackPro)

- **Room migration 8 → 9:**
  - `remoteId TEXT` (UUID) on vehicles, tracks and sessions, backfilled for existing rows
  - `laptimeMs INTEGER` on `lap_time_data`, backfilled by parsing `laptime`. **Store the
    millisecond value from now on.** `TimeAttackViewModel` already has `timeMs` before
    formatting, and today that precision is thrown away when it's rounded to hundredths.
  - `gpsSource` on `session_data`, recording which `GpsProviderType` was active
  - `remoteTrackId` / published flag on `track_main_data`
  - `syncState` and `syncedAt` on sessions, so the queue knows what's dirty
- Sign-in screen, and tokens in encrypted storage with automatic refresh. It's opt-in:
  nothing changes for a user who never signs in, per `PRODUCT.md`.
- A sync worker (WorkManager, network-constrained) that uploads dirty vehicles, then
  tracks, then sessions, **in that order**, because each depends on the one before.
- An API client. OkHttp and kotlinx.serialization are already dependencies.
- Screens for publishing or finding tracks, and for the leaderboard.

---

## Decisions made (2026-09-28)

1. **Phone-GPS and ESP32 laps share one leaderboard.** Each entry shows its `gpsSource` so
   the difference stays visible (decision 6).
2. **Leaderboards and published tracks are public.** `GET /tracks`, `GET /tracks/{id}` and
   `GET /tracks/{id}/leaderboard` work without signing in, so a leaderboard can be shared
   as a link. A token is optional there and only adds the caller's own `me` entry, plus
   access to their private tracks. Everything that writes still requires sign-in.
3. **Refresh tokens last 90 days,** so someone who drives once a month doesn't have to sign
   in again every time. Already applied: `Jwt:RefreshTokenDays` is 90 in `appsettings.json`.
4. **No lap verification for now.** Lap times are trusted as uploaded, and GPS traces are
   left out of v1 because verification was their only purpose (decision 5).
5. **The championship model is parked.** Series, race events, results and points stay in
   the code and their tests keep passing, but the app doesn't use them and they won't be
   developed further for now.
