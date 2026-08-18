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

## Local authentication config

For real Google login, add the backend Web/Server OAuth client ID to
`frontend/local.properties`. This is the same client ID configured on the
backend as `Google__ClientId`, not the Android OAuth client ID.

```properties
GOOGLE_SERVER_CLIENT_ID=YOUR_WEB_OR_SERVER_CLIENT_ID.apps.googleusercontent.com
BACKEND_BASE_URL=https://aup-0mjy.onrender.com/
```

When testing the backend running on your development machine from an Android
emulator, use:

```properties
BACKEND_BASE_URL=http://10.0.2.2:5129/
```

## Run and test the backend

The backend API is currently hosted at https://aup-0mjy.onrender.com/

You can test your connection to the API by running https://aup-0mjy.onrender.com/health

It should return OK
