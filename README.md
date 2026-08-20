# TUKI

> **A smart public transportation companion for planning, navigating, and completing everyday trips.**

TUKI is a mobile transportation application focused on making local commuting easier. It combines journey planning, public transportation data, GPS navigation, AI-assisted trip planning, and ride matching behind a single mobile client and backend API.

---

## Overview

TUKI is built around the complete commuter journey:

```text
Destination
    ↓
Journey Planning
    ↓
Route Selection
    ↓
Navigation
    ↓
Boarding / Alighting
    ↓
Trip Completion
```

The application supports both traditional route planning and natural-language interaction through its AI assistant.

## Features

### Journey Planning

- Search for destinations and places
- Plan journeys from a selected origin or current location
- Combine multiple transportation legs
- Support transfers between routes
- Discover nearby public transportation
- Local jeepney and tricycle route support
- Favorite and recent journeys

### Navigation

- Live GPS tracking
- Turn-by-turn navigation
- Boarding and alighting guidance
- Trip progress and distance tracking
- Landmark-based navigation
- Off-route detection
- Automatic and manual rerouting
- Active-trip restoration

### AI Assistant

TUKI includes an AI-powered assistant that understands natural-language transportation requests.

```text
User request
     ↓
AI intent extraction
     ↓
Journey planning
     ↓
Route / navigation session
```

The AI layer can:

- Understand natural-language trip requests
- Extract journey intent
- Generate journey plans
- Pass AI-generated plans into the navigation flow
- Generate navigation speech
- Use NVIDIA NIM as the AI provider

### Ride Matching

TUKI also provides passenger and driver workflows:

- Passenger ride requests
- Ride matching
- Match acceptance / rejection / cancellation
- Driver profiles
- Vehicle information
- Driver availability sessions
- Driver location updates

### Authentication

- Username/password authentication
- Account registration
- Google Sign-In
- Facebook / OIDC authentication
- Email verification
- Password recovery
- User profile management
- API-key authentication between the mobile app and backend

---

## Architecture

```text
┌──────────────────────────┐
│       TUKI Mobile        │
│ Kotlin + Jetpack Compose │
└────────────┬─────────────┘
             │ HTTPS
             │ X-Api-Key
             ▼
┌──────────────────────────┐
│    ASP.NET Core API      │
│                          │
│ Auth                     │
│ Users                    │
│ Journeys                 │
│ Navigation               │
│ Trips                    │
│ AI                       │
│ Ride Matching            │
└──────┬───────────┬───────┘
       │           │
       ▼           ▼
┌────────────┐  ┌────────────────────┐
│ SQL Server │  │ External Services  │
│   TukiDb   │  │                    │
└────────────┘  │ Google Maps        │
                │ Valhalla           │
                │ Pelias             │
                │ NVIDIA NIM         │
                │ Google / Facebook  │
                └────────────────────┘
```

### Navigation API flow

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
            ├── Off-route detection
            └── Rerouting
            │
            ▼
       Trip complete
```

---

## Tech Stack

| Component | Technology |
|---|---|
| Mobile | Kotlin |
| UI | Jetpack Compose |
| Android | Android SDK |
| Backend | C# / .NET 9 / ASP.NET Core |
| Data Access | Entity Framework Core |
| Database | Microsoft SQL Server |
| Maps | Google Maps SDK for Android |
| Routing | Valhalla |
| Geocoding / Search | Pelias |
| AI | NVIDIA NIM |
| Authentication | Google / Facebook OIDC / API keys |
| Deployment | Docker / Render |

---

## Repository Structure

```text
AUP/
│
├── frontend/
│   ├── app/                 # Android application
│   ├── ios/                 # iOS project / development
│   └── API_CONTRACT.md      # Frontend ↔ backend contract
│
├── backend/
│   ├── Controllers/         # HTTP API endpoints
│   ├── Services/            # Application/business logic
│   ├── Models/              # API and database models
│   ├── Helpers/             # Shared utilities
│   └── Program.cs           # API startup/configuration
│
├── backend.Tests/           # Backend tests
│
├── database/
│   ├── TukiDbSchema.sql
│   ├── TukiNavigationSchema.sql
│   └── ...                  # SQL Server schema/setup scripts
│
├── Dockerfile               # Backend container
└── README.md
```

---

## Requirements

### Frontend

- Android Studio
- Android SDK Platform 36
- JDK 17+
- Android emulator API 26+ or a physical Android device

### Backend

- .NET 9 SDK
- SQL Server
- Git
- Docker (optional for local container development)

---

## Getting Started

### 1. Clone

```bash
git clone https://github.com/Mark-Batongbacal/AUP.git
cd AUP
```

### 2. Start the backend

```bash
cd backend
dotnet run
```

The default local API is available at:

```text
http://localhost:5129
```

For development configuration, use the files/environment variables described in [`backend/README.md`](backend/README.md).

### 3. Configure the Android app

Open `frontend/` in Android Studio and configure `frontend/local.properties`.

For an Android emulator connecting to a backend running on the host machine:

```properties
GOOGLE_SERVER_CLIENT_ID=YOUR_WEB_OR_SERVER_CLIENT_ID.apps.googleusercontent.com
BACKEND_BASE_URL=http://10.0.2.2:5129/
```

For the deployed API:

```properties
BACKEND_BASE_URL=https://aup-0mjy.onrender.com/
```

`10.0.2.2` maps the Android emulator to the host machine's `localhost`.

### 4. Build the Android application

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

## Backend Configuration

Development secrets should be kept outside committed source files.

Typical backend configuration includes:

```text
Login__Users__0__UserName=<username>
Login__Users__0__Password=<password>
NVIDIA_API_KEY=<nvidia-api-key>
ConnectionStrings__TukiDbConnection=<sql-server-connection-string>
Valhalla__BaseUrl=<valhalla-url>
Pelias__BaseUrl=<pelias-url>
Facebook__AppId=<facebook-app-id>
Facebook__AppSecret=<facebook-app-secret>
```

**Do not commit `.env`, `appsettings.Development.json`, passwords, API keys, OAuth secrets, or database credentials.**

See [`backend/README.md`](backend/README.md) for the complete backend configuration and deployment procedure.

---

## Database

TUKI uses Microsoft SQL Server with Entity Framework Core and the `TukiDb` database.

The repository contains SQL schema and upgrade scripts under [`database/`](database/).

The backend is designed to work with the existing database schema and does **not** automatically modify the production database on startup.

For database changes, follow the workflow documented in [`database/README.md`](database/README.md).

---

## API

The Android application communicates with the backend through the ASP.NET Core Web API.

The API currently covers:

- Authentication
- User accounts
- Places and destination search
- Transportation routes
- Journey planning
- Navigation sessions
- Trip sessions
- Favorite trips
- AI assistant
- Ride matching
- Drivers
- Driver availability
- Driver location
- Tricycle points
- Health monitoring

### Health Check

```http
GET /health
```

Deployed API:

```text
https://aup-0mjy.onrender.com/health
```

---

## Deployment

The backend is containerized using the repository-root `Dockerfile`.

The current deployment target is Render. Production configuration is supplied through environment variables/secrets rather than committed configuration files.

The Android client can connect to the deployed API by setting:

```properties
BACKEND_BASE_URL=https://aup-0mjy.onrender.com/
```

---

## Development Workflow

The project uses a development branch with separate feature branches.

Before starting work:

```bash
git switch dev
git pull
git switch <your-branch>
git merge dev
```

Creating a new feature branch:

```bash
git switch dev
git pull
git switch -c <your-branch>
```

Commit and push:

```bash
git status
git add <files>
git commit -m "Describe your change"
git push -u origin <your-branch>
```

Keep feature branches synchronized with `dev` before opening a pull request.

---

## Documentation

| Document | Description |
|---|---|
| [`frontend/README.md`](frontend/README.md) | Android setup and API connection |
| [`frontend/API_CONTRACT.md`](frontend/API_CONTRACT.md) | Frontend/backend API contract |
| [`backend/README.md`](backend/README.md) | Backend configuration and deployment |
| [`database/README.md`](database/README.md) | Database setup and schema workflow |

---

## Project Status

**In development.**

Current development areas include transportation routing, real-time navigation, AI-assisted trip planning, local transportation support, and ride matching.

---

## License

This project is currently maintained as an academic/software development project. Licensing information will be added when finalized.
