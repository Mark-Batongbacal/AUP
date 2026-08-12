# Frontend development guide

This directory contains the Android app, built with Jetpack Compose.

## Run the app

Before starting, install Android Studio, Android SDK Platform 36, and JDK 17 or
newer. Create an Android emulator running API 26 or newer, or connect a
physical device.

Open this `frontend/` directory in Android Studio and run the `app`
configuration. To build from a terminal instead:

```bash
./gradlew :app:assembleDebug
```

## Run and test the backend

The API is in the sibling `backend/` directory. Start it in a second terminal:

```bash
cd ../backend
dotnet restore
dotnet run --launch-profile http
```

The development server listens on `http://localhost:5129`. Confirm that it is
running by requesting its OpenAPI document:

```bash
curl http://localhost:5129/openapi/v1.json
```

You can also open `backend/backend.http` in a JetBrains IDE or VS Code REST
Client extension and send its request. It uses the same local server address.

## Connect the Android app to the local API

The current app is a UI-only scaffold; it does not make backend requests yet.
When adding API calls, use the address that matches where the app runs:

| App target | API base URL |
| --- | --- |
| Android Emulator | `http://10.0.2.2:5129` |
| Physical device | `http://<your-computer-LAN-IP>:5129` |
| Backend tested from the same computer | `http://localhost:5129` |

Before the app can call an HTTP API, add the Android `INTERNET` permission and
configure the backend's CORS policy for the client. For a physical device,
ensure the phone and computer are on the same network and that the firewall
permits the API port.

For production, use HTTPS and do not rely on the emulator-only `10.0.2.2`
address.
