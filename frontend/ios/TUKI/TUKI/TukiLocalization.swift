import Combine
import Foundation

/// Runtime-observable language flag, mirroring Android's `AppLanguagePreference`
/// (core/localization/AppLanguagePreference.kt): flipping this updates every
/// `TukiInterfaceText` string everywhere it's read, without an app relaunch.
final class TukiLanguagePreference: ObservableObject {
    static let shared = TukiLanguagePreference()

    private static let defaultsKey = "tuki.language"
    private let defaults: UserDefaults

    @Published private(set) var currentLanguage: String

    init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        currentLanguage = Self.normalize(defaults.string(forKey: Self.defaultsKey))
    }

    func update(_ language: String?) {
        let normalized = Self.normalize(language)
        currentLanguage = normalized
        defaults.set(normalized, forKey: Self.defaultsKey)
    }

    func isFilipino() -> Bool { currentLanguage == "Filipino" }

    static func normalize(_ language: String?) -> String {
        let value = language?.trimmingCharacters(in: .whitespaces).lowercased()
        if value == "filipino" || value == "tagalog" || (value?.hasPrefix("fil-") ?? false) {
            return "Filipino"
        }
        return "English"
    }
}

/// Ported 1:1 from Android's `core/localization/TukiInterfaceText.kt` — identical property
/// names and identical English/Filipino wording, so both platforms show the same copy.
/// Add entries here as each screen is rebuilt for parity; don't hardcode new user-facing
/// strings directly in views.
enum TukiInterfaceText {
    static var isFilipino: Bool { TukiLanguagePreference.shared.isFilipino() }

    private static func pick(_ english: String, _ filipino: String) -> String {
        isFilipino ? filipino : english
    }

    static var home: String { "Home" }
    static var recent: String { "Recent" }
    static var favorites: String { "Favorites" }
    static var profile: String { "Profile" }
    static var settings: String { "Settings" }

    static var hello: String { pick("Hello", "Kamusta") }
    static var whereToToday: String { pick("Where to today?", "Saan ka patungo?") }
    static var planTripOrAskAi: String {
        pick(
            "Plan your trip or ask our AI for the best way to go.",
            "Planuhin ang biyahe mo o magtanong sa AI para sa pinakamainam na ruta."
        )
    }
    static var currentLocation: String { pick("Current location", "Kasalukuyang lokasyon") }
    static var currentLocationUpper: String { pick("CURRENT LOCATION", "KASALUKUYANG LOKASYON") }
    static var currentArea: String { pick("Current area", "Kasalukuyang lugar") }
    static var locatingYou: String { pick("Locating you...", "Hinahanap ang lokasyon mo...") }
    static var locationPermissionDenied: String {
        pick("Location permission denied", "Hindi pinayagan ang access sa lokasyon")
    }
    static var unableToDetectLocation: String {
        pick("Unable to detect location", "Hindi matukoy ang lokasyon")
    }
    static var tapToChange: String { pick("Tap to change", "I-tap para baguhin") }
    static var tapToChangeMultiline: String { pick("Tap to\nchange", "I-tap para\nbaguhin") }
    static var destination: String { pick("Destination", "Destinasyon") }
    static var destinationUpper: String { pick("DESTINATION", "DESTINASYON") }
    static var pickupUpper: String { pick("PICKUP", "PICKUP") }
    static var whereAreYouGoing: String { pick("Where are you going?", "Saan ka pupunta?") }
    static var searchOrEnterPlace: String { pick("Search or enter a place", "Maghanap o maglagay ng lugar") }
    static var tapToChangeDestination: String {
        pick("Tap to change destination", "I-tap para baguhin ang destinasyon")
    }
    static var findRoutes: String { pick("Find Routes", "Maghanap ng Ruta") }
    static var recentPlaces: String { pick("Recent places", "Mga kamakailang lugar") }
    static var viewAll: String { pick("View all", "Tingnan lahat") }
    static var findingRecentPlaces: String {
        pick("Finding your recent places...", "Hinahanap ang mga kamakailang lugar mo...")
    }
    static var addShortcut: String { pick("Add\nshortcut", "Magdagdag\nshortcut") }
    static var startJourneyWithTuki: String {
        pick("Start your journey with TUKI", "Simulan ang biyahe mo gamit ang TUKI")
    }
    static var pickDestinationRecentAppear: String {
        pick(
            "Pick a destination and your recent places will appear here.",
            "Pumili ng destinasyon at lalabas dito ang mga kamakailang lugar mo."
        )
    }
    static var askTukiAi: String { pick("Ask TUKI AI", "Magtanong sa TUKI AI") }
    static var letAiFindBestWay: String {
        pick("Let AI find the best way to go.", "Hayaan ang AI na hanapin ang pinakamainam na ruta.")
    }
    static var newLabel: String { pick("NEW", "BAGO") }
    static var tripInProgress: String { pick("TRIP IN PROGRESS", "MAY BIYAHENG KASALUKUYAN") }
    static var resume: String { pick("Resume", "Ipagpatuloy") }

    static var searchLocation: String { pick("Search location...", "Maghanap ng lokasyon...") }
    static var searchingNearbyPlaces: String {
        pick("Searching nearby places...", "Naghahanap ng malalapit na lugar...")
    }
    static var searchingMorePlaces: String {
        pick("Searching more places...", "Naghahanap pa ng mga lugar...")
    }
    static var morePlaces: String { pick("More places...", "Iba pang lugar...") }
    static var pickupPoint: String { pick("Pick-up point", "Pickup point") }
    static var tapMapOrSearchPlace: String {
        pick("Tap the map or search for a place", "I-tap ang mapa o maghanap ng lugar")
    }
    static var moveMapThenDone: String {
        pick("Move around the map, then press Done.", "Igalaw ang mapa, pagkatapos ay pindutin ang Done.")
    }
    static var done: String { "Done" }

    static var pickOrigin: String { pick("Pick origin", "Pumili ng pinanggalingan") }
    static var pickDestination: String { pick("Pick destination", "Pumili ng destinasyon") }
    static var pinnedOrigin: String { pick("Pinned origin", "Napiling pinanggalingan") }
    static var pinnedDestination: String { pick("Pinned destination", "Napiling destinasyon") }
    static var tapMapChooseOrigin: String {
        pick("Tap the map to choose your origin", "I-tap ang mapa para pumili ng pinanggalingan")
    }
    static var tapMapChooseDestination: String {
        pick("Tap the map to choose a destination", "I-tap ang mapa para pumili ng destinasyon")
    }
    static var useThisOrigin: String { pick("Use This Origin", "Gamitin ang Pinanggalingang Ito") }
    static var useThisDestination: String { pick("Use This Destination", "Gamitin ang Destinasyong Ito") }
    static var setPickupDestinationSubtitle: String {
        pick(
            "Set your pickup and destination in one place, then TUKI will find your best commute options.",
            "Itakda ang pickup at destinasyon mo, pagkatapos ay hahanapin ng TUKI ang pinakamainam na commute options."
        )
    }
    static var currentLocationOrPickup: String {
        pick("Current location or pickup", "Kasalukuyang lokasyon o pickup")
    }
    static var useCurrent: String { pick("Use current", "Gamitin ang kasalukuyan") }
    static var pickOnMap: String { pick("Pick on map", "Pumili sa mapa") }
    static var searchingPickup: String { pick("Searching pickup...", "Naghahanap ng pickup...") }
    static var map: String { "Map" }
    static var searchingPlaces: String { pick("Searching places...", "Naghahanap ng mga lugar...") }
    static var destinationPickupTip: String {
        pick(
            "Tip: choose pickup first if you are not starting from your current location.",
            "Tip: piliin muna ang pickup kung hindi ka magsisimula sa kasalukuyan mong lokasyon."
        )
    }
    static var waitingForPickup: String { pick("Waiting for pickup...", "Naghihintay ng pickup...") }

    static var recentTrips: String { pick("Recent Trips", "Mga Kamakailang Biyahe") }
    static var all: String { pick("All", "Lahat") }
    static var completed: String { pick("Completed", "Natapos") }
    static var cancelled: String { pick("Cancelled", "Kinansela") }
    static var waitingToBoard: String { pick("WaitingToBoard", "Naghihintay Sumakay") }
    static var noTripsYet: String { pick("No trips in this category yet.", "Wala pang biyahe sa kategoryang ito.") }
    static var signInToViewJourneys: String {
        pick("Sign in to view your recent journeys.", "Mag-sign in para makita ang mga kamakailang biyahe mo.")
    }

    static var saveFavoriteRoutes: String {
        pick(
            "Save your favorite routes\nfor quick access",
            "I-save ang paborito mong ruta\npara madaling balikan"
        )
    }
    static var howToAddFavorites: String { pick("How to add favorites?", "Paano magdagdag sa Favorites?") }
    static var tapStarToSave: String {
        pick(
            "Tap the star on any route to save it here.",
            "I-tap ang bituin sa anumang ruta para i-save ito rito."
        )
    }
    static var noFavoriteRoutes: String { pick("No favorite routes yet.", "Wala pang naka-save na favorite route.") }
    static var signInFavorites: String {
        pick(
            "Sign in to save and view your favorite routes.",
            "Mag-sign in para mag-save at makita ang favorite routes mo."
        )
    }

    static var routeDetails: String { pick("Route Details", "Detalye ng Ruta") }
    static var stepByStepGuide: String { "Step-by-step guide" }
    static var startTrip: String { "Start Trip" }
    static var endTrip: String { "End Trip" }
    static var fullRoute: String { pick("Full route", "Buong ruta") }
    static var yourCompleteRoute: String { pick("Your complete route", "Buong ruta mo") }
    static var selectedTravelSegment: String { pick("Selected travel segment", "Napiling bahagi ng biyahe") }
    static var tapStepInspect: String { pick("Tap a step to inspect its route", "I-tap ang hakbang para makita ang ruta") }
    static var walkTo: String { pick("Walk to", "Maglakad papunta sa") }
    static var rideTricycle: String { pick("Ride Tricycle", "Sumakay ng Tricycle") }
    static var rideJeepney: String { pick("Ride Jeepney", "Sumakay ng Jeepney") }
    static var tipPrepareFare: String {
        pick(
            "Tip: Prepare exact fare or have small bills for a smoother ride.",
            "Tip: Maghanda ng eksaktong pamasahe o maliliit na pera para mas maayos ang biyahe."
        )
    }

    static var language: String { pick("Language", "Wika") }
    static var selectLanguage: String { pick("SELECT LANGUAGE", "PUMILI NG WIKA") }
    static var save: String { pick("Save", "I-save") }
    static var account: String { "ACCOUNT" }
    static var appearance: String { pick("APPEARANCE", "ITSURA") }
    static var support: String { pick("SUPPORT", "SUPPORT") }
    static var editProfile: String { pick("Edit Profile", "I-edit ang Profile") }
    static var privacySecurity: String { pick("Privacy & Security", "Privacy at Security") }
    static var appearancePreferences: String {
        pick("Appearance and app preferences", "Itsura at mga preference ng app")
    }
    static var darkMode: String { pick("Dark Mode", "Dark Mode") }
    static var darkModeSubtitle: String { pick("Switch between light and dark theme", "Palitan ang light at dark theme") }
    static var helpCenter: String { pick("Help Center", "Help Center") }
    static var sendFeedback: String { pick("Send Feedback", "Magpadala ng Feedback") }
    static var aboutTuki: String { pick("About TUKI", "Tungkol sa TUKI") }
    static var logOut: String { "Log out" }

    static var logIn: String { "Log in" }
    static var continueAsGuest: String { "Continue as Guest" }
    static var welcomeBack: String { pick("Welcome back", "Maligayang pagbabalik") }
    static var loginSubtitle: String { pick("Log in to continue your commute", "Mag-log in para ipagpatuloy ang biyahe") }
    static var email: String { "Email" }
    static var password: String { "Password" }
    static var forgotPassword: String { pick("Forgot password?", "Nakalimutan ang password?") }
    static var newToTuki: String { pick("New to Tuki?", "Bago sa TUKI?") }
    static var signUp: String { "Sign up" }
    static var createAccount: String { pick("Create an account", "Gumawa ng account") }
    static var fullName: String { pick("Full Name", "Buong Pangalan") }
    static var sendOtp: String { pick("Send OTP", "Ipadala ang OTP") }
    static var verifyOtp: String { pick("Verify OTP", "I-verify ang OTP") }
    static var resetPassword: String { pick("Reset Password", "I-reset ang Password") }
    static var changePassword: String { pick("Change password", "Palitan ang password") }
    static var checkYourEmail: String { pick("Check your email", "Tingnan ang email mo") }
    static var newPassword: String { pick("New password", "Bagong password") }
    static var confirmNewPassword: String { pick("Confirm new password", "Kumpirmahin ang bagong password") }

    static func status(_ raw: String) -> String {
        guard isFilipino else { return raw }
        if raw.caseInsensitiveCompare("Completed") == .orderedSame { return completed }
        if raw.caseInsensitiveCompare("Cancelled") == .orderedSame { return cancelled }
        if raw.caseInsensitiveCompare("WaitingToBoard") == .orderedSame { return waitingToBoard }
        return raw
    }
}
