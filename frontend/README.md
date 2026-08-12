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

The backend API is currently hosted at https://aup-0mjy.onrender.com/

You can test your connection to the API by running https://aup-0mjy.onrender.com/health

It should return OK
