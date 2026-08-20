# TUKI

**Public transportation, simplified.**

TUKI is a commuter-focused mobile application for planning and navigating trips using local transportation data. It combines route planning, GPS navigation, public transport information, AI-assisted trip planning, and ride matching in one system.

> **Plan. Navigate. Arrive.**

---

## What is TUKI?

TUKI is designed around the actual flow of a commuter's trip rather than treating route search and navigation as separate features.

```text
Search destination
        ↓
Plan journey
        ↓
Choose route
        ↓
Start navigation
        ↓
Board / transfer / alight
        ↓
Complete trip
```

The system supports both conventional trip planning and natural-language requests through its AI assistant.

## Core Features

**Journey Planning**
- Destination and place search
- Origin-to-destination journey planning
- Multi-leg routes and transfers
- Nearby public transportation discovery
- Jeepney routes and tricycle connection points
- Favorite and recent trips

**Live Navigation**
- GPS location tracking
- Turn-by-turn guidance
- Boarding and alighting guidance
- Landmark-based navigation
- Trip progress and distance tracking
- Off-route detection and rerouting
- Active trip restoration

**AI Assistant**
- Natural-language transportation requests
- Journey intent extraction
- AI-generated journey plans
- Integration with the normal navigation flow
- AI-assisted navigation speech
- NVIDIA NIM integration

**Ride Matching**
- Passenger ride requests
- Driver availability
- Passenger/driver matching
- Match acceptance, rejection, and cancellation
- Driver profiles and vehicle information
- Driver location updates

**Accounts**
- Username/password authentication
- Google Sign-In
- Facebook / OIDC authentication
- Email verification
- Password recovery
- User profile management

---

## System Architecture

```text
                         ┌──────────────────────┐
                         │      TUKI App        │
                         │ Kotlin / Compose     │
                         │                      │
                         │ Maps • Trips • AI   │
                         │ Auth • Navigation   │
                         │ Ride Matching       │
                         └──────────┬───────────┘
                                    │
                              HTTPS / API Key
                                    │
                                    ▼
                         ┌──────────────────────┐
                         │   ASP.NET Core API   │
                         │                      │
                         │ Controllers          │
                         │ Services             │
                         │ Repositories         │
                         │ Navigation           │
                         │ AI                   │
                         │ Ride Matching        │
                         └───────┬───────┬──────┘
                                 │       │
                    ┌────────────┘       └──────────────┐
                    ▼                                   ▼
             ┌─────────────┐                   ┌─────────────────┐
             │  SQL Server │                   │ External APIs   │
             │    TukiDb   │                   │                 │
             └─────────────┘                   │ Google Maps     │
                                               │ Valhalla        │
                                               │ Pelias          │
                                               │ NVIDIA NIM      │
                                               │ Google / FB     │
                                               └─────────────────┘
```

### Navigation flow

```text
POST /api/journeys/plan
          │
          ▼
POST /api/navigation/start
          │
          ▼
GET /api/navigation/active
          │
          ▼
POST /api/navigation/{sessionId}/location
          │
          ├── Boarding / Alighting
          ├── Progress tracking
          ├── Off-route detection
          └── Rerouting
          │
          ▼
      Trip complete
```

---

## Technology

| Area | Stack |
|---|---|
| Mobile | Kotlin, Jetpack Compose |
| Android | Android SDK 36 |
| Backend | C#, .NET 9, ASP.NET Core |
| Data | Entity Framework Core, SQL Server |
| Maps | Google Maps SDK for Android |
| Routing | Valhalla |
| Search | Pelias |
| AI | NVIDIA NIM |
| Authentication | Google, Facebook OIDC, API keys |
| Deployment | Docker, Render |

---

## Repository

```text
AUP/
├── frontend/                 Android application
│   ├── app/                  Jetpack Compose source
│   ├── ios/                  iOS project / work in progress
│   └── API_CONTRACT.md       API contract
│
├── backend/                  ASP.NET Core API
│   ├── Controllers/          API endpoints
│   ├── Services/             Application logic
│   ├── Models/               API and database models
│   ├── Helpers/              Shared utilities
│   └── Program.cs             Application startup
│
├── backend.Tests/            Backend tests
├── database/                 SQL Server schema and upgrade scripts
├── Dockerfile                Container build
└── README.md
```

---

## Development Setup

### Requirements

- Android Studio
- Android SDK Platform 36
- JDK 17 or newer
- .NET 9 SDK
- SQL Server
- Git

### Clone

```bash
git clone https://github.com/Mark-Batongbacal/AUP.git
cd AUP
```

### Backend

Backend development uses the configuration documented in [`backend/README.md`](backend/README.md). Keep credentials in the ignored `.env` / development configuration and never commit them.

```bash
cd backend
dotnet run
```

The default development API is:

```text
http://localhost:5129
```

### Android

Open `frontend/` in Android Studio and run the `app` configuration.

For an Android emulator connecting to a backend running on the host machine:

```properties
GOOGLE_SERVER_CLIENT_ID=YOUR_WEB_OR_SERVER_CLIENT_ID.apps.googleusercontent.com
BACKEND_BASE_URL=http://10.0.2.2:5129/
```

For the deployed backend:

```properties
BACKEND_BASE_URL=https://aup-0mjy.onrender.com/
```

Build from the command line:

**Windows**

```powershell
cd frontend
.\gradlew.bat :app:assembleDebug
```

**macOS / Linux**

```bash
cd frontend
./gradlew :app:assembleDebug
```

---

## API

The Android client communicates with the backend through the ASP.NET Core Web API using API-key authentication.

Current API areas include:

- Authentication and users
- Places and destination search
- Transportation routes
- Journey planning
- Navigation sessions
- Trip sessions
- Favorite trips
- AI assistant
- Ride matching
- Drivers and driver availability
- Driver location
- Tricycle points
- Health monitoring

### Health check

```http
GET /health
```

Deployed API:

```text
https://aup-0mjy.onrender.com/health
```

---

## Database

TUKI uses the existing **SQL Server `TukiDb`** database through Entity Framework Core.

Database schema and upgrade scripts are maintained in [`database/`](database/). The backend does not automatically modify the production schema when it starts.

For database changes, follow [`database/README.md`](database/README.md).

---

## Deployment

The backend is packaged with the repository-root `Dockerfile` and deployed as a containerized service on Render.

Production secrets and configuration are supplied through environment variables rather than committed files.

The Android application connects to the deployed backend through `BACKEND_BASE_URL`.

---

## Git Workflow

Development work is organized around `dev` and feature branches.

```bash
# Update development branch
git switch dev
git pull

# Create or update your feature branch
git switch <your-branch>
git merge dev

# Commit and push
git add <files>
git commit -m "Describe your change"
git push -u origin <your-branch>
```

Keep feature branches synchronized with `dev` before opening a pull request.

---

## Documentation

- [`frontend/README.md`](frontend/README.md) — Android setup and backend connection
- [`frontend/API_CONTRACT.md`](frontend/API_CONTRACT.md) — frontend/backend API contract
- [`backend/README.md`](backend/README.md) — backend configuration and deployment
- [`database/README.md`](database/README.md) — database setup and schema workflow

---

## Status

**In development.**

TUKI is currently being developed as a transportation and navigation platform focused on local commuting, real-time navigation, AI-assisted journey planning, and ride matching.

---

*Built for local commuters.* 🇵🇭
