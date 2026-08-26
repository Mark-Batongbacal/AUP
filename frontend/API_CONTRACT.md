# Android API contract

Source checked: backend `dev` at `78252e9` plus the navigation-facade changes in this worktree. All paths below are relative to `BuildConfig.BACKEND_BASE_URL`.

## High-level mobile navigation API (recommended)

Android should use `NavigationRepository` for an active journey. Each operation returns one complete `NavigationSnapshotDto`; the app does not need a follow-up session, instruction, or landmark request.

| Flow | Endpoint | Repository method |
|---|---|---|
| Plan recommendations | `POST api/journeys/plan` | `RoutingRepository.planJourneys(request)` |
| Start a selected recommendation | `POST api/navigation/start` | `startNavigation(recommendationId)` |
| Restore the active journey | `GET api/navigation/active` | `getActiveNavigation()` |
| Send GPS and receive updated state | `POST api/navigation/{sessionId}/location` | `updateLocation(sessionId, update)` |
| Confirm boarding | `POST api/navigation/{sessionId}/boarding` | `confirmBoarding(sessionId)` |
| Confirm alighting | `POST api/navigation/{sessionId}/alighting` | `confirmAlighting(sessionId)` |
| Cancel | `POST api/navigation/{sessionId}/cancel` | `cancel(sessionId)` |
| Explicit reroute from the device's current GPS | `POST api/navigation/{sessionId}/reroute` | `reroute(sessionId, request)` |

`NavigationSnapshotDto.state`, `nextInstruction`, confirmation flags, distances, leg data, and landmark role/relation are authoritative. `spokenInstruction` is presentation text produced by the backend and may be absent; clients must never parse it to determine state. Landmark wire roles are `BOARD_REFERENCE`, `ALIGHT_REFERENCE`, and `PROGRESS_REFERENCE`; relevant relations are `NEAR_BOARD_POINT`, `BEFORE_ALIGHT`, and `ALONG_ROUTE`.

Live-navigation reroute requests send `latitude`, `longitude`, `accuracyMeters`, `timestamp`, and optional `speedMetersPerSecond`/`bearingDegrees` directly to `/reroute`. The backend validates and persists that fix and uses the same coordinates as the replacement route origin; an explicit reroute does not require a preceding `/location` request. Callers without supplied GPS retain the legacy session-location fallback.

Use `RoutingRepository.planJourneys` for the mobile flow. Authenticated requests return persisted recommendation IDs that can be passed to `startNavigation`; guest requests return transient in-memory recommendation IDs for route inspection and local guest tracking only. Guest clients must not call authenticated persistence endpoints for recents, favorites, or backend navigation sessions.

Recent journeys are persisted only for authenticated users and are exposed through `GET api/trips/recent`. The response includes completed/cancelled trip sessions only, plus reroute metadata when the saved session has rerouted.

## Low-level / legacy / internal APIs

The APIs below remain supported for compatibility and administrative/internal workflows. New Android navigation flows should not coordinate `Trips` and `TripSessions` directly.

| Backend controller | Endpoint | Retrofit method | Repository method | Response DTO |
|---|---|---|---|---|
| Auth | `POST api/auth/login` | `AuthApi.login` | `AuthRepository.login` | `LoginResponseDto` |
| Auth | `POST api/auth/register` | `AuthApi.register` | `AuthRepository.register` | `RegisterResponseDto` |
| Auth | `POST api/auth/google` | `AuthApi.google` | `AuthRepository.loginWithGoogle` | `LoginResponseDto` |
| Auth | `POST api/auth/facebook` | `AuthApi.facebook` | `AuthRepository.loginWithFacebook` | `LoginResponseDto` |
| Auth | `POST api/auth/facebook/oidc` | `AuthApi.facebookOidc` | `AuthRepository.loginWithFacebookOidc` | `LoginResponseDto` |
| Auth | `GET api/auth/me` | `AuthApi.me` | `AuthRepository.getCurrentAuthIdentity` | `AuthIdentityDto` |
| Users | `GET api/users/me` | `UsersApi.getCurrentUser` | `UserRepository.getCurrentUser` | `UserProfileDto` |
| Users | `PUT api/users/me` | `UsersApi.updateCurrentUser` | `UserRepository.updateCurrentUser` | `UserProfileDto` |
| Places | `GET api/places/search?q&focusLat&focusLon` | `PlacesApi.search` | `PlacesRepository.searchPlaces` | `List<DestinationSearchResultDto>` |
| Routing | `GET api/test/jeepney/nearby?lat&lon` | `RoutingApi.nearby` | `RoutingRepository.findNearbyRoutes` | `List<NearbyJeepneyRouteDto>` |
| Routing | `GET api/test/jeepney/plan?originLat&originLon&destinationLat&destinationLon` | `RoutingApi.plan` | `RoutingRepository.planTrip` | `List<JeepneyTripPlanDto>` → `List<JourneyPlan>` |
| Trips | `POST api/trips` | `TripsApi.start` | `TripRepository.startTrip` | `PassengerTripDetailsDto` |
| Trips | `GET api/trips/{tripId}` | `TripsApi.get` | `TripRepository.getTrip` | `PassengerTripDetailsDto` |
| Trips | `GET api/trips/{tripId}/alerts` | `TripsApi.alerts` | `TripRepository.getTripAlerts` | `List<TripAlertDto>` |
| TripSessions | `POST api/tripsessions` | `TripSessionsApi.create` | `TripSessionRepository.create` | `TripSessionDto` |
| TripSessions | `GET api/tripsessions/{id}` | `TripSessionsApi.get` | `TripSessionRepository.get` | `TripSessionDto` |
| TripSessions | `GET api/tripsessions/active` | `TripSessionsApi.active` | `TripSessionRepository.getActive` | `TripSessionDto` |
| TripSessions | `POST .../{id}/start`, `cancel`, `boarding-confirmed`, `alighting-confirmed` | corresponding `TripSessionsApi` method | corresponding `TripSessionRepository` method | `TripSessionDto` |
| TripSessions | `GET api/tripsessions/{id}/instructions` | `TripSessionsApi.instructions` | `TripSessionRepository.getInstructions` | `List<NavigationInstructionDto>` |
| TripSessions | `POST api/tripsessions/{id}/location` | `TripSessionsApi.location` | `TripSessionRepository.updateLocation` | `LocationUpdateResultDto` |
| TripSessions | `POST api/tripsessions/{id}/reroute` | `TripSessionsApi.reroute` | `TripSessionRepository.reroute` | `RerouteResultDto` |
| TransportRoutes | `GET api/transport-routes` | `TransportRoutesApi.activeRoutes` | `TransportRouteRepository.getActiveRoutes` | `List<TransportRouteListItemDto>` |
| TransportRoutes | `GET api/transport-routes/latest/polyline` | `TransportRoutesApi.latestPolyline` | `TransportRouteRepository.getLatestPolyline` | `TransportRoutePolylineDto` |
| TransportRoutes | `GET api/transport-routes/{routeId}/points` | `TransportRoutesApi.points` | `TransportRouteRepository.getRoutePoints` | `RoutePointsResponseDto` |
| TransportRoutes | `POST api/transport-routes`, `PUT .../{routeId}/points` | `TransportRoutesApi.create`, `replacePoints` | intentionally not exposed by passenger repository | `CreatedTransportRouteDto`, `RoutePointsResponseDto` |
| TricyclePoints | `GET api/tricycle-points/{id}` | `TricyclePointsApi.get` | `TricycleRepository.getPoint` | `TricyclePointResponseDto` |
| TricyclePoints | `POST api/tricycle-points` | `TricyclePointsApi.create` | intentionally not exposed by passenger repository | `TricyclePointResponseDto` |
| RideMatching | `POST/GET api/ride-matching/requests...`, `GET api/ride-matching/matches/{id}` | corresponding `RideMatchingApi` method | passenger request/get methods on `RideMatchingRepository` | `RideRequestDetailsDto` / `RideMatchDetailsDto` |
| RideMatching | `POST api/ride-matching/requests/{id}/match` | `RideMatchingApi.createMatch` | intentionally not exposed by passenger repository | `RideMatchDetailsDto` |
| RideMatching | `POST api/ride-matching/matches/{id}/accept`, `reject`, `cancel` | corresponding `RideMatchingApi` method | corresponding `RideMatchingRepository` method | `204 Unit` |
| Drivers | `GET api/drivers/{id}`, `vehicle`, `availability` | corresponding `DriversApi` method | corresponding `DriverRepository` method | driver detail DTOs |
| Drivers | `POST .../availability/start`, `stop`; `PUT .../location` | corresponding `DriversApi` method | corresponding `DriverRepository` method | session DTO / `204 Unit` / location DTO |
| AI | `POST api/AI/ask` | `AiApi.ask` | `AiRepository.ask` | `AssistantResponseDto` |
| Health | `GET health` | `HealthApi.getHealth` | `HealthService.check` | `HealthResponseDto` |
| Trips | `GET api/trips/recent` | `TripsApi.recent` | `TripRepository.getRecentJourneys` | `List<PassengerTripHistoryItemDto>` |

`X-Api-Key` is attached centrally using the header name returned by login/register. The authentication scheme is metadata and is not sent as a Bearer prefix. ASP.NET default numeric enum values are retained as `Int` on the wire and converted through fallback-safe UI/domain helpers. Guest mode is represented by the absence of a valid stored credential.

Not represented: `ValhallaTestController` and the anonymous `/test` route, because they are diagnostics and no Android production feature uses them. Chat service/database types are also omitted because `dev` exposes no chat controller.
