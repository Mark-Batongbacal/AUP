# Android API contract

Source checked: backend `dev` at `971f4b1`. All paths below are relative to `BuildConfig.BACKEND_BASE_URL`.

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

`X-Api-Key` is attached centrally using the header name returned by login/register. The authentication scheme is metadata and is not sent as a Bearer prefix. ASP.NET default numeric enum values are retained as `Int` on the wire and converted through fallback-safe UI/domain helpers.

Not represented: `ValhallaTestController` and the anonymous `/test` route, because they are diagnostics and no Android production feature uses them. Chat service/database types are also omitted because `dev` exposes no chat controller.
