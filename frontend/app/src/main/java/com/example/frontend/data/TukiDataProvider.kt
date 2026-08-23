package com.example.frontend.data

import android.content.Context
import com.example.frontend.core.network.ApiClient
import com.example.frontend.core.network.ApiErrorParser
import com.example.frontend.core.storage.AuthSessionStore
import com.example.frontend.core.storage.SharedPreferencesAuthSessionStore
import com.example.frontend.data.ai.AiApi
import com.example.frontend.data.ai.AiRepository
import com.example.frontend.data.ai.AiRepositoryImpl
import com.example.frontend.data.auth.AuthApi
import com.example.frontend.data.auth.AuthRepository
import com.example.frontend.data.auth.AuthRepositoryImpl
import com.example.frontend.data.drivers.DriverRepository
import com.example.frontend.data.drivers.DriverRepositoryImpl
import com.example.frontend.data.drivers.DriversApi
import com.example.frontend.data.favorites.FavoritesApi
import com.example.frontend.data.favorites.FavoritesRepository
import com.example.frontend.data.favorites.FavoritesRepositoryImpl
import com.example.frontend.data.health.HealthApi
import com.example.frontend.data.health.HealthService
import com.example.frontend.data.navigation.NavigationApi
import com.example.frontend.data.navigation.NavigationRepository
import com.example.frontend.data.navigation.NavigationRepositoryImpl
import com.example.frontend.data.navigation.SharedPreferencesNavigationLocalStore
import com.example.frontend.data.places.PlacesApi
import com.example.frontend.data.places.PlacesRepository
import com.example.frontend.data.places.PlacesRepositoryImpl
import com.example.frontend.data.ridematching.RideMatchingApi
import com.example.frontend.data.ridematching.RideMatchingRepository
import com.example.frontend.data.ridematching.RideMatchingRepositoryImpl
import com.example.frontend.data.routing.RoutingApi
import com.example.frontend.data.routing.RoutingRepository
import com.example.frontend.data.routing.RoutingRepositoryImpl
import com.example.frontend.data.transport.TransportRouteRepository
import com.example.frontend.data.transport.TransportRouteRepositoryImpl
import com.example.frontend.data.transport.TransportRoutesApi
import com.example.frontend.data.tricycle.TricyclePointsApi
import com.example.frontend.data.tricycle.TricycleRepository
import com.example.frontend.data.tricycle.TricycleRepositoryImpl
import com.example.frontend.data.trips.TripRepository
import com.example.frontend.data.trips.TripRepositoryImpl
import com.example.frontend.data.trips.TripsApi
import com.example.frontend.data.tripsessions.TripSessionRepository
import com.example.frontend.data.tripsessions.TripSessionRepositoryImpl
import com.example.frontend.data.tripsessions.TripSessionsApi
import com.example.frontend.data.users.UserRepository
import com.example.frontend.data.users.UserRepositoryImpl
import com.example.frontend.data.users.UsersApi

class TukiDataProvider(
    context: Context,
    val sessionStore: AuthSessionStore = SharedPreferencesAuthSessionStore(context)
) {
    private val client = ApiClient(sessionStore)
    private val errors = ApiErrorParser(client.gson)
    private fun <T> api(type: Class<T>): T = client.create(type)

    private val authApi = api(AuthApi::class.java)
    private val usersApi = api(UsersApi::class.java)
    private val navigationLocalStore = SharedPreferencesNavigationLocalStore(
        context = context,
        sessions = sessionStore,
        gson = client.gson
    )

    val authRepository: AuthRepository = AuthRepositoryImpl(authApi, usersApi, sessionStore, errors)
    val userRepository: UserRepository = UserRepositoryImpl(usersApi, sessionStore, errors)
    val placesRepository: PlacesRepository = PlacesRepositoryImpl(api(PlacesApi::class.java), sessionStore, errors)
    val routingRepository: RoutingRepository = RoutingRepositoryImpl(api(RoutingApi::class.java), sessionStore, errors)
    val tripRepository: TripRepository = TripRepositoryImpl(api(TripsApi::class.java), sessionStore, errors)
    val tripSessionRepository: TripSessionRepository = TripSessionRepositoryImpl(api(TripSessionsApi::class.java), sessionStore, errors)
    val navigationRepository: NavigationRepository = NavigationRepositoryImpl(
        api(NavigationApi::class.java),
        sessionStore,
        errors,
        navigationLocalStore
    )
    val transportRouteRepository: TransportRouteRepository = TransportRouteRepositoryImpl(api(TransportRoutesApi::class.java), sessionStore, errors)
    val tricycleRepository: TricycleRepository = TricycleRepositoryImpl(api(TricyclePointsApi::class.java), sessionStore, errors)
    val rideMatchingRepository: RideMatchingRepository = RideMatchingRepositoryImpl(api(RideMatchingApi::class.java), sessionStore, errors)
    val driverRepository: DriverRepository = DriverRepositoryImpl(api(DriversApi::class.java), sessionStore, errors)
    val aiRepository: AiRepository = AiRepositoryImpl(api(AiApi::class.java), sessionStore, errors)
    val favoritesRepository: FavoritesRepository = FavoritesRepositoryImpl(api(FavoritesApi::class.java), sessionStore, errors)
    val healthService = HealthService(api(HealthApi::class.java), errors)
}
