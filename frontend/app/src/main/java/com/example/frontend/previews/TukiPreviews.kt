package com.example.frontend.previews

import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview
import com.example.frontend.model.RecentCommute
import com.example.frontend.screens.CommuteDetailScreen
import com.example.frontend.screens.FavoritesScreen
import com.example.frontend.screens.LoginScreen
import com.example.frontend.screens.OnboardingScreen
import com.example.frontend.screens.SignupScreen
import com.example.frontend.ui.theme.FrontendTheme
import com.example.frontend.screens.HomeScreen
import com.example.frontend.screens.ProfileScreen
import com.example.frontend.screens.RecentScreen
import com.example.frontend.screens.RouteResultsScreen
import com.example.frontend.screens.SettingsScreen
import com.example.frontend.screens.ForgotPasswordScreen
import com.example.frontend.screens.NavigationScreen
import com.example.frontend.screens.TripTrackingScreen
import com.example.frontend.TemporaryMapSamples

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Onboarding"
)
@Composable
fun OnboardingPreview() {
    FrontendTheme {
        OnboardingScreen(
            onLetsRideClick = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Login"
)
@Composable
fun LoginPreview() {
    FrontendTheme {
        LoginScreen(
            authRepository = PreviewMocks.authRepository,
            onBack = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "SignUp"
)
@Composable
fun SignUpPreview() {
    FrontendTheme {
        SignupScreen(
            authRepository = PreviewMocks.authRepository,
            onBack = {}
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Home"
)
@Composable
fun HomePreview() {
    FrontendTheme {
        HomeScreen(
            tripRepository = PreviewMocks.tripRepository,
            placesRepository = PreviewMocks.placesRepository
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Commute Detail"
)
@Composable
fun CommuteDetailPreview() {
    FrontendTheme {
        CommuteDetailScreen(
            commute = RecentCommute(
                id = "1",
                origin = "Sta. Rita",
                destination = "Guagua Town",
                legs = 3,
                minutes = 22
            )
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Route Results"
)
@Composable
fun RouteResultsPreview() {
    FrontendTheme {
        RouteResultsScreen(
            origin = "Brgy. Sta. Rita",
            destinationQuery = "Guagua Town",
            routingRepository = PreviewMocks.routingRepository,
            placesRepository = PreviewMocks.placesRepository
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Recent"
)
@Composable
fun RecentPreview() {
    FrontendTheme {
        RecentScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Favorites"
)
@Composable
fun FavoritesPreview() {
    FrontendTheme {
        FavoritesScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Profile"
)
@Composable
fun ProfilePreview() {
    FrontendTheme {
        ProfileScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Settings"
)
@Composable
fun SettingsPreview() {
    FrontendTheme {
        SettingsScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Forgot Password"
)
@Composable
fun ForgotPasswordPreview() {
    FrontendTheme {
        ForgotPasswordScreen()
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Navigation"
)
@Composable
fun NavigationPreview() {
    FrontendTheme {
        NavigationScreen(
            origin = "Sta. Rita",
            destination = "Guagua Town",
            steps = emptyList()
        )
    }
}

@Preview(
    showBackground = true,
    showSystemUi = true,
    name = "Trip Tracking"
)
@Composable
fun TripTrackingPreview() {
    FrontendTheme {
        TripTrackingScreen(
            origin = "Sta. Rita",
            destination = "Guagua Town",
            routePoints = TemporaryMapSamples.routePoints
        )
    }
}
