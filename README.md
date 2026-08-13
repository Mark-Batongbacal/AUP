# AUP

Android frontend and ASP.NET backend for the AUP project.

The Android app lives in `frontend/` and uses Jetpack Compose. Open that
directory in Android Studio, configure a JDK, then run:

```bash
./gradlew :app:assembleDebug
```

## Google Maps API key

The frontend reads the Maps SDK for Android key from `frontend/local.properties`,
which is excluded from Git. Add your own key there before running the map:

```properties
MAPS_API_KEY=YOUR_API_KEY
```

`frontend/local.defaults.properties` only contains a non-secret placeholder so
the project can build without committing a real API key.
