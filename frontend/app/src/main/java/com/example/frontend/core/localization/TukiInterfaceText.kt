package com.example.frontend.core.localization

object TukiInterfaceText {
    private fun pick(english: String, filipino: String): String =
        if (AppLanguagePreference.isFilipino()) filipino else english

    val home: String get() = pick("Home", "Home")
    val recent: String get() = pick("Recent", "Recent")
    val favorites: String get() = pick("Favorites", "Favorites")
    val profile: String get() = pick("Profile", "Profile")
    val settings: String get() = pick("Settings", "Settings")

    val whereToToday: String get() = pick("Where to today?", "Saan ka patungo?")
    val planTripOrAskAi: String get() = pick(
        "Plan your trip or ask our AI for the best way to go.",
        "Planuhin ang biyahe mo o magtanong sa AI para sa pinakamainam na ruta."
    )
    val currentLocation: String get() = pick("Current location", "Kasalukuyang lokasyon")
    val currentArea: String get() = pick("Current area", "Kasalukuyang lugar")
    val tapToChange: String get() = pick("Tap to change", "I-tap para baguhin")
    val destination: String get() = pick("Destination", "Destinasyon")
    val whereAreYouGoing: String get() = pick("Where are you going?", "Saan ka pupunta?")
    val searchOrEnterPlace: String get() = pick("Search or enter a place", "Maghanap o maglagay ng lugar")
    val findRoutes: String get() = pick("Find Routes", "Maghanap ng Ruta")
    val recentPlaces: String get() = pick("Recent places", "Mga kamakailang lugar")
    val viewAll: String get() = pick("View all", "Tingnan lahat")
    val tripInProgress: String get() = pick("TRIP IN PROGRESS", "MAY BIYAHENG KASALUKUYAN")
    val resume: String get() = pick("Resume", "Ipagpatuloy")

    val recentTrips: String get() = pick("Recent Trips", "Mga Kamakailang Biyahe")
    val all: String get() = pick("All", "Lahat")
    val completed: String get() = pick("Completed", "Natapos")
    val cancelled: String get() = pick("Cancelled", "Kinansela")
    val waitingToBoard: String get() = pick("WaitingToBoard", "Naghihintay Sumakay")
    val noTripsYet: String get() = pick("No trips in this category yet.", "Wala pang biyahe sa kategoryang ito.")

    val saveFavoriteRoutes: String get() = pick(
        "Save your favorite routes\nfor quick access",
        "I-save ang paborito mong ruta\npara madaling balikan"
    )
    val howToAddFavorites: String get() = pick("How to add favorites?", "Paano magdagdag sa Favorites?")
    val tapStarToSave: String get() = pick(
        "Tap the star on any route to save it here.",
        "I-tap ang bituin sa anumang ruta para i-save ito rito."
    )

    val routeDetails: String get() = pick("Route Details", "Detalye ng Ruta")
    val stepByStepGuide: String get() = "Step-by-step guide"
    val startTrip: String get() = "Start Trip"
    val endTrip: String get() = "End Trip"
    val fullRoute: String get() = pick("Full route", "Buong ruta")
    val yourCompleteRoute: String get() = pick("Your complete route", "Buong ruta mo")
    val selectedTravelSegment: String get() = pick("Selected travel segment", "Napiling bahagi ng biyahe")
    val tapStepInspect: String get() = pick("Tap a step to inspect its route", "I-tap ang hakbang para makita ang ruta")
    val walkTo: String get() = pick("Walk to", "Maglakad papunta sa")
    val rideTricycle: String get() = pick("Ride Tricycle", "Sumakay ng Tricycle")
    val rideJeepney: String get() = pick("Ride Jeepney", "Sumakay ng Jeepney")

    val language: String get() = pick("Language", "Wika")
    val selectLanguage: String get() = pick("SELECT LANGUAGE", "PUMILI NG WIKA")
    val save: String get() = pick("Save", "I-save")
    val account: String get() = pick("ACCOUNT", "ACCOUNT")
    val editProfile: String get() = pick("Edit Profile", "I-edit ang Profile")
    val privacySecurity: String get() = pick("Privacy & Security", "Privacy at Security")
    val appearancePreferences: String get() = pick("Appearance and app preferences", "Itsura at mga preference ng app")
    val logOut: String get() = "Log out"

    val logIn: String get() = "Log in"
    val continueAsGuest: String get() = "Continue as Guest"
    val welcomeBack: String get() = pick("Welcome back", "Maligayang pagbabalik")
    val loginSubtitle: String get() = pick("Log in to continue your commute", "Mag-log in para ipagpatuloy ang biyahe")
    val email: String get() = pick("Email", "Email")
    val password: String get() = pick("Password", "Password")
    val forgotPassword: String get() = pick("Forgot password?", "Nakalimutan ang password?")
    val newToTuki: String get() = pick("New to Tuki?", "Bago sa TUKI?")
    val signUp: String get() = pick("Sign up", "Sign up")
    val createAccount: String get() = pick("Create an account", "Gumawa ng account")
    val fullName: String get() = pick("Full Name", "Buong Pangalan")
    val sendOtp: String get() = pick("Send OTP", "Ipadala ang OTP")
    val verifyOtp: String get() = pick("Verify OTP", "I-verify ang OTP")
    val resetPassword: String get() = pick("Reset Password", "I-reset ang Password")
    val changePassword: String get() = pick("Change password", "Palitan ang password")

    fun status(raw: String): String = when {
        !AppLanguagePreference.isFilipino() -> raw
        raw.equals("Completed", true) -> completed
        raw.equals("Cancelled", true) -> cancelled
        raw.equals("WaitingToBoard", true) -> waitingToBoard
        else -> raw
    }
}
