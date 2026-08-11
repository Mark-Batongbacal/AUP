# AUP

Android frontend and ASP.NET backend for the AUP project.

## Prerequisites

### Frontend

To build and run the Android app, install:

- Android Studio with Android SDK Platform 36
- JDK 17 or newer, available on `PATH`, with `JAVA_HOME` configured
- An Android emulator or physical device running Android 8.0 (API 26) or newer
- Internet access for the first Gradle build

The Android app lives in `frontend/` and uses Jetpack Compose. Open that
directory in Android Studio, or build it from the command line:

```bash
cd frontend
./gradlew :app:assembleDebug
```

### Backend

Install the .NET 9 SDK, then start the API:

```bash
cd backend
dotnet restore
dotnet run
```

In development, the API listens on `http://localhost:5129` and
`https://localhost:7184`.

### Supabase (optional)

The current frontend and backend do not yet require Supabase. When database or
authentication features are added, install Docker (or Docker Desktop) and the
Supabase CLI, then run this from the repository root:

```bash
supabase start
```

When the Android emulator needs to call the local backend, use `10.0.2.2` for
the backend host instead of `localhost`.
