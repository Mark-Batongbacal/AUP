# TUKI 🚌

**Your everyday companion for getting around.**

TUKI is a public transportation and navigation app built for commuters. It combines route planning, live GPS navigation, local transportation data, ride matching, and an AI assistant into one mobile experience.

> **TUKI** — plan your trip, find your route, and get there.

## What TUKI can do

### 🗺️ Plan a journey

- Search for destinations and places.
- Plan a trip from your current location or a selected origin.
- Get transportation routes and route connections.
- Support transfers between different transportation legs.
- Find nearby public transportation routes.
- Support local jeepney routes and tricycle pickup/connection points.
- Save favorite trips and access recent journeys.

### 📍 Navigate in real time

Once a journey starts, TUKI can follow the commuter throughout the trip.

- Live GPS location tracking.
- Turn-by-turn navigation.
- Boarding and alighting guidance.
- Distance and trip progress tracking.
- Landmark-based navigation references.
- Off-route detection.
- Automatic and manual rerouting.
- Restore an active trip when returning to the app.

### 🤖 Ask TUKI

TUKI includes an AI assistant for natural-language trip planning and navigation support.

- Ask questions using normal language.
- Extract journey intent from AI requests.
- Generate journey plans from natural-language instructions.
- Send AI-generated plans into the normal navigation flow.
- Generate AI-assisted navigation speech.
- Powered by NVIDIA NIM.

### 🚗 Ride matching

TUKI also includes passenger and driver workflows for ride matching.

- Create and manage passenger ride requests.
- Match passengers with available rides.
- Accept, reject, and cancel matches.
- Driver profiles and vehicle information.
- Driver availability sessions.
- Driver location updates.

### 🔐 Accounts & authentication

- Username/password authentication.
- Account registration.
- Google Sign-In.
- Facebook Sign-In / OIDC.
- Email verification.
- Password recovery.
- User profile management.
- API-key authentication between the app and backend.

## Tech stack

| Layer | Technology |
|---|---|
| Mobile | Kotlin + Jetpack Compose |
| Android | Android SDK |
| Backend | C# + .NET 9 + ASP.NET Core |
| ORM | Entity Framework Core |
| Database | Microsoft SQL Server |
| Maps | Google Maps SDK for Android |
| Routing | Valhalla |
| Search / Geocoding | Pelias |
| AI | NVIDIA NIM |
| Authentication | Google, Facebook OIDC, API keys |
| Deployment | Docker + container hosting |

## Architecture

```text
┌─────────────────────────────────┐
│          TUKI Mobile App        │
│        Kotlin / Jetpack         │
│             Compose             │
│                                 │
│ Search • Trips • Maps • AI      │
│ Navigation • Auth • Ride Match  │
└────────────────┬────────────────┘
                 │ HTTPS
                 │ X-Api-Key
                 ▼
┌─────────────────────────────────┐
│       ASP.NET Core Backend      │
│                                 │
│ Auth • Users • Journeys         │
│ Routing • Navigation • AI      │
│ Trips • Drivers • Ride Matching │
└───────┬──────────┬──────────────┘
        │          │
        ▼          ▼
┌─────────────┐  ┌────────────────┐
│ SQL Server  │  │ External APIs  │
│   TukiDb    │  │                │
└─────────────┘  │ Valhalla       │
                 │ Pelias         │
                 │ NVIDIA NIM     │
                 │ Google         │
                 │ Facebook       │
                 └────────────────┘
```

## Project structure

```text
AUP/
├── frontend/          # Android application
│   ├── app/           # Jetpack Compose app
│   ├── ios/           # iOS resources / work in progress
│   └── API_CONTRACT.md
│
├── backend/           # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Services/
│   ├── Repositories/
│   └── ...
│
├── backend.Tests/     # Backend automated tests
├── database/          # SQL Server scripts and database setup
├── Dockerfile         # Backend container build
└── README.md
```

## Getting started

### Requirements

- [Android Studio](https://developer.android.com/studio)
- Android SDK Platform 36 or newer
- JDK 17+
- .NET 9 SDK
- SQL Server for local backend development
- Git

### 1. Clone the repository

```bash
git clone https://github.com/Mark-Batongbacal/AUP.git
cd AUP
```

### 2. Run the Android app

Open `frontend/` in Android Studio, select an emulator or Android device, and run the `app` configuration.

Or build from the terminal:

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

### 3. Configure the frontend

Create/update `frontend/local.properties`:

```properties
GOOGLE_SERVER_CLIENT_ID=YOUR_WEB_OR_SERVER_CLIENT_ID.apps.googleusercontent.com
BACKEND_BASE_URL=http://10.0.2.2:5129/
```

`10.0.2.2` points an Android emulator to the host machine's `localhost`.

For the deployed API:

```properties
BACKEND_BASE_URL=https://aup-0mjy.onrender.com/
```

### 4. Configure the backend

During development, backend secrets are loaded from `backend/.env`.

Example:

```text
Login__Users__0__UserName=admin@aup.edu
Login__Users__0__Password=<secure-password>
NVIDIA_API_KEY=<your-nvidia-api-key>
ConnectionStrings__TukiDbConnection=<sql-server-connection-string>
Valhalla__BaseUrl=<your-valhalla-base-url>
Pelias__BaseUrl=<your-pelias-base-url>
Facebook__AppId=<your-facebook-app-id>
Facebook__AppSecret=<your-facebook-app-secret>
```

**Never commit real credentials or secrets to Git.**

Start the backend:

**Windows PowerShell**

```powershell
cd backend
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

**macOS / Linux**

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

The local API runs on `http://localhost:5129` by default.

## API

The Android application communicates with the ASP.NET Core backend through HTTPS and API-key authentication.

The main navigation flow is:

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

The API also provides endpoints for:

- Authentication and users
- Places and destination search
- Transportation routes
- Journey planning
- Navigation sessions
- Trip sessions
- Favorite trips
- AI assistant
- Ride matching
- Drivers
- Driver availability and location
- Tricycle points
- Health monitoring

### Health check

The backend exposes a public health endpoint:

```http
GET /health
```

For the deployed backend, it can be used to verify that the API is online.

## Backend deployment

The backend is containerized using the repository-root `Dockerfile` and can be deployed to a container platform such as Render or Azure Container Apps.

Production configuration should be provided through environment variables/secrets rather than committed configuration files.

The frontend can then point `BACKEND_BASE_URL` at the deployed API.

## Database

TUKI uses **Microsoft SQL Server** with the `TukiDb` database through Entity Framework Core.

Database scripts and setup resources are available under `database/`.

The application does not automatically change the production database schema on startup.

## Development workflow

The project uses separate development and feature branches.

Before starting work:

```bash
git switch dev
git pull
git switch <your-branch>
git merge dev
```

For a new branch:

```bash
git switch dev
git pull
git switch -c <your-branch>
```

Commit and push your changes:

```bash
git status
git add <files-you-changed>
git commit -m "Describe your change"
git push -u origin <your-branch>
```

Keep your branch updated with `dev` before opening a pull request.

## Security

- Do not commit `.env` files.
- Do not commit passwords, API keys, OAuth secrets, or database credentials.
- Store production secrets in the hosting provider's environment/secrets configuration.
- Keep local frontend credentials in ignored configuration files.
- Never use real production credentials in sample configuration.

## Documentation

- [`frontend/README.md`](frontend/README.md) — Android setup and backend connection.
- [`frontend/API_CONTRACT.md`](frontend/API_CONTRACT.md) — Android/backend API contract.
- [`backend/README.md`](backend/README.md) — backend setup and deployment.
- [`database/README.md`](database/README.md) — database setup and schema workflow.

## Status

🚧 **TUKI is actively under development.**

The current project focuses on commuter journey planning, GPS navigation, local transportation routing, AI-assisted travel, and ride matching.

---

Built for commuters. Built with local transportation in mind. 🇵🇭
