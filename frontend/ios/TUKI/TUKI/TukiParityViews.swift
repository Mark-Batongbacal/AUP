import CoreLocation
import MapKit
import SwiftUI

struct TukiParityRootView: View {
    @StateObject private var auth = AuthViewModel()
    @State private var entry: Entry = .onboarding

    private enum Entry {
        case onboarding, login, signup, forgotPassword
    }

    var body: some View {
        Group {
            if auth.canEnterApp {
                TukiParityMainView(auth: auth)
            } else {
                switch entry {
                case .onboarding:
                    ParityOnboarding { entry = .login }
                case .login:
                    ParityLogin(
                        auth: auth,
                        onSignUp: { entry = .signup },
                        onForgotPassword: { entry = .forgotPassword },
                        onGuest: { auth.continueAsGuest() }
                    )
                case .signup:
                    ParitySignup(auth: auth) { entry = .login }
                case .forgotPassword:
                    ParityForgotPassword { entry = .login }
                }
            }
        }
        .preferredColorScheme(.light)
    }
}

private struct ParityOnboarding: View {
    let onContinue: () -> Void

    var body: some View {
        ZStack {
            TukiPalette.teal.ignoresSafeArea()
            VStack(spacing: 18) {
                Spacer()
                Image("TukiLogo")
                    .resizable()
                    .scaledToFit()
                    .frame(width: 170, height: 170)
                Text("TUKI.")
                    .font(.system(size: 46, weight: .heavy))
                    .foregroundStyle(.white)
                Text("Commute smarter.\nMove easier.")
                    .font(.system(size: 21))
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.white)
                Spacer()
                Button(action: onContinue) {
                    Text("Let's Ride")
                        .font(.system(size: 25, weight: .bold))
                        .foregroundStyle(.white)
                        .frame(maxWidth: .infinity)
                        .frame(height: 72)
                        .background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 22))
                }
                .buttonStyle(.plain)
            }
            .padding(.horizontal, 34)
            .padding(.bottom, 45)
        }
    }
}

private struct ParityLogin: View {
    @ObservedObject var auth: AuthViewModel
    let onSignUp: () -> Void
    let onForgotPassword: () -> Void
    let onGuest: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Image("TukiLogo").resizable().scaledToFit().frame(width: 50, height: 50)
                    Text("TUKI.").font(.system(size: 30, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                }
                Text("Welcome back").font(.system(size: 24, weight: .heavy)).padding(.top, 20)
                Text("Log in to continue your commute")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundStyle(TukiPalette.gray)
                    .padding(.top, 4)

                VStack(spacing: 12) {
                    TukiFormField(label: "Email", text: $auth.userName, keyboardType: .emailAddress, textContentType: .username)
                    TukiFormField(label: "Password", text: $auth.password, isSecure: true, textContentType: .password)
                    Button("Forgot password?", action: onForgotPassword)
                        .font(.system(size: 17, weight: .bold))
                        .foregroundStyle(TukiPalette.teal)
                        .frame(maxWidth: .infinity, alignment: .trailing)
                        .buttonStyle(.plain)
                }
                .padding(.top, 25)

                if let error = auth.errorMessage {
                    Text(error)
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundStyle(TukiPalette.error)
                        .multilineTextAlignment(.center)
                        .padding(.top, 12)
                }

                TukiPrimaryButton(
                    title: auth.isAuthenticating ? "Logging in..." : "Log in",
                    isLoading: auth.isAuthenticating,
                    isEnabled: !auth.isAuthenticating,
                    action: auth.loginWithPassword
                )
                .padding(.top, 20)

                HStack(spacing: 14) {
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                    Text("OR").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.gray)
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                }
                .padding(.vertical, 15)

                HStack(spacing: 12) {
                    ParitySocialButton(title: "Google", image: "GoogleLogo", enabled: !auth.isAuthenticating, action: auth.loginWithGoogle)
                    ParitySocialButton(title: "Facebook", image: "FacebookLogo", enabled: !auth.isAuthenticating, action: auth.loginWithFacebook)
                }

                Button(action: onGuest) {
                    Text("Continue as Guest")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(TukiPalette.dark)
                        .frame(maxWidth: .infinity)
                        .frame(height: 56)
                        .overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
                }
                .buttonStyle(.plain)
                .disabled(auth.isAuthenticating)
                .padding(.top, 12)

                HStack(spacing: 0) {
                    Text("New to Tuki? ").foregroundStyle(TukiPalette.gray)
                    Button("Sign up", action: onSignUp).foregroundStyle(TukiPalette.orange).fontWeight(.bold).buttonStyle(.plain)
                }
                .font(.system(size: 17, weight: .semibold))
                .padding(.top, 12)
            }
            .padding(.horizontal, 34)
            .padding(.top, 25)
            .padding(.bottom, 20)
        }
        .background(.white)
    }
}

private struct ParitySocialButton: View {
    let title: String
    let image: String
    let enabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Image(image).resizable().scaledToFit().frame(width: 20, height: 20)
                Text(title).font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
            }
            .frame(maxWidth: .infinity)
            .frame(height: 56)
            .overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
        .opacity(enabled ? 1 : 0.6)
    }
}

private struct ParitySignup: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void
    @State private var fullName = ""
    @State private var email = ""
    @State private var password = ""
    @State private var confirmation = ""
    @State private var localError: String?

    var body: some View {
        ScrollView {
            VStack(spacing: 12) {
                TukiLogoHeader(logoSize: 75, titleSize: 32)
                Text("Create an account").font(.system(size: 26, weight: .heavy))
                Text("Start your seamless commute today").foregroundStyle(TukiPalette.gray)
                TukiCompactFormField(label: "Full Name", text: $fullName)
                TukiCompactFormField(label: "Email", text: $email, keyboardType: .emailAddress)
                TukiCompactFormField(label: "Password", text: $password, isSecure: true)
                TukiCompactFormField(label: "Confirm Password", text: $confirmation, isSecure: true)
                if let error = localError ?? auth.errorMessage {
                    Text(error).font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.error)
                }
                TukiPrimaryButton(title: "Sign up", isLoading: auth.isAuthenticating, isEnabled: !auth.isAuthenticating) {
                    guard fullName.split(whereSeparator: { $0.isWhitespace }).count >= 2 else { localError = "Enter both your first and last name."; return }
                    guard email.contains("@") else { localError = "Enter a valid email address."; return }
                    guard password.count >= 8 else { localError = "Password must be at least 8 characters."; return }
                    guard password == confirmation else { localError = "Passwords do not match."; return }
                    localError = nil
                    Task { _ = await auth.register(fullName: fullName, email: email, password: password) }
                }
                Button("Already have an account? Log in", action: onBack)
                    .foregroundStyle(TukiPalette.orange)
                    .fontWeight(.bold)
                    .buttonStyle(.plain)
            }
            .padding(28)
        }
    }
}

private struct ParityForgotPassword: View {
    let onBack: () -> Void
    @State private var email = ""
    @State private var sent = false

    var body: some View {
        VStack(spacing: 20) {
            HStack { Button("‹", action: onBack); Spacer() }.font(.system(size: 22, weight: .bold))
            TukiLogoHeader()
            Text("Reset Password").font(.system(size: 26, weight: .heavy))
            Text("Enter your email to receive a reset link").foregroundStyle(TukiPalette.gray)
            TukiFormField(label: "Email", text: $email, keyboardType: .emailAddress)
            if sent { Text("Reset link sent! Check your inbox.").foregroundStyle(TukiPalette.teal).fontWeight(.semibold) }
            TukiPrimaryButton(title: sent ? "Sent!" : "Send Reset Link", isEnabled: !sent) { sent = true }
            Spacer()
        }
        .padding(34)
    }
}

private enum ParityTab: CaseIterable {
    case home, recent, favorites, profile
    var label: String { switch self { case .home: "Home"; case .recent: "Recent"; case .favorites: "Favorites"; case .profile: "Profile" } }
    var image: String { switch self { case .home: "HomeIcon"; case .recent: "RecentIcon"; case .favorites: "FavoriteIcon"; case .profile: "ProfileIcon" } }
}

private enum ParityScreen {
    case tabs
    case destination(String, CLLocationCoordinate2D?)
    case ai
    case routes(String, CLLocationCoordinate2D, TukiPlace)
    case detail(String, CLLocationCoordinate2D, TukiPlace, TukiRouteChoice)
    case tracking(String, TukiPlace, TukiRouteChoice, TukiNavigationSnapshot, Bool)
    case commute(RecentCommute)
}

private struct TukiParityMainView: View {
    @ObservedObject var auth: AuthViewModel
    @StateObject private var location = TukiLocationService()
    @State private var tab: ParityTab = .home
    @State private var screen: ParityScreen = .tabs
    @State private var recent: [RecentCommute] = []
    @State private var favorites: [FavoriteRoute] = []
    @State private var currentLabel = "Locating you..."

    private let api: TukiPlatformAPI?
    private let historyAPI: TukiHistoryAPI?

    init(auth: AuthViewModel) {
        self.auth = auth
        let store = KeychainTukiCredentialStore()
        if let configuration = try? AppConfiguration.load() {
            api = TukiPlatformAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
            historyAPI = TukiHistoryAPI(baseURL: configuration.backendBaseURL, credentialStore: store)
        } else {
            api = nil
            historyAPI = nil
        }
    }

    var body: some View {
        Group {
            switch screen {
            case .tabs:
                tabView
            case .destination(let originName, let coordinate):
                ParityDestinationSearch(api: api, location: location, initialOriginName: originName, initialOrigin: coordinate) { screen = .tabs } onFind: { name, origin, destination in
                    screen = .routes(name, origin, destination)
                }
            case .ai:
                ParityAIChat(userName: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User")) { screen = .tabs } onDestination: { name in
                    Task { await openAIRoute(name) }
                }
            case .routes(let originName, let origin, let destination):
                ParityRouteResults(api: api, originName: originName, origin: origin, destination: destination) { screen = .tabs } onSelect: { choice in
                    screen = .detail(originName, origin, destination, choice)
                }
            case .detail(let originName, let origin, let destination, let choice):
                ParityRouteDetail(api: api, auth: auth, originName: originName, destination: destination, choice: choice) {
                    screen = .routes(originName, origin, destination)
                } onStarted: { snapshot, guest in
                    screen = .tracking(originName, destination, choice, snapshot, guest)
                } onEnded: {
                    screen = .tabs
                    tab = .home
                }
            case .tracking(let originName, let destination, let choice, let snapshot, let guest):
                ParityTracking(api: api, location: location, originName: originName, destination: destination, choice: choice, initialSnapshot: snapshot, isGuest: guest) {
                    screen = .tabs
                    tab = .home
                }
            case .commute(let commute):
                ParityCommuteDetail(commute: commute) { screen = .tabs }
            }
        }
        .task { await refreshLocation() }
    }

    private var tabView: some View {
        VStack(spacing: 0) {
            Group {
                switch tab {
                case .home:
                    ParityHome(name: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User"), currentLabel: currentLabel) {
                        screen = .destination(currentLabel, location.currentLocation?.coordinate)
                    } onAI: {
                        screen = .ai
                    }
                case .recent:
                    ParityRecent(commutes: recent, guest: auth.isGuest) { screen = .commute($0) }
                case .favorites:
                    ParityFavorites(routes: favorites, guest: auth.isGuest)
                case .profile:
                    ParityProfile(auth: auth)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            ParityBottomBar(tab: $tab)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .task(id: tab) { await refreshTab() }
    }

    private func refreshLocation() async {
        guard let current = await location.requestCurrentLocation() else {
            currentLabel = location.errorMessage ?? "Unable to detect location"
            return
        }
        if let api, case .success(let place) = await api.reverseGeocode(lat: current.coordinate.latitude, lon: current.coordinate.longitude) {
            currentLabel = place.name
        } else {
            currentLabel = "Current location"
        }
    }

    private func refreshTab() async {
        guard !auth.isGuest else { recent = []; favorites = []; return }
        if tab == .recent, let historyAPI {
            if case .success(let values) = await historyAPI.history() { recent = values }
        } else if tab == .favorites, let historyAPI {
            if case .success(let values) = await historyAPI.favorites() { favorites = values }
        } else if tab == .profile {
            _ = await auth.refreshProfile()
        }
    }

    private func openAIRoute(_ name: String) async {
        guard let api, let current = await location.requestCurrentLocation() else { return }
        if case .success(let places) = await api.searchPlaces(name, focusLat: current.coordinate.latitude, focusLon: current.coordinate.longitude), let place = places.first {
            screen = .routes(currentLabel, current.coordinate, place)
        }
    }
}

private struct ParityBottomBar: View {
    @Binding var tab: ParityTab
    var body: some View {
        HStack {
            ForEach(ParityTab.allCases, id: \.self) { item in
                Button { tab = item } label: {
                    VStack(spacing: 4) {
                        Image(item.image).renderingMode(.template).resizable().scaledToFit().frame(width: 24, height: 24)
                        Text(item.label).font(.system(size: 12, weight: .semibold))
                    }
                    .foregroundStyle(tab == item ? TukiPalette.teal : TukiPalette.gray)
                    .frame(maxWidth: .infinity)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.vertical, 12)
        .background(.white)
    }
}

private struct ParityHome: View {
    let name: String
    let currentLabel: String
    let onPin: () -> Void
    let onAI: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Hello, \(name) 👋").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray)
            Text("Where are you going?").font(.system(size: 25, weight: .heavy)).foregroundStyle(TukiPalette.dark)
            Text("Pick a destination yourself, or tell our AI where you want to go.").font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
            Text("●  \(currentLabel) (current location)")
                .font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                .padding(16).frame(maxWidth: .infinity, alignment: .leading)
                .background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
            ParityActionCard(title: "Pin your destination", subtitle: "Search or drop a pin on the map if you already know where you're headed.", actionTitle: "🗺️ Open map", action: onPin)
            ParityActionCard(title: "Ask our AI", subtitle: "Describe where you want to go and we'll figure out the location and commute.", actionTitle: "✨ Ask AI", action: onAI)
            Spacer()
        }
        .padding(24)
        .background(TukiPalette.cream)
    }
}

private struct ParityActionCard: View {
    let title: String
    let subtitle: String
    let actionTitle: String
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 12) {
                Text(title).font(.system(size: 17, weight: .bold))
                Text(subtitle).font(.system(size: 13)).opacity(0.75)
                Text(actionTitle).font(.system(size: 14, weight: .bold)).frame(maxWidth: .infinity).padding(14).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14))
            }
            .foregroundStyle(.white)
            .padding(18)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(TukiPalette.dark)
            .clipShape(RoundedRectangle(cornerRadius: 18))
        }
        .buttonStyle(.plain)
    }
}

private struct ParityRecent: View {
    let commutes: [RecentCommute]
    let guest: Bool
    let onTap: (RecentCommute) -> Void
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text("Recent").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                if commutes.isEmpty {
                    Text(guest ? "Sign in to view your recent journeys." : "No completed or cancelled trips yet.").foregroundStyle(TukiPalette.gray)
                }
                ForEach(commutes) { commute in
                    Button { onTap(commute) } label: {
                        VStack(alignment: .leading, spacing: 4) {
                            Text("\(commute.origin) to \(commute.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
                            Text("\(commute.status) · \(commute.legs) legs · \(commute.minutes) min").font(.system(size: 13)).foregroundStyle(TukiPalette.teal)
                        }
                        .padding(16).frame(maxWidth: .infinity, alignment: .leading)
                        .background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
    }
}

private struct ParityFavorites: View {
    let routes: [FavoriteRoute]
    let guest: Bool
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 12) {
                Text("Favorites").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                if routes.isEmpty { Text(guest ? "Sign in to save favorite routes." : "No favorite routes yet.").foregroundStyle(TukiPalette.gray) }
                ForEach(routes) { route in
                    VStack(alignment: .leading) {
                        Text("\(route.origin) to \(route.destination)").font(.system(size: 17, weight: .bold))
                        Text("Used \(route.timesUsed) times · \(route.note)").font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                    }
                    .padding(16).frame(maxWidth: .infinity, alignment: .leading)
                    .background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16))
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
    }
}

private struct ParityProfile: View {
    @ObservedObject var auth: AuthViewModel
    @State private var editing = false
    @State private var fullName = ""
    @State private var phone = ""

    var body: some View {
        ScrollView {
            VStack(spacing: 16) {
                let profile = auth.currentUserProfile
                let displayName = auth.isGuest ? "Guest" : (profile?.displayName ?? "User")
                Text(String(displayName.prefix(1))).font(.system(size: 34, weight: .heavy)).foregroundStyle(.white).frame(width: 90, height: 90).background(TukiPalette.teal).clipShape(Circle())
                Text(displayName).font(.system(size: 21, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text(auth.isGuest ? "Guest mode" : (profile?.email ?? "")).foregroundStyle(TukiPalette.gray)
                HStack {
                    ParityStat(value: "\(profile?.tripsTaken ?? 0)", label: "TRIPS TAKEN")
                    ParityStat(value: "\(profile?.favoritesCount ?? 0)", label: "FAVORITES")
                }
                Button("Edit Profile") {
                    fullName = profile?.displayName ?? ""
                    phone = profile?.phoneNumber ?? ""
                    editing = true
                }.buttonStyle(.borderedProminent).tint(TukiPalette.teal)
                Button("Sign Out") { auth.signOut() }.foregroundStyle(TukiPalette.orange).fontWeight(.bold)
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
        .sheet(isPresented: $editing) {
            NavigationStack {
                Form {
                    TextField("Full name", text: $fullName)
                    TextField("Phone", text: $phone)
                }
                .navigationTitle("Edit profile")
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) { Button("Cancel") { editing = false } }
                    ToolbarItem(placement: .confirmationAction) {
                        Button("Save") {
                            Task {
                                if case .success = await auth.updateProfile(fullName: fullName, phone: phone) { editing = false }
                            }
                        }
                    }
                }
            }
        }
    }
}

private struct ParityStat: View {
    let value: String
    let label: String
    var body: some View {
        VStack { Text(value).font(.system(size: 22, weight: .heavy)); Text(label).font(.system(size: 11)).foregroundStyle(TukiPalette.gray) }
            .frame(maxWidth: .infinity).padding(16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
    }
}

private struct ParityDestinationSearch: View {
    let api: TukiPlatformAPI?
    @ObservedObject var location: TukiLocationService
    let initialOriginName: String
    let initialOrigin: CLLocationCoordinate2D?
    let onBack: () -> Void
    let onFind: (String, CLLocationCoordinate2D, TukiPlace) -> Void

    @State private var destinationText = ""
    @State private var results: [TukiPlace] = []
    @State private var selected: TukiPlace?
    @State private var unsupported = false

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Button("← Back", action: onBack).fontWeight(.bold).foregroundStyle(TukiPalette.teal)
            Text("Where are you going?").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark)
            Text(initialOriginName).padding(14).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
            TextField("Type or search a place", text: $destinationText)
                .padding(14).background(.white).clipShape(RoundedRectangle(cornerRadius: 14))
            ScrollView {
                LazyVStack(spacing: 8) {
                    ForEach(results) { place in
                        Button {
                            selected = place
                            destinationText = place.name
                            unsupported = !TukiServiceArea.contains(latitude: place.latitude, longitude: place.longitude)
                        } label: {
                            VStack(alignment: .leading) {
                                Text(place.name).fontWeight(.bold)
                                if let address = place.address { Text(address).font(.system(size: 12)).foregroundStyle(TukiPalette.gray) }
                            }
                            .frame(maxWidth: .infinity, alignment: .leading).padding(12).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12))
                        }
                        .buttonStyle(.plain)
                    }
                }
            }
            Button("Find Routes") {
                if let selected {
                    let origin = initialOrigin ?? location.currentLocation?.coordinate ?? CLLocationCoordinate2D(latitude: 15.145, longitude: 120.59)
                    onFind(initialOriginName, origin, selected)
                }
            }
            .disabled(selected == nil)
            .fontWeight(.bold).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 50)
            .background(TukiPalette.orange.opacity(selected == nil ? 0.4 : 1)).clipShape(RoundedRectangle(cornerRadius: 14))
        }
        .padding(24)
        .background(TukiPalette.cream.ignoresSafeArea())
        .task(id: destinationText) { await search() }
        .alert(TukiServiceArea.title, isPresented: $unsupported) { Button("OK", role: .cancel) {} } message: { Text(TukiServiceArea.message) }
    }

    private func search() async {
        let query = destinationText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard query.count >= 2, selected?.name != query, let api else { results = []; return }
        try? await Task.sleep(for: .milliseconds(300))
        guard !Task.isCancelled else { return }
        if case .success(let values) = await api.searchPlaces(query, focusLat: initialOrigin?.latitude, focusLon: initialOrigin?.longitude) {
            results = Array(values.prefix(6))
        }
    }
}

private struct ParityRouteResults: View {
    let api: TukiPlatformAPI?
    let originName: String
    let origin: CLLocationCoordinate2D
    let destination: TukiPlace
    let onBack: () -> Void
    let onSelect: (TukiRouteChoice) -> Void
    @State private var routes: [TukiRouteChoice] = []
    @State private var loading = true
    @State private var error: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Button("← Back", action: onBack).foregroundStyle(TukiPalette.teal).fontWeight(.bold)
                Text("Route options").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text("\(originName) → \(destination.name)").foregroundStyle(TukiPalette.gray)
                if loading { ProgressView("Finding routes...").padding(.vertical, 30) }
                if let error { Text(error).foregroundStyle(TukiPalette.error) }
                ForEach(routes) { route in
                    Button { onSelect(route) } label: {
                        VStack(alignment: .leading, spacing: 8) {
                            Text(route.isRecommended ? "⭐ \(route.label)" : route.label).font(.system(size: 18, weight: .bold))
                            Text("~\(route.totalMinutes) min · ₱\(Int(route.totalFare)) · \(route.steps.count) legs")
                            Text("Walk \(route.walkMeters) m · \(route.transfers) transfers")
                                .font(.system(size: 12)).opacity(0.7)
                        }
                        .foregroundStyle(.white).padding(18).frame(maxWidth: .infinity, alignment: .leading)
                        .background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18))
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(24)
        }
        .background(TukiPalette.cream)
        .task { await load() }
    }

    private func load() async {
        guard let api else { error = "Routing is not configured."; loading = false; return }
        switch await api.plan(originName: originName, originLat: origin.latitude, originLon: origin.longitude, destination: destination) {
        case .success(let values): routes = values
        case .failure(let value): error = value.message
        }
        loading = false
    }
}

private struct ParityRouteDetail: View {
    let api: TukiPlatformAPI?
    @ObservedObject var auth: AuthViewModel
    let originName: String
    let destination: TukiPlace
    let choice: TukiRouteChoice
    let onBack: () -> Void
    let onStarted: (TukiNavigationSnapshot, Bool) -> Void
    let onEnded: () -> Void
    @State private var working = false
    @State private var error: String?
    @State private var active: TukiNavigationSnapshot?

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Button("← Back", action: onBack).fontWeight(.bold).foregroundStyle(TukiPalette.teal)
            Text("Route Details").font(.system(size: 24, weight: .heavy))
            Text("\(originName) → \(destination.name)")
            ScrollView {
                LazyVStack(spacing: 12) {
                    ForEach(Array(choice.steps.enumerated()), id: \.offset) { _, step in
                        Text("\(step.mode) to \(step.to) · \(step.minutes) mins")
                            .frame(maxWidth: .infinity, alignment: .leading).padding(16).background(.white).clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                }
            }
            if let error { Text(error).foregroundStyle(TukiPalette.error) }
            if let active {
                Button("Resume Active Trip") { onStarted(active, false) }.buttonStyle(.borderedProminent).tint(TukiPalette.teal)
                Button("End Active Trip") { Task { await endActive() } }.foregroundStyle(TukiPalette.orange)
            }
            TukiPrimaryButton(title: working ? "Working..." : "Start Trip", isLoading: working, isEnabled: !working && active == nil) { Task { await start() } }
        }
        .padding(24)
        .background(TukiPalette.cream.ignoresSafeArea())
    }

    private func start() async {
        guard !working else { return }
        working = true
        defer { working = false }
        if auth.isGuest {
            onStarted(guestSnapshot(), true)
            return
        }
        guard let api else { error = "Navigation is not configured."; return }
        switch await api.startNavigation(recommendationId: choice.id) {
        case .success(let snapshot): onStarted(snapshot, false)
        case .failure(let value):
            if value.message.localizedCaseInsensitiveContains("active trip"), case .success(let snapshot) = await api.activeNavigation() {
                active = snapshot
                error = "You already have an active trip. Resume it or end it before starting this route."
            } else { error = value.message }
        }
    }

    private func endActive() async {
        guard let api, let active else { return }
        if case .success = await api.cancel(sessionId: active.sessionId) { onEnded() }
    }

    private func guestSnapshot() -> TukiNavigationSnapshot {
        let first = choice.steps.first
        let end = choice.legEndPoints.first
        return TukiNavigationSnapshot(
            sessionId: "guest-\(UUID().uuidString)", state: "GuestActive", currentLegIndex: 0,
            currentLeg: first.map { TukiNavigationLeg(legIndex: 0, transportMode: $0.mode.uppercased(), routeName: nil, fromName: $0.from, toName: $0.to, startLatitude: nil, startLongitude: nil, endLatitude: end?.latitude, endLongitude: end?.longitude, distanceMeters: nil, fare: $0.fare ?? 0) },
            nextInstruction: first.map { TukiNavigationInstruction(type: "Continue", routeName: nil, transportMode: $0.mode.uppercased(), distanceMeters: nil, requiresConfirmation: false) },
            spokenInstruction: first.map { "Take \($0.mode) toward \($0.to)" }, remainingDistanceMeters: nil, progressMeters: 0,
            boardInfo: nil, alightInfo: nil, landmark: nil, requiresBoardingConfirmation: false,
            requiresAlightingConfirmation: first?.mode.lowercased() == "jeepney", rerouteRequired: false,
            status: "Guest navigation", triggeredEvents: []
        )
    }
}

private struct ParityTracking: View {
    let api: TukiPlatformAPI?
    @ObservedObject var location: TukiLocationService
    let originName: String
    let destination: TukiPlace
    let choice: TukiRouteChoice
    let initialSnapshot: TukiNavigationSnapshot
    let isGuest: Bool
    let onEnded: () -> Void

    @State private var snapshot: TukiNavigationSnapshot
    @State private var error: String?
    @State private var working = false
    @State private var showExit = false
    @State private var showParaPo = false

    init(api: TukiPlatformAPI?, location: TukiLocationService, originName: String, destination: TukiPlace, choice: TukiRouteChoice, initialSnapshot: TukiNavigationSnapshot, isGuest: Bool, onEnded: @escaping () -> Void) {
        self.api = api
        self.location = location
        self.originName = originName
        self.destination = destination
        self.choice = choice
        self.initialSnapshot = initialSnapshot
        self.isGuest = isGuest
        self.onEnded = onEnded
        _snapshot = State(initialValue: initialSnapshot)
    }

    var body: some View {
        ZStack {
            ParityRouteMap(destination: choice.legEndPoints.last)
            VStack {
                HStack {
                    Button("‹") { showExit = true }.font(.system(size: 24, weight: .bold)).buttonStyle(.plain)
                    VStack(alignment: .leading) {
                        Text("Current Trip").font(.system(size: 13, weight: .bold)).foregroundStyle(TukiPalette.gray)
                        Text("\(originName) → \(destination.name)").font(.system(size: 16, weight: .heavy))
                    }
                    Spacer()
                }
                .padding(20).background(.white).clipShape(RoundedRectangle(cornerRadius: 20)).padding(24)
                Spacer()
                VStack(alignment: .leading, spacing: 12) {
                    Text("NEXT STEP").font(.system(size: 12, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                    Text(snapshot.displayInstruction).font(.system(size: 19, weight: .heavy))
                    Text(snapshot.remainingDistanceMeters.map { "\(Int($0.rounded())) m remaining" } ?? "Waiting for location update").foregroundStyle(TukiPalette.gray)
                    if let error { Text(error).foregroundStyle(TukiPalette.error).font(.system(size: 12)) }
                    HStack {
                        if snapshot.requiresBoardingConfirmation || snapshot.requiresAlightingConfirmation {
                            Button(snapshot.requiresBoardingConfirmation ? "Confirm Board" : "Confirm Alight") { Task { await confirm() } }
                                .buttonStyle(.borderedProminent).tint(snapshot.requiresBoardingConfirmation ? TukiPalette.teal : TukiPalette.orange)
                        }
                        Spacer()
                        Button("🔔") { showParaPo = true }.font(.system(size: 28)).disabled(!canParaPo)
                    }
                    ProgressView(value: progress).tint(TukiPalette.teal)
                }
                .padding(24).background(.white).clipShape(RoundedRectangle(cornerRadius: 24)).shadow(radius: 8).padding(20)
            }
        }
        .alert("Trip is still active", isPresented: $showExit) {
            Button("Continue Trip", role: .cancel) {}
            Button("End Trip", role: .destructive) { Task { await endTrip() } }
        } message: {
            Text("Going back will not end the navigation session. Continue the trip or end it first?")
        }
        .alert("Para po!", isPresented: $showParaPo) {
            Button("OK", role: .cancel) {}
        } message: {
            Text("Get ready to alight at your stop.")
        }
        .task(id: snapshot.sessionId) { await poll() }
    }

    private var canParaPo: Bool {
        snapshot.requiresAlightingConfirmation || snapshot.nextInstruction?.type.localizedCaseInsensitiveContains("alight") == true
    }

    private var progress: Double {
        guard let distance = snapshot.currentLeg?.distanceMeters, distance > 0 else { return 0 }
        return min(max(snapshot.progressMeters / distance, 0), 1)
    }

    private func endTrip() async {
        if isGuest { onEnded(); return }
        guard let api else { return }
        if case .success = await api.cancel(sessionId: snapshot.sessionId) { onEnded() }
    }

    private func confirm() async {
        guard !working, !isGuest, let api else { return }
        working = true
        defer { working = false }
        let result = snapshot.requiresBoardingConfirmation
            ? await api.board(sessionId: snapshot.sessionId)
            : await api.alight(sessionId: snapshot.sessionId)
        switch result {
        case .success(let value): snapshot = value; error = nil
        case .failure(let value): error = value.message
        }
    }

    private func poll() async {
        guard !isGuest, let api else { return }
        while !Task.isCancelled {
            if snapshot.state.lowercased() == "arrived" || snapshot.state.lowercased() == "cancelled" { return }
            if let current = await location.requestCurrentLocation() {
                let update = TukiNavigationLocationUpdate(
                    latitude: current.coordinate.latitude,
                    longitude: current.coordinate.longitude,
                    accuracyMeters: current.horizontalAccuracy,
                    timestamp: ISO8601DateFormatter().string(from: current.timestamp),
                    speedMetersPerSecond: current.speed >= 0 ? current.speed : nil,
                    bearingDegrees: current.course >= 0 ? current.course : nil
                )
                switch await api.updateLocation(sessionId: snapshot.sessionId, update: update) {
                case .success(let value): snapshot = value; error = nil
                case .failure(let value): error = value.message
                }
            }
            try? await Task.sleep(for: .seconds(5))
        }
    }
}

private struct ParityRouteMap: View {
    let destination: TukiCoordinate?
    var body: some View {
        Map {
            if let destination {
                Marker("Destination", coordinate: CLLocationCoordinate2D(latitude: destination.latitude, longitude: destination.longitude))
                    .tint(.orange)
            }
        }
        .ignoresSafeArea()
    }
}

private struct ParityCommuteDetail: View {
    let commute: RecentCommute
    let back: () -> Void
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Button("← Back", action: back).foregroundStyle(TukiPalette.teal).fontWeight(.bold)
                Text("\(commute.origin) → \(commute.destination)").font(.system(size: 24, weight: .heavy))
                Text("\(commute.legs) legs · \(commute.minutes) min total").foregroundStyle(TukiPalette.teal)
                ForEach(Array(commute.steps.enumerated()), id: \.offset) { _, step in
                    Text("\(step.mode): \(step.from) → \(step.to) · \(step.minutes) min")
                        .padding(14).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
                }
            }
            .padding(30)
        }
        .background(TukiPalette.cream)
    }
}

private struct ParityAIChat: View {
    let userName: String
    let onBack: () -> Void
    let onDestination: (String) -> Void
    @State private var messages: [AIMessage] = []
    @State private var input = ""
    @State private var thinking = false

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Button("←", action: onBack).font(.system(size: 24, weight: .bold))
                Text("Ask our AI").font(.system(size: 20, weight: .heavy))
                Spacer()
            }.padding(18)
            ScrollView {
                LazyVStack(spacing: 12) {
                    ForEach(messages) { message in
                        VStack(alignment: message.user ? .trailing : .leading, spacing: 8) {
                            Text(message.text).foregroundStyle(.white).padding(12)
                                .background(message.user ? TukiPalette.orange : TukiPalette.dark)
                                .clipShape(RoundedRectangle(cornerRadius: 16))
                            if message.place {
                                Button("📍 Jollibee SM Clark — Yes, that's it") { onDestination("Jollibee SM Clark") }
                                    .foregroundStyle(.white).padding(12).background(TukiPalette.teal).clipShape(RoundedRectangle(cornerRadius: 14))
                            }
                        }.frame(maxWidth: .infinity, alignment: message.user ? .trailing : .leading)
                    }
                    if thinking { ProgressView("Thinking…") }
                }.padding(16)
            }
            HStack {
                TextField("Type your message...", text: $input).textFieldStyle(.roundedBorder)
                Button("➤") { send(input) }.disabled(input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || thinking)
            }.padding(12).background(TukiPalette.dark)
        }
        .background(TukiPalette.cream)
        .onAppear {
            if messages.isEmpty {
                messages = [AIMessage(text: "Hi \(userName)! Where would you like to go? You can describe it in your own words.", user: false, place: false)]
            }
        }
    }

    private func send(_ text: String) {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty, !thinking else { return }
        messages.append(AIMessage(text: value, user: true, place: false))
        input = ""
        thinking = true
        Task {
            try? await Task.sleep(for: .milliseconds(700))
            messages.append(AIMessage(text: "Got it — found a Jollibee near SM Clark, Clark Freeport Zone. Is this the one?", user: false, place: true))
            thinking = false
        }
    }
}

private struct AIMessage: Identifiable {
    let id = UUID()
    let text: String
    let user: Bool
    let place: Bool
}
