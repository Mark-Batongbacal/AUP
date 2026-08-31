# Tuki (AUP)

Tuki is an Android and ASP.NET Core public-transport navigation project built around informal and semi-formal transport in Pampanga. The initial coverage focuses on Porac, Angeles, Dau, and nearby areas where a commuter may need to combine walking, tricycles, and jeepneys instead of following a single fixed transit route.

The project has grown beyond a simple route finder. Its goal is to maintain a usable commuter journey from origin to destination: find practical transport combinations, identify where to board and alight, estimate fare and duration, guide the user through the active trip, and reroute when the remaining journey is no longer sensible.

## What Tuki does

Tuki's routing backend is deterministic. The AI assistant is used to understand what the commuter means, while the backend remains responsible for deciding what routes actually exist and what journey should be offered.

Current and actively developed capabilities include:

- Multimodal journey planning across walking, tricycle, and jeepney legs.
- Direct and multi-transfer jeepney routing.
- Origin and destination access legs.
- Fare, duration, walking-distance, and route-ranking calculations.
- Boarding and alighting points with route geometry per leg.
- Support for looping routes and controlled same-route/self-transfer cases when meaningful forward progress exists.
- Active-trip state, navigation instructions, remaining distance, and rerouting logic.
- Destination/place resolution through real place services rather than LLM-generated coordinates.
- Android navigation UI with route choices, active navigation, recent destinations, favorites, and the Tuki assistant.

The difficult part of the system is no longer merely finding a mathematically valid path. A route also has to be reasonable for a real commuter. Current routing work therefore focuses heavily on ranking quality, transfer quality, realistic duration estimates, and avoiding technically valid but impractical journeys.

## Routing architecture

The backend owns transportation logic, including:

- route discovery and route geometry;
- walking and tricycle access;
- jeepney boarding, alighting, and transfers;
- fare calculation;
- travel-time estimation;
- route ranking such as fastest, cheapest, and balanced options;
- active-trip progression;
- deviation detection and rerouting; and
- confirmation of meaningful route changes during an active trip.

The intended journey graph can combine legs such as:

```text
walk -> tricycle -> jeepney -> transfer -> jeepney -> walk
```

Not every journey uses every mode. The planner generates candidates and ranks the combinations that are actually useful for the requested trip.

### Looping routes and self-transfers

Some local jeepney routes loop back near earlier portions of their own geometry. Treating a route ID as permanently visited can incorrectly remove valid journeys, while blindly allowing reuse can create cycles.

Tuki therefore treats same-route transfers as directed progress through the route. Candidate self-transfers are constrained by forward progress, transfer walking distance, and cycle-prevention rules so that a looping route can be reused only when doing so represents meaningful travel rather than graph churn.

## Tuki AI assistant

The assistant follows one important rule:

> The AI understands the commuter; the deterministic backend understands transportation.

The LLM layer may interpret messages such as:

- `₱13 na lang pera ko.`
- `Pagod na ako.`
- `Ayoko mag-trike.`
- `Mas konting lakad sana.`

Those statements are converted into structured trip constraints or actions. The backend then evaluates the real routes, fares, geometry, and journey options.

The AI should not invent:

- jeepney routes;
- fares;
- coordinates;
- travel times;
- boarding/alighting points; or
- route connectivity.

These values come from Tuki's routing and place-service layers.

### Assistant state and constraints

The current assistant model supports trip/conversation-level information such as:

- budget / maximum fare;
- optimization preference;
- maximum walking distance;
- walking preference; and
- transport modes to avoid.

Preferences are intended to remain scoped to the current trip or conversation unless the user explicitly asks for something to be remembered permanently.

Meaningful changes to an active route should be presented to the commuter before Tuki replaces the current journey. Mentioning another place in conversation also does not automatically mean that the active destination should change.

### Not implemented yet: arrival-time constraints

Natural-language arrival deadlines such as:

```text
Kailangan kong makarating before 5 PM.
```

are part of the planned assistant behavior, but **arrival/deadline time constraints are not currently represented in the assistant constraint model**. They should therefore not be treated as a supported routing constraint yet.

A future implementation should add a structured time/deadline value to assistant intent and planning state, then let the deterministic backend determine whether candidate journeys can satisfy it.

## Navigation

Tuki navigation is intended to operate on the remaining actionable journey rather than simply displaying the original polyline forever.

Navigation work includes:

- current GPS position;
- current journey leg;
- next instruction;
- remaining distance;
- passed-route progress;
- boarding and alighting transitions;
- route deviation; and
- rerouting when the current plan has become impractical.

This is an active development area. In particular, GPS accuracy, line progression, reroute thresholds, and making the displayed route consistently match the user's real progress still require hardening.

## Places and destination search

Coordinates used by Tuki should come from real place/search services, not from the LLM.

The architecture is designed around Pelias as the primary geocoder/search source, with richer external place lookup used selectively when local results are weak or ambiguous. Results should be merged and deduplicated before they are shown to the commuter.

This separation keeps natural-language understanding independent from geographic truth and helps control third-party API usage.

## Current development priorities

The largest remaining engineering risks are:

1. **Route quality** — a route can be valid without being a route a real commuter would choose.
2. **Transfer quality** — avoid unnecessary, backwards, or awkward transfers while preserving useful multi-transfer journeys.
3. **Travel-time realism** — walking, tricycle, waiting, and jeepney durations must remain believable.
4. **Navigation reliability** — route progression, GPS matching, deviation detection, and rerouting must agree with the user's actual movement.
5. **Transit data quality** — local jeepney geometry, boarding behavior, tricycle terminals/TODAs, fare rules, and route direction are often unavailable as clean public transit feeds.
6. **Security and authorization** — user-facing clients must not gain administrative capabilities merely by knowing a shared API key.
7. **Production infrastructure** — routing, geocoding, API, database, reverse proxy, caching, and observability need to remain maintainable as usage grows.

The practical rollout strategy is to make a limited geographic area highly reliable before expanding coverage. Adding another city without sufficiently accurate local transit data increases the routing and debugging surface significantly.

## Project layout

- `frontend/` — Android app built with Jetpack Compose.
- `backend/` — ASP.NET Core API.
- `Tuki.Admin/` — ASP.NET Core admin web application.
- `database/` — SQL Server schema script and local database setup notes.
- `Dockerfile` — Render container build for the backend; it must remain at the repository root.
- `Tuki.Admin/Dockerfile` — container build for the admin web application.
- `docker-compose.yml` — runs the backend and admin containers together for local/container deployments.

### Production containers

The production Compose stack is designed for an Ubuntu 24.04 VM and includes
the backend, admin application, SQL Server, Valhalla, Pelias, and Caddy.
Production secrets are held in Ansible Vault. During deployment, Ansible
renders service-specific files under `runtime/`, and Compose reads those files
with `env_file`. The root `.env.example` is placeholder-only documentation and
is not the production secret source. See `infra/ansible/README.md` for Vault
editing and deployment commands.

Only Caddy publishes host ports 80 and 443. Backend (`5129`), admin (`5030`),
SQL Server (`1433`), Valhalla (`8002`), Pelias (`4000`), and Pelias
Elasticsearch remain on the private Compose network. SSH remains a host service
on port 22.

Persistent data defaults to `/opt/tuki/data`. Compose does not initialize,
restore, overwrite, or delete the production database. Follow
`infra/ansible/README.md` for the GCP deployment and separate migration
workflow.

## Prerequisites

- Android Studio, Android SDK Platform 36, and JDK 17 or later for the Android app.
- .NET 9 SDK for the backend.
- Git for source control.
- GitHub CLI (`gh`) if you want to authenticate and work with GitHub from the terminal.

## Run locally

### Android frontend

Open `frontend/` in Android Studio, select an emulator or physical Android device, then run the `app` configuration. You can also build it from Windows PowerShell:

```powershell
cd frontend
.\gradlew.bat :app:assembleDebug
```

### Backend

The backend loads local credentials from `backend/.env` when `ASPNETCORE_ENVIRONMENT=Development`. The file is ignored by Git.

Create `backend/.env` with your own values:

```text
Login__Users__0__UserName=admin@aup.edu
Login__Users__0__Password=<secure-password>
Login__Users__1__UserName=<second-user-name>
Login__Users__1__Password=<secure-password>
Login__Users__2__UserName=<third-user-name>
Login__Users__2__Password=<secure-password>
Login__Users__3__UserName=<fourth-user-name>
Login__Users__3__Password=<secure-password>
NVIDIA_API_KEY=<your-nvidia-api-key>
ConnectionStrings__TukiDbConnection=<sql-server-connection-string>
Valhalla__BaseUrl=<your-valhalla-base-url>
Pelias__BaseUrl=<your-pelias-base-url>
```

Never commit real passwords or API keys. `appsettings.json` contains only non-secret defaults; `appsettings.Development.json` and `.env` are ignored.

For Azure Container Apps, configure ingress with target port `5129`. Add all credentials and service URLs as Container App secrets/environment variables; the production container does not load `backend/.env`.

The SQL Server schema is tracked in `database/TukiDbSchema.sql`; run it against `TukiDb` in SSMS when setting up or reconciling a local database.

Start the backend in Windows PowerShell:

```powershell
cd backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

On macOS/Linux, use `ASPNETCORE_ENVIRONMENT=Development dotnet run` instead.

The local API runs at `http://localhost:5129` by default. Use `backend/backend.http` to test login, authenticated API calls, and the configured LLM request.

The public health endpoint requires no API key:

```text
GET /health
```

It returns a `200 OK` response with `status` and the server-side response time in milliseconds.

## Set up Git and GitHub CLI

Install Git on Windows (open PowerShell as a normal user):

```powershell
winget install --id Git.Git -e --source winget
```

Configure the identity used for your commits:

```powershell
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

Install GitHub CLI on Windows, then authenticate:

```powershell
winget install --id GitHub.cli -e --source winget
gh auth login
```

Restart PowerShell if either command is not found. In `gh auth login`, choose `GitHub.com`, then use the browser login flow and select HTTPS or SSH according to your preferred Git setup. Confirm authentication with:

```powershell
gh auth status
```

Clone the project if you do not already have it:

```powershell
git clone <repository-url>
cd AUP
```

## Team branch workflow

Before starting work, update `dev`, switch to your own branch, and merge the current development changes:

```powershell
# 1. Update dev
git switch dev
git pull

# 2. Switch to your branch
git switch <your-branch>

# 3. Bring in the latest dev changes
git merge dev

# 4. Start coding
```

For a new branch:

```powershell
git switch dev
git pull
git switch -c <your-branch>
```

Before committing, check what will be included and ensure `.env` is not staged:

```powershell
git status
git add <files-you-changed>
git commit -m "Describe your change"
git push -u origin <your-branch>
```

See the [frontend development guide](frontend/README.md) for instructions on running the app alongside the local backend.

## Google Maps API key

The frontend reads the Maps SDK for Android key from `frontend/local.properties`, which is excluded from Git. Add your own key there before running the map:

```properties
MAPS_API_KEY=YOUR_API_KEY
```

`frontend/local.defaults.properties` only contains a non-secret placeholder so the project can build without committing a real API key.
