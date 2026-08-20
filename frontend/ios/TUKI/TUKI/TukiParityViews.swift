import SwiftUI
import MapKit

struct TukiParityRootView: View {
    @StateObject private var auth = AuthViewModel()
    @State private var entry = Entry.onboarding

    private enum Entry { case onboarding, login, signup, forgot }

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
                        onForgot: { entry = .forgot },
                        onGuest: { auth.continueAsGuest() }
                    )
                case .signup:
                    ParitySignup(auth: auth, onBack: { entry = .login })
                case .forgot:
                    ParityForgotPassword(onBack: { entry = .login }, onDone: { entry = .login })
                }
            }
        }
        .preferredColorScheme(.light)
    }
}

private struct ParityOnboarding: View {
    let onContinue: () -> Void
    @State private var raised = false

    var body: some View {
        ZStack {
            TukiPalette.teal.ignoresSafeArea()
            VStack(spacing: 0) {
                Spacer().frame(height: 170)
                Image("TukiLogo").resizable().scaledToFit().frame(width: 170, height: 170)
                    .offset(y: raised ? -14 : 0)
                    .onAppear {
                        withAnimation(.easeInOut(duration: 0.6).repeatForever(autoreverses: true)) { raised = true }
                    }
                Text("TUKI.").font(.system(size: 46, weight: .heavy)).foregroundStyle(.white).padding(.top, 5)
                Text("Commute smarter.").font(.system(size: 21)).foregroundStyle(.white).padding(.top, 38)
                Text("Move easier.").font(.system(size: 21)).foregroundStyle(.white)
                HStack(spacing: 8) {
                    Capsule().fill(TukiPalette.orange).frame(width: 30, height: 10)
                    Circle().fill(.white.opacity(0.3)).frame(width: 10, height: 10)
                    Circle().fill(.white.opacity(0.3)).frame(width: 10, height: 10)
                }.padding(.top, 28)
                Spacer().frame(height: 40)
                Button(action: onContinue) {
                    Text("Let's Ride").font(.system(size: 25, weight: .bold)).foregroundStyle(.white)
                        .frame(maxWidth: .infinity).frame(height: 84).background(TukiPalette.orange)
                        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                }.buttonStyle(.plain)
                Spacer(minLength: 0)
            }.padding(.horizontal, 34).padding(.bottom, 45)
        }
    }
}

private struct ParityLogin: View {
    @ObservedObject var auth: AuthViewModel
    let onSignUp: () -> Void
    let onForgot: () -> Void
    let onGuest: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Image("TukiLogo").resizable().scaledToFit().frame(width: 50, height: 50)
                    Text("TUKI.").font(.system(size: 30, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                }
                Text("Welcome back").font(.system(size: 24, weight: .heavy)).padding(.top, 20)
                Text("Log in to continue your commute").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 4)
                VStack(spacing: 10) {
                    TukiFormField(label: "Email", text: $auth.userName, keyboardType: .emailAddress, textContentType: .username)
                    TukiFormField(label: "Password", text: $auth.password, isSecure: true, textContentType: .password)
                    Button("Forgot password?", action: onForgot).font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.teal)
                        .frame(maxWidth: .infinity, alignment: .trailing).buttonStyle(.plain)
                }.padding(.top, 25)

                if let error = auth.errorMessage {
                    Text(error).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.error)
                        .multilineTextAlignment(.center).padding(.top, 12)
                }

                TukiPrimaryButton(title: auth.isAuthenticating ? "Logging in..." : "Log in", isLoading: auth.isAuthenticating, isEnabled: !auth.isAuthenticating, action: auth.loginWithPassword)
                    .padding(.top, 20)
                HStack(spacing: 14) {
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                    Text("OR").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.gray)
                    Rectangle().fill(Color.gray.opacity(0.35)).frame(height: 1)
                }.padding(.vertical, 15)
                HStack(spacing: 12) {
                    ParitySocialButton(title: "Google", image: "GoogleLogo", enabled: !auth.isAuthenticating, action: auth.loginWithGoogle)
                    ParitySocialButton(title: "Facebook", image: "FacebookLogo", enabled: !auth.isAuthenticating, action: auth.loginWithFacebook)
                }
                Button(action: onGuest) {
                    Text("Continue as Guest").font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark)
                        .frame(maxWidth: .infinity).frame(height: 56).background(.white)
                        .overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
                }.buttonStyle(.plain).disabled(auth.isAuthenticating).padding(.top, 12)
                HStack(spacing: 0) {
                    Text("New to Tuki? ").foregroundStyle(TukiPalette.gray).fontWeight(.semibold)
                    Button("Sign up", action: onSignUp).foregroundStyle(TukiPalette.orange).fontWeight(.bold).buttonStyle(.plain)
                }.font(.system(size: 17)).padding(.top, 8)
            }.padding(.horizontal, 34).padding(.top, 25).padding(.bottom, 15)
        }.background(.white).scrollDismissesKeyboard(.interactively)
    }
}

private struct ParitySocialButton: View {
    let title: String; let image: String; let enabled: Bool; let action: () -> Void
    var body: some View {
        Button(action: action) {
            HStack(spacing: 6) {
                Image(image).resizable().scaledToFit().frame(width: 20, height: 20)
                Text(title).font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
            }.frame(maxWidth: .infinity).frame(height: 56).background(.white)
                .overlay { RoundedRectangle(cornerRadius: 16).stroke(TukiPalette.border, lineWidth: 2) }
        }.buttonStyle(.plain).disabled(!enabled).opacity(enabled ? 1 : 0.65)
    }
}

private struct ParitySignup: View {
    @ObservedObject var auth: AuthViewModel
    let onBack: () -> Void
    @State private var fullName = ""; @State private var email = ""; @State private var password = ""; @State private var confirm = ""; @State private var localError: String?

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                TukiLogoHeader(logoSize: 75, titleSize: 32)
                Text("Create an account").font(.system(size: 26, weight: .heavy)).padding(.top, 16)
                Text("Start your seamless commute today").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 4)
                VStack(spacing: 10) {
                    TukiCompactFormField(label: "Full Name", text: $fullName)
                    TukiCompactFormField(label: "Email", text: $email, keyboardType: .emailAddress)
                    TukiCompactFormField(label: "Password", text: $password, isSecure: true)
                    TukiCompactFormField(label: "Confirm Password", text: $confirm, isSecure: true)
                }.padding(.top, 20)
                if let error = localError ?? auth.errorMessage { Text(error).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.error).padding(.top, 12) }
                TukiPrimaryButton(title: auth.isAuthenticating ? "Signing up..." : "Sign up", isLoading: auth.isAuthenticating, isEnabled: !auth.isAuthenticating) {
                    let name = fullName.trimmingCharacters(in: .whitespacesAndNewlines), mail = email.trimmingCharacters(in: .whitespacesAndNewlines)
                    guard name.split(whereSeparator: { $0.isWhitespace }).count >= 2 else { localError = "Enter both your first and last name."; return }
                    guard mail.contains("@") else { localError = "Enter a valid email address."; return }
                    guard password.count >= 8 else { localError = "Password must be at least 8 characters."; return }
                    guard password == confirm else { localError = "Passwords do not match."; return }
                    localError = nil
                    Task { _ = await auth.register(fullName: name, email: mail, password: password) }
                }.padding(.top, 20)
                HStack(spacing: 0) {
                    Text("Already have an account? ").foregroundStyle(TukiPalette.gray)
                    Button("Log in", action: onBack).foregroundStyle(TukiPalette.orange).fontWeight(.bold).buttonStyle(.plain)
                }.font(.system(size: 17)).padding(.vertical, 16)
            }.padding(.horizontal, 28).padding(.top, 20)
        }.background(.white)
    }
}

private struct ParityForgotPassword: View {
    let onBack: () -> Void; let onDone: () -> Void
    @State private var email = ""; @State private var sending = false; @State private var success = false; @State private var error: String?
    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                HStack { Button("‹", action: onBack).font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(width: 38, height: 38).background(TukiPalette.cream).clipShape(RoundedRectangle(cornerRadius: 12)); Spacer() }
                .buttonStyle(.plain)
                TukiLogoHeader().padding(.top, 20)
                Text("Reset Password").font(.system(size: 26, weight: .heavy)).padding(.top, 35)
                Text("Enter your email to receive a reset link").font(.system(size: 18, weight: .semibold)).foregroundStyle(TukiPalette.gray).multilineTextAlignment(.center).padding(.top, 8)
                TukiFormField(label: "Email", text: $email, keyboardType: .emailAddress).padding(.top, 40)
                if let error { Text(error).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.error).padding(.top, 12) }
                if success { Text("Reset link sent! Check your inbox.").font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.teal).padding(.top, 12) }
                TukiPrimaryButton(title: success ? "Sent!" : "Send Reset Link", isLoading: sending, isEnabled: !sending && !success) {
                    guard email.contains("@") else { error = "Enter a valid email address."; return }
                    error = nil; sending = true
                    Task {
                        try? await Task.sleep(for: .milliseconds(1500)); sending = false; success = true
                        try? await Task.sleep(for: .seconds(2)); onDone()
                    }
                }.padding(.top, 40)
            }.padding(.horizontal, 34).padding(.top, 35).padding(.bottom, 15)
        }.background(.white)
    }
}

private enum ParityTab: CaseIterable { case home, recent, favorites, profile
    var label: String { switch self { case .home: "Home"; case .recent: "Recent"; case .favorites: "Favorites"; case .profile: "Profile" } }
    var image: String { switch self { case .home: "HomeIcon"; case .recent: "RecentIcon"; case .favorites: "FavoriteIcon"; case .profile: "ProfileIcon" } }
}

private enum ParityScreen {
    case tabs
    case destination(originName: String, origin: CLLocationCoordinate2D?)
    case askAI
    case routes(originName: String, origin: CLLocationCoordinate2D, destination: TukiPlace)
    case routeDetail(originName: String, origin: CLLocationCoordinate2D, destination: TukiPlace, choice: TukiRouteChoice)
    case tracking(originName: String, destination: TukiPlace, choice: TukiRouteChoice, snapshot: TukiNavigationSnapshot, isGuest: Bool)
    case commute(RecentCommute)
}

private struct TukiParityMainView: View {
    @ObservedObject var auth: AuthViewModel
    @StateObject private var location = TukiLocationService()
    @State private var tab: ParityTab = .home
    @State private var screen: ParityScreen = .tabs
    @State private var recent: [RecentCommute] = []
    @State private var favorites: [FavoriteRoute] = []
    @State private var loadingRecent = false
    @State private var recentError: String?
    @State private var currentLabel = "Locating you..."

    private let api: TukiPlatformAPI?
    private let historyAPI: TukiHistoryAPI?

    init(auth: AuthViewModel) {
        self.auth = auth
        let store = KeychainTukiCredentialStore()
        if let config = try? AppConfiguration.load() {
            self.api = TukiPlatformAPI(baseURL: config.backendBaseURL, credentialStore: store)
            self.historyAPI = TukiHistoryAPI(baseURL: config.backendBaseURL, credentialStore: store)
        } else { self.api = nil; self.historyAPI = nil }
    }

    var body: some View {
        Group {
            switch screen {
            case .tabs: tabs
            case .destination(let name, let coordinate):
                ParityDestinationSearch(api: api, location: location, initialOriginName: name, initialOrigin: coordinate, onBack: { screen = .tabs }) { originName, origin, destination in
                    screen = .routes(originName: originName, origin: origin, destination: destination)
                }
            case .askAI:
                ParityAIChat(userName: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User"), onBack: { screen = .tabs }) { destinationName in
                    Task { await routeFromAI(destinationName) }
                }
            case .routes(let originName, let origin, let destination):
                ParityRouteResults(api: api, originName: originName, origin: origin, destination: destination, onBack: { screen = .tabs }) { choice in
                    screen = .routeDetail(originName: originName, origin: origin, destination: destination, choice: choice)
                }
            case .routeDetail(let originName, let origin, let destination, let choice):
                ParityRouteDetail(api: api, auth: auth, originName: originName, origin: origin, destination: destination, choice: choice, onBack: { screen = .routes(originName: originName, origin: origin, destination: destination) }) { snapshot, guest in
                    screen = .tracking(originName: originName, destination: destination, choice: choice, snapshot: snapshot, isGuest: guest)
                } onEnded: {
                    screen = .tabs; tab = .home
                }
            case .tracking(let originName, let destination, let choice, let snapshot, let guest):
                ParityTracking(api: api, location: location, originName: originName, destination: destination, choice: choice, initialSnapshot: snapshot, isGuest: guest, onBackToRoute: { screen = .routeDetail(originName: originName, origin: location.currentLocation?.coordinate ?? CLLocationCoordinate2D(), destination: destination, choice: choice) }) {
                    screen = .tabs; tab = .home
                }
            case .commute(let commute):
                ParityCommuteDetail(commute: commute) { screen = .tabs }
            }
        }
        .task { await updateCurrentLocationLabel() }
    }

    private var tabs: some View {
        VStack(spacing: 0) {
            Group {
                switch tab {
                case .home:
                    ParityHome(name: auth.currentUserProfile?.greetingName ?? (auth.isGuest ? "Guest" : "User"), currentLabel: currentLabel, locating: currentLabel == "Locating you...", onPin: {
                        screen = .destination(originName: currentLabel, origin: location.currentLocation?.coordinate)
                    }, onAI: { screen = .askAI })
                case .recent:
                    ParityRecent(commutes: recent, guest: auth.isGuest, loading: loadingRecent, error: recentError) { screen = .commute($0) }
                case .favorites:
                    ParityFavorites(routes: favorites, guest: auth.isGuest)
                case .profile:
                    ParityProfile(auth: auth)
                }
            }.frame(maxWidth: .infinity, maxHeight: .infinity)
            ParityBottomBar(tab: $tab)
        }.background(TukiPalette.cream.ignoresSafeArea())
            .task(id: tab) { await refreshTab() }
    }

    private func refreshTab() async {
        guard !auth.isGuest else { recent = []; favorites = []; return }
        if tab == .recent, let historyAPI {
            loadingRecent = true; recentError = nil
            switch await historyAPI.history() {
            case .success(let values): recent = values
            case .failure(.notAuthenticated): auth.signOut()
            case .failure(let error): recent = []; recentError = error.message
            }
            loadingRecent = false
        } else if tab == .favorites, let historyAPI {
            switch await historyAPI.favorites() {
            case .success(let values): favorites = values
            case .failure(.notAuthenticated): auth.signOut()
            case .failure: favorites = []
            }
        } else if tab == .profile { _ = await auth.refreshProfile() }
    }

    private func updateCurrentLocationLabel() async {
        guard let api, let value = await location.requestCurrentLocation() else { currentLabel = location.errorMessage ?? "Unable to detect location"; return }
        switch await api.reverseGeocode(lat: value.coordinate.latitude, lon: value.coordinate.longitude) {
        case .success(let place): currentLabel = place.name
        case .failure: currentLabel = "Current location"
        }
    }

    private func routeFromAI(_ destinationName: String) async {
        guard let api, let loc = await location.requestCurrentLocation() else { return }
        switch await api.searchPlaces(destinationName, focusLat: loc.coordinate.latitude, focusLon: loc.coordinate.longitude) {
        case .success(let places):
            if let place = places.first { screen = .routes(originName: currentLabel, origin: loc.coordinate, destination: place) }
        case .failure: break
        }
    }
}

private struct ParityBottomBar: View {
    @Binding var tab: ParityTab
    var body: some View {
        HStack(spacing: 0) {
            ForEach(ParityTab.allCases, id: \.self) { item in
                Button { tab = item } label: {
                    VStack(spacing: 4) {
                        Image(item.image).renderingMode(.template).resizable().scaledToFit().frame(width: 24, height: 24)
                        Text(item.label).font(.system(size: 12, weight: .semibold))
                    }.foregroundStyle(tab == item ? TukiPalette.teal : TukiPalette.gray).frame(maxWidth: .infinity)
                }.buttonStyle(.plain)
            }
        }.padding(.horizontal, 24).padding(.top, 14).padding(.bottom, 8).background(.white)
    }
}

private struct ParityHome: View {
    let name: String; let currentLabel: String; let locating: Bool; let onPin: () -> Void; let onAI: () -> Void
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("Hello, \(name) 👋").font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray).padding(.top, 24)
            Text("Where are you going?").font(.system(size: 25, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 4)
            Text("Pick a destination yourself, or tell our AI where you want to go.").font(.system(size: 12, weight: .medium)).foregroundStyle(TukiPalette.gray).padding(.top, 6)
            HStack(spacing: 12) {
                Circle().fill(TukiPalette.teal).frame(width: 11, height: 11)
                if locating { ProgressView().tint(TukiPalette.teal).scaleEffect(0.75) }
                Text(locating ? "Locating you..." : "\(currentLabel) (current location)").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                Spacer()
            }.padding(.horizontal, 16).padding(.vertical, 14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)).padding(.top, 14)
            ParityActionCard(kind: .pin, action: onPin).padding(.top, 12)
            ParityActionCard(kind: .ai, action: onAI).padding(.top, 12)
            Spacer()
        }.padding(.horizontal, 24).background(TukiPalette.cream)
    }
}

private struct ParityActionCard: View {
    enum Kind { case pin, ai }
    let kind: Kind; let action: () -> Void
    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 0) {
                HStack(spacing: 12) {
                    Text(kind == .pin ? "📍" : "✨").frame(width: 34, height: 34).background(.white.opacity(0.12)).clipShape(RoundedRectangle(cornerRadius: 10))
                    Text(kind == .pin ? "Pin your destination" : "Ask our AI").font(.system(size: 17, weight: .bold)).foregroundStyle(.white)
                    if kind == .ai { Text("NEW").font(.system(size: 10, weight: .bold)).foregroundStyle(.white).padding(.horizontal, 8).padding(.vertical, 3).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 8)) }
                }
                Text(kind == .pin ? "Search or drop a pin on the map if you already know where you're headed." : "Describe where you want to go and we'll figure out the location and commute.")
                    .font(.system(size: 13)).foregroundStyle(.white.opacity(0.75)).padding(.top, 10)
                HStack { Text(kind == .pin ? "🔍  Type or search a place" : "💬  \"Yung malapit sa SM Clark...\"").font(.system(size: 13)).foregroundStyle(.white.opacity(0.85)); Spacer() }
                    .padding(.horizontal, 14).frame(height: 48).background(kind == .pin ? .white.opacity(0.08) : TukiPalette.teal.opacity(0.35)).clipShape(RoundedRectangle(cornerRadius: 14)).padding(.top, 16)
                HStack { Spacer(); Text(kind == .pin ? "🗺️ Open map" : "✨ Ask AI").font(.system(size: 14, weight: kind == .ai ? .bold : .regular)).foregroundStyle(.white); Spacer() }
                    .frame(height: 48).background(kind == .pin ? .white.opacity(0.08) : TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14)).padding(.top, 10)
            }.padding(18).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18))
        }.buttonStyle(.plain)
    }
}

private struct ParityRecent: View {
    let commutes: [RecentCommute]; let guest: Bool; let loading: Bool; let error: String?; let onTap: (RecentCommute) -> Void
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Recent").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.bottom, 24)
                if loading { HStack { Spacer(); ProgressView().tint(TukiPalette.teal); Spacer() }.padding(.vertical, 48) }
                else if let error { Text(error).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.error) }
                else if commutes.isEmpty { Text(guest ? "Sign in to view your recent journeys." : "No completed or cancelled trips yet.").font(.system(size: 14)).foregroundStyle(TukiPalette.gray) }
                else {
                    ForEach(["Today", "Yesterday", "Earlier"], id: \.self) { section in
                        let values = commutes.filter { $0.dateGroup == section }
                        if !values.isEmpty {
                            Text(section.uppercased()).font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray).padding(.bottom, 10)
                            ForEach(values) { commute in Button { onTap(commute) } label: { ParityCommuteCard(commute: commute) }.buttonStyle(.plain).padding(.bottom, 12) }
                            Spacer().frame(height: 10)
                        }
                    }
                }
            }.padding(.horizontal, 30).padding(.vertical, 30)
        }.background(TukiPalette.cream)
    }
}

private struct ParityCommuteCard: View {
    let commute: RecentCommute
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("\(commute.origin) to \(commute.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark)
            Text(([commute.status.isEmpty ? nil : commute.status, "\(commute.legs) legs", "\(commute.minutes) min"].compactMap { $0 }).joined(separator: " · ")).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.teal)
            if commute.wasRerouted { Text(commute.rerouteCount > 1 ? "Rerouted \(commute.rerouteCount) times" : "Rerouted").font(.system(size: 12, weight: .semibold)).foregroundStyle(TukiPalette.gray) }
        }.frame(maxWidth: .infinity, alignment: .leading).padding(16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16))
    }
}

private struct ParityFavorites: View {
    let routes: [FavoriteRoute]; let guest: Bool
    var body: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                Text("Favorites").font(.system(size: 27, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.bottom, 24)
                Text("STARRED ROUTES").font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray).padding(.bottom, 10)
                if guest { Text("Sign in to save favorite routes.").font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.gray) }
                else if routes.isEmpty { Text("No favorite routes yet.").font(.system(size: 14)).foregroundStyle(TukiPalette.gray) }
                else { ForEach(routes) { route in HStack { VStack(alignment: .leading, spacing: 4) { Text("\(route.origin) to \(route.destination)").font(.system(size: 17, weight: .bold)).foregroundStyle(TukiPalette.dark); Text("Used \(route.timesUsed) times · \(route.note)").font(.system(size: 13)).foregroundStyle(TukiPalette.gray) }; Spacer(); Image("FavoriteIcon").resizable().scaledToFit().frame(width: 22, height: 22) }.padding(16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 16)).padding(.bottom, 12) } }
                VStack(alignment: .leading, spacing: 4) { Text("Tip").font(.system(size: 18, weight: .bold)); Text("Tap the star on any route to save it here").font(.system(size: 14)).opacity(0.85) }.foregroundStyle(.white).frame(maxWidth: .infinity, alignment: .leading).padding(20).background(TukiPalette.teal).clipShape(RoundedRectangle(cornerRadius: 18)).padding(.top, 10)
            }.padding(.horizontal, 30).padding(.vertical, 30)
        }.background(TukiPalette.cream)
    }
}

private enum ProfilePage { case overview, edit, privacy, password, language }
private struct ParityProfile: View {
    @ObservedObject var auth: AuthViewModel
    @State private var page: ProfilePage = .overview
    var body: some View {
        switch page {
        case .overview: overview
        case .edit: ParityEditProfile(auth: auth) { page = .overview }
        case .privacy: ParityPrivacy(auth: auth, onBack: { page = .overview }, onPassword: { page = .password })
        case .password: ParityPassword(auth: auth) { page = .privacy }
        case .language: ParityLanguage { page = .overview }
        }
    }
    private var overview: some View {
        let profile = auth.currentUserProfile
        let name = auth.isGuest ? "Guest" : (profile?.displayName ?? "User")
        let email = auth.isGuest ? "Guest mode" : (profile?.email ?? "")
        let initials = auth.isGuest ? "G" : name.split(separator: " ").prefix(2).compactMap(\.first).map(String.init).joined().uppercased()
        return ScrollView {
            LazyVStack(alignment: .leading, spacing: 0) {
                VStack(spacing: 0) {
                    Text(initials).font(.system(size: 30, weight: .heavy)).foregroundStyle(.white).frame(width: 90, height: 90).background(TukiPalette.teal).clipShape(Circle())
                    Text(name).font(.system(size: 21, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 14)
                    Text(email).font(.system(size: 15)).foregroundStyle(TukiPalette.gray).padding(.top, 4)
                }.frame(maxWidth: .infinity).padding(.bottom, 24)
                HStack(spacing: 12) { ParityStat(value: auth.isGuest ? "0" : "\(profile?.tripsTaken ?? 0)", label: "TRIPS TAKEN"); ParityStat(value: auth.isGuest ? "0" : "\(profile?.favoritesCount ?? 0)", label: "FAVORITES") }
                Text("ACCOUNT").font(.system(size: 14, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 28).padding(.bottom, 12)
                ParityAccountRow(image: "EditProfileIcon", title: "Edit Profile", subtitle: "Name, email, phone") { page = .edit }
                ParityAccountRow(image: "PrivacyIcon", title: "Privacy & Security", subtitle: "Password, data settings") { page = .privacy }.padding(.top, 12)
                ParityAccountRow(image: "LanguageIcon", title: "Language", subtitle: "English") { page = .language }.padding(.top, 12)
                Button("Sign Out") { auth.signOut() }.font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.orange).frame(maxWidth: .infinity).frame(height: 48).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)).buttonStyle(.plain).padding(.top, 12)
            }.padding(.horizontal, 30).padding(.vertical, 30)
        }.background(TukiPalette.cream)
    }
}

private struct ParityStat: View { let value: String; let label: String; var body: some View { VStack(spacing: 2) { Text(value).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark); Text(label).font(.system(size: 11, weight: .semibold)).foregroundStyle(TukiPalette.gray) }.frame(maxWidth: .infinity).padding(.vertical, 16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)) } }
private struct ParityAccountRow: View { let image: String; let title: String; let subtitle: String; let action: () -> Void; var body: some View { Button(action: action) { HStack(spacing: 14) { Image(image).resizable().scaledToFit().frame(width: 40, height: 40); VStack(alignment: .leading, spacing: 2) { Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark); Text(subtitle).font(.system(size: 13)).foregroundStyle(TukiPalette.gray) }; Spacer(); Text("›").font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.gray) }.padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)) }.buttonStyle(.plain) } }

private struct ParityHeader: View { let title: String; let back: () -> Void; var body: some View { HStack(spacing: 14) { Button("‹", action: back).font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(width: 38, height: 38).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12)).buttonStyle(.plain); Text(title).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark); Spacer() }.padding(.horizontal, 24).padding(.top, 20) } }

private struct ParityEditProfile: View {
    @ObservedObject var auth: AuthViewModel; let back: () -> Void
    @State private var name = ""; @State private var phone = ""; @State private var saving = false; @State private var error: String?
    var body: some View {
        VStack(spacing: 0) { ParityHeader(title: "Edit profile", back: back); ScrollView { VStack(spacing: 18) {
            let initials = name.split(separator: " ").prefix(2).compactMap(\.first).map(String.init).joined().uppercased()
            VStack { ZStack(alignment: .bottomTrailing) { Text(initials.isEmpty ? "?" : initials).font(.system(size: 34, weight: .heavy)).foregroundStyle(.white).frame(width: 100, height: 100).background(TukiPalette.teal).clipShape(Circle()); Text("📷").frame(width: 32, height: 32).background(TukiPalette.orange).clipShape(Circle()) }; Text("Change photo").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.teal).padding(.top, 10) }
            ParityField(label: "Full name", text: $name)
            VStack(alignment: .leading, spacing: 8) { Text("Email").font(.system(size: 14, weight: .semibold)); Text(auth.currentUserProfile?.email ?? "").foregroundStyle(TukiPalette.gray).frame(maxWidth: .infinity, alignment: .leading).padding(14).background(TukiPalette.creamCard.opacity(0.6)).clipShape(RoundedRectangle(cornerRadius: 14)); Text("Email is tied to your login and can't be changed here yet.").font(.system(size: 11)).foregroundStyle(TukiPalette.gray) }
            ParityField(label: "Phone", text: $phone)
            if let error { Text(error).foregroundStyle(TukiPalette.error).font(.system(size: 13, weight: .semibold)) }
            Button { guard !saving else { return }; saving = true; Task { switch await auth.updateProfile(fullName: name, phone: phone) { case .success: back(); case .failure(let e): error = e.message }; saving = false } } label: { HStack { if saving { ProgressView().tint(.white) }; Text("Save changes").font(.system(size: 16, weight: .bold)).foregroundStyle(.white) }.frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 16)) }.buttonStyle(.plain)
        }.padding(30) } }.background(TukiPalette.cream.ignoresSafeArea()).onAppear { name = auth.currentUserProfile?.displayName ?? ""; phone = auth.currentUserProfile?.phoneNumber ?? "" }
    }
}
private struct ParityField: View { let label: String; @Binding var text: String; var body: some View { VStack(alignment: .leading, spacing: 8) { Text(label).font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.dark); TextField("", text: $text).padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)) } } }

private struct ParityPrivacy: View {
    @ObservedObject var auth: AuthViewModel; let onBack: () -> Void; let onPassword: () -> Void
    @State private var twoFactor = false; @State private var showDelete = false; @State private var deleting = false; @State private var error: String?
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            ParityHeader(title: "Privacy & security", back: onBack); ScrollView { VStack(alignment: .leading, spacing: 8) {
                Text("PASSWORD").paritySection(); ParitySetting(icon: "🔑", title: "Change password", subtitle: "Last changed 3 months ago", action: onPassword)
                Text("SECURITY").paritySection().padding(.top, 16); HStack { VStack(alignment: .leading) { Text("Two-factor authentication").font(.system(size: 16, weight: .bold)); Text("Add an extra layer of security").font(.system(size: 12)).foregroundStyle(TukiPalette.gray) }; Spacer(); Toggle("", isOn: $twoFactor).labelsHidden().tint(TukiPalette.teal) }.padding(16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 18))
                Text("DATA").paritySection().padding(.top, 16); ParitySetting(icon: "🗑️", title: "Delete account", subtitle: "Permanently remove your data", destructive: true) { showDelete = true }
            }.padding(24) }
        }.background(TukiPalette.cream.ignoresSafeArea()).alert("Delete your account?", isPresented: $showDelete) {
            Button("Cancel", role: .cancel) { error = nil }
            Button("Delete", role: .destructive) { deleting = true; Task { switch await auth.deleteAccount() { case .success: break; case .failure(let e): error = e.message; showDelete = true }; deleting = false } }
        } message: { Text(error ?? "This will permanently delete your account and all of your data, including trip history and favorites. This can't be undone.") }
    }
}
private struct ParitySetting: View { let icon: String; let title: String; let subtitle: String; var destructive = false; let action: () -> Void; var body: some View { Button(action: action) { HStack(spacing: 14) { Text(icon).font(.system(size: 20)).frame(width: 44, height: 44).background(.white.opacity(0.4)).clipShape(RoundedRectangle(cornerRadius: 12)); VStack(alignment: .leading, spacing: 2) { Text(title).font(.system(size: 16, weight: .bold)).foregroundStyle(destructive ? Color.red : TukiPalette.dark); Text(subtitle).font(.system(size: 12)).foregroundStyle(TukiPalette.gray) }; Spacer(); Text("›").foregroundStyle(TukiPalette.gray) }.padding(16).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 18)) }.buttonStyle(.plain) } }
private extension Text { func paritySection() -> some View { self.font(.system(size: 12, weight: .bold)).foregroundStyle(TukiPalette.gray) } }

private struct ParityPassword: View {
    @ObservedObject var auth: AuthViewModel; let back: () -> Void
    @State private var current = ""; @State private var new = ""; @State private var confirm = ""; @State private var saving = false; @State private var success = false; @State private var error: String?
    var body: some View { VStack(spacing: 0) { ParityHeader(title: "Change password", back: back); ScrollView { VStack(alignment: .leading, spacing: 18) {
        Text("Enter your current password, then choose a new one.").font(.system(size: 14, weight: .semibold)).foregroundStyle(TukiPalette.gray)
        ParitySecure(label: "Current password", text: $current); ParitySecure(label: "New password", text: $new); Text("Must be at least 8 characters.").font(.system(size: 11)).foregroundStyle(TukiPalette.gray); ParitySecure(label: "Confirm new password", text: $confirm)
        if let error { Text(error).font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.error) }; if success { Text("Password changed successfully.").font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.teal) }
        Button { guard !saving && !success else { return }; if current.isEmpty { error = "Enter your current password."; return }; if new.count < 8 { error = "New password must be at least 8 characters."; return }; if new == current { error = "New password must be different from your current password."; return }; if new != confirm { error = "New password and confirmation do not match."; return }; saving = true; error = nil; Task { switch await auth.changePassword(current: current, new: new) { case .success: success = true; try? await Task.sleep(for: .milliseconds(1200)); back(); case .failure(let e): error = e.message }; saving = false } } label: { HStack { if saving { ProgressView().tint(.white) }; Text(success ? "Saved" : "Change password").font(.system(size: 16, weight: .bold)).foregroundStyle(.white) }.frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange.opacity(saving || success ? 0.4 : 1)).clipShape(RoundedRectangle(cornerRadius: 16)) }.buttonStyle(.plain)
    }.padding(24) } }.background(TukiPalette.cream.ignoresSafeArea()) }
}
private struct ParitySecure: View { let label: String; @Binding var text: String; @State private var show = false; var body: some View { VStack(alignment: .leading, spacing: 8) { Text(label).font(.system(size: 14, weight: .semibold)); HStack { Group { if show { TextField("", text: $text) } else { SecureField("", text: $text) } }; Button(show ? "HIDE" : "SHOW") { show.toggle() }.font(.system(size: 11, weight: .bold)).foregroundStyle(TukiPalette.teal).buttonStyle(.plain) }.padding(.horizontal, 14).frame(height: 56).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)) } } }
private struct ParityLanguage: View { let back: () -> Void; @State private var language = "English"; var body: some View { VStack(spacing: 0) { ParityHeader(title: "Language", back: back); VStack(alignment: .leading, spacing: 12) { Text("APP LANGUAGE").font(.system(size: 13, weight: .heavy)).foregroundStyle(TukiPalette.gray); ForEach(["English", "Filipino"], id: \.self) { option in Button { language = option } label: { HStack { Text(option).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.dark); Spacer(); Image(systemName: language == option ? "largecircle.fill.circle" : "circle").foregroundStyle(TukiPalette.teal) }.padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)) }.buttonStyle(.plain) }; Button("Save language", action: back).font(.system(size: 16, weight: .bold)).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 52).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 16)).buttonStyle(.plain); Spacer() }.padding(30) }.background(TukiPalette.cream.ignoresSafeArea()) } }

private struct ParityDestinationSearch: View {
    let api: TukiPlatformAPI?; @ObservedObject var location: TukiLocationService; let initialOriginName: String; let initialOrigin: CLLocationCoordinate2D?; let onBack: () -> Void; let onFind: (String, CLLocationCoordinate2D, TukiPlace) -> Void
    @State private var originName = ""; @State private var origin: CLLocationCoordinate2D?; @State private var destinationText = ""; @State private var selected: TukiPlace?; @State private var results: [TukiPlace] = []; @State private var originResults: [TukiPlace] = []; @State private var showMap = false; @State private var mapOrigin = false; @State private var mapCoordinate: CLLocationCoordinate2D?; @State private var unsupported = false; @State private var searchError: String?
    var body: some View {
        ZStack {
            Color.black.opacity(0.4).ignoresSafeArea()
            ScrollView { VStack(alignment: .leading, spacing: 16) {
                Button("← Back", action: onBack).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.teal).buttonStyle(.plain)
                Text("Where are you going?").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Text("Type your destination and we'll pull up your best commute options.").font(.system(size: 13, weight: .medium)).foregroundStyle(TukiPalette.gray)
                VStack(alignment: .leading, spacing: 10) {
                    Text("●  Current Location / Origin").font(.system(size: 13, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    TextField("Search or edit origin", text: $originName).padding(12).background(.white.opacity(0.65)).clipShape(RoundedRectangle(cornerRadius: 14))
                    ForEach(originResults) { place in Button { setOrigin(place) } label: { VStack(alignment: .leading) { Text(place.name).fontWeight(.bold); if let a = place.address { Text(a).font(.system(size: 11)).foregroundStyle(TukiPalette.gray) } }.frame(maxWidth: .infinity, alignment: .leading).padding(10).background(.white.opacity(0.65)).clipShape(RoundedRectangle(cornerRadius: 12)) }.buttonStyle(.plain) }
                    HStack { Button("Use Current Location") { Task { await useCurrent() } }; Button("Pick Origin on Map") { mapOrigin = true; mapCoordinate = origin; showMap = true } }.buttonStyle(.borderedProminent).tint(TukiPalette.teal).font(.system(size: 12, weight: .bold))
                }.padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
                VStack(alignment: .leading, spacing: 10) {
                    Text("📍  Pin your destination").font(.system(size: 16, weight: .bold)).foregroundStyle(.white)
                    TextField("Type or search a place", text: $destinationText).foregroundStyle(.white).padding(12).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 14))
                    ForEach(results) { place in Button { selected = place; destinationText = place.name; results = []; validate(place.latitude, place.longitude) } label: { VStack(alignment: .leading) { Text(place.name).fontWeight(.bold); if let a = place.address { Text(a).font(.system(size: 11)).opacity(0.7) } }.foregroundStyle(.white).frame(maxWidth: .infinity, alignment: .leading).padding(10).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 12)) }.buttonStyle(.plain) }
                    Button("🗺️ Open map") { mapOrigin = false; mapCoordinate = selected.map { CLLocationCoordinate2D(latitude: $0.latitude, longitude: $0.longitude) } ?? origin; showMap = true }.foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 44).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 14)).buttonStyle(.plain)
                }.padding(16).background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18))
                if let searchError { Text(searchError).font(.system(size: 12)).foregroundStyle(TukiPalette.error) }
                let canSubmit = selected != nil && origin != nil
                Button("Find Routes") { if let selected, let origin { onFind(originName, origin, selected) } }.disabled(!canSubmit).font(.system(size: 16, weight: .bold)).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 48).background(TukiPalette.orange.opacity(canSubmit ? 1 : 0.4)).clipShape(RoundedRectangle(cornerRadius: 14)).buttonStyle(.plain)
            }.padding(20).background(TukiPalette.cream).clipShape(RoundedRectangle(cornerRadius: 24)).padding(.horizontal, 20).padding(.vertical, 16) }
        }.onAppear { originName = initialOriginName; origin = initialOrigin }
            .task(id: destinationText) { await searchDestination() }.task(id: originName) { await searchOrigin() }
            .sheet(isPresented: $showMap) { ParityMapPicker(initial: mapCoordinate) { coordinate in Task { await acceptMap(coordinate) } } }
            .alert(TukiServiceArea.title, isPresented: $unsupported) { Button("OK", role: .cancel) {} } message: { Text(TukiServiceArea.message) }
    }
    private func validate(_ lat: Double, _ lon: Double) { if !TukiServiceArea.contains(latitude: lat, longitude: lon) { unsupported = true } }
    private func useCurrent() async { guard let loc = await location.requestCurrentLocation() else { searchError = TukiServiceArea.locationFailureMessage; return }; origin = loc.coordinate; if let api, case .success(let place) = await api.reverseGeocode(lat: loc.coordinate.latitude, lon: loc.coordinate.longitude) { originName = place.name }; validate(loc.coordinate.latitude, loc.coordinate.longitude) }
    private func setOrigin(_ place: TukiPlace) { originName = place.name; origin = CLLocationCoordinate2D(latitude: place.latitude, longitude: place.longitude); originResults = []; validate(place.latitude, place.longitude) }
    private func searchDestination() async { let q = destinationText.trimmingCharacters(in: .whitespacesAndNewlines); guard q.count >= 2, selected?.name != q, let api else { results = []; return }; try? await Task.sleep(for: .milliseconds(350)); guard !Task.isCancelled else { return }; switch await api.searchPlaces(q, focusLat: origin?.latitude, focusLon: origin?.longitude) { case .success(let v): results = Array(v.prefix(5)); case .failure(let e): results = []; searchError = e.message } }
    private func searchOrigin() async { let q = originName.trimmingCharacters(in: .whitespacesAndNewlines); guard q.count >= 2, q != initialOriginName, let api else { originResults = []; return }; try? await Task.sleep(for: .milliseconds(350)); guard !Task.isCancelled else { return }; switch await api.searchPlaces(q, focusLat: origin?.latitude, focusLon: origin?.longitude) { case .success(let v): originResults = Array(v.prefix(5)); case .failure: originResults = [] } }
    private func acceptMap(_ coordinate: CLLocationCoordinate2D) async { showMap = false; guard let api else { return }; validate(coordinate.latitude, coordinate.longitude); if mapOrigin { origin = coordinate; if case .success(let place) = await api.reverseGeocode(lat: coordinate.latitude, lon: coordinate.longitude) { originName = place.name } else { originName = "Pinned origin" } } else { if case .success(let place) = await api.reverseGeocode(lat: coordinate.latitude, lon: coordinate.longitude) { selected = place; destinationText = place.name } else { selected = TukiPlace(id: "map-\(coordinate.latitude)-\(coordinate.longitude)", name: "Pinned destination", latitude: coordinate.latitude, longitude: coordinate.longitude, category: "map", source: "map", address: nil); destinationText = "Pinned destination" } } }
}

private struct ParityMapPicker: View {
    let initial: CLLocationCoordinate2D?; let onUse: (CLLocationCoordinate2D) -> Void
    @Environment(\.dismiss) private var dismiss; @State private var selected: CLLocationCoordinate2D?
    var body: some View { VStack(spacing: 12) { HStack { Text("Pick location").font(.system(size: 18, weight: .bold)); Spacer(); Button("✕") { dismiss() } }; ParityMKMap(selected: $selected, initial: initial).clipShape(RoundedRectangle(cornerRadius: 18)); Text(selected.map { "📍 \(String(format: "%.5f", $0.latitude)), \(String(format: "%.5f", $0.longitude))" } ?? "Tap the map to choose a location").font(.system(size: 13)).foregroundStyle(TukiPalette.gray); if let selected { Button("Use This Location") { onUse(selected); dismiss() }.fontWeight(.bold).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 48).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14)).buttonStyle(.plain) } }.padding(16).background(TukiPalette.cream) }
}

private struct ParityMKMap: UIViewRepresentable {
    @Binding var selected: CLLocationCoordinate2D?; let initial: CLLocationCoordinate2D?
    func makeCoordinator() -> Coordinator { Coordinator(self) }
    func makeUIView(context: Context) -> MKMapView { let map = MKMapView(); map.delegate = context.coordinator; map.addGestureRecognizer(UITapGestureRecognizer(target: context.coordinator, action: #selector(Coordinator.tap(_:)))); let center = initial ?? CLLocationCoordinate2D(latitude: 15.145, longitude: 120.59); map.setRegion(MKCoordinateRegion(center: center, span: MKCoordinateSpan(latitudeDelta: 0.12, longitudeDelta: 0.12)), animated: false); return map }
    func updateUIView(_ map: MKMapView, context: Context) { map.removeAnnotations(map.annotations); if let selected { let pin = MKPointAnnotation(); pin.coordinate = selected; map.addAnnotation(pin) } }
    final class Coordinator: NSObject, MKMapViewDelegate { var parent: ParityMKMap; init(_ parent: ParityMKMap) { self.parent = parent }; @objc func tap(_ sender: UITapGestureRecognizer) { guard let map = sender.view as? MKMapView else { return }; parent.selected = map.convert(sender.location(in: map), toCoordinateFrom: map) } }
}

private struct ParityRouteResults: View {
    let api: TukiPlatformAPI?; let originName: String; let origin: CLLocationCoordinate2D; let destination: TukiPlace; let onBack: () -> Void; let onSelect: (TukiRouteChoice) -> Void
    @State private var routes: [TukiRouteChoice] = []; @State private var loading = true; @State private var error: String?; @State private var unsupported = false
    var body: some View { ScrollView { VStack(alignment: .leading, spacing: 0) { Button("← Back", action: onBack).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.teal).buttonStyle(.plain); Text("Where are you going?").font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 16); VStack(alignment: .leading, spacing: 8) { Text("●  \(originName) (current location)").font(.system(size: 14, weight: .bold)); Text("■  \(destination.name)").font(.system(size: 14)) }.padding(16).frame(maxWidth: .infinity, alignment: .leading).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)).padding(.top, 14); if loading { HStack { Spacer(); VStack { ProgressView().tint(TukiPalette.teal); Text("Finding routes...").foregroundStyle(TukiPalette.gray) }; Spacer() }.padding(.vertical, 32) } else if let error { Text("Error: \(error)").foregroundStyle(.red).fontWeight(.bold).padding(.top, 18) } else { Text("ROUTE OPTIONS · \(originName) → \(destination.name)".uppercased()).font(.system(size: 11, weight: .bold)).foregroundStyle(TukiPalette.gray).padding(.top, 18).padding(.bottom, 10); ForEach(routes) { route in Button { onSelect(route) } label: { ParityRouteCard(route: route) }.buttonStyle(.plain).padding(.bottom, 12) } } }.padding(.horizontal, 24).padding(.vertical, 12) }.background(TukiPalette.cream.ignoresSafeArea()).task { await load() }.alert(TukiServiceArea.title, isPresented: $unsupported) { Button("OK", role: .cancel) {} } message: { Text(TukiServiceArea.message) } }
    private func load() async { guard let api else { error = "Routing is not configured."; loading = false; return }; switch await api.plan(originName: originName, originLat: origin.latitude, originLon: origin.longitude, destination: destination) { case .success(let v): routes = v; case .failure(let e): error = e.message; if e.message.contains(TukiServiceArea.shortMessage) { unsupported = true } }; loading = false }
}
private struct ParityRouteCard: View { let route: TukiRouteChoice; var body: some View { VStack(alignment: .leading, spacing: 12) { HStack { Text(route.isRecommended ? "⭐ \(route.label)" : route.label).font(.system(size: 18, weight: .bold)).foregroundStyle(.white); Spacer(); if route.isRecommended { Text("RECOMMENDED").font(.system(size: 10, weight: .bold)).foregroundStyle(.white).padding(.horizontal, 10).padding(.vertical, 5).background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 10)) } }; HStack { ParityMetric(value: "~\(route.totalMinutes) min", label: "EST. TIME"); ParityMetric(value: "₱\(Int(route.totalFare))", label: "EST. FARE") }; HStack { ParityMetric(value: "\(route.walkMeters) m", label: "WALK"); ParityMetric(value: "\(route.steps.count) legs", label: route.transfers == 1 ? "1 TRANSFER" : "\(route.transfers) TRANSFERS") }; HStack { VStack(alignment: .leading) { Text("GEN. COST").font(.system(size: 10, weight: .bold)).foregroundStyle(.white.opacity(0.6)); Text("Fare + time value").font(.system(size: 10)).foregroundStyle(.white.opacity(0.5)) }; Spacer(); Text("₱\(Int(route.generalCost))").font(.system(size: 18, weight: .heavy)).foregroundStyle(TukiPalette.orange) }.padding(12).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 12)); Text("Estimates only — actual time and fare may vary with traffic and driver").font(.system(size: 10)).foregroundStyle(.white.opacity(0.45)) }.padding(20).background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18)) } }
private struct ParityMetric: View { let value: String; let label: String; var body: some View { VStack { Text(value).font(.system(size: 16, weight: .bold)).foregroundStyle(.white); Text(label).font(.system(size: 10, weight: .bold)).foregroundStyle(.white.opacity(0.55)) }.frame(maxWidth: .infinity).padding(10).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 12)) } }

private struct ParityRouteDetail: View {
    let api: TukiPlatformAPI?; @ObservedObject var auth: AuthViewModel; let originName: String; let origin: CLLocationCoordinate2D; let destination: TukiPlace; let choice: TukiRouteChoice; let onBack: () -> Void; let onStarted: (TukiNavigationSnapshot, Bool) -> Void; let onEnded: () -> Void
    @State private var working = false; @State private var error: String?; @State private var active: TukiNavigationSnapshot?
    var body: some View { VStack(alignment: .leading, spacing: 0) { ParityHeader(title: "Route Details", back: onBack); Text("\(originName) → \(destination.name)").font(.system(size: 18, weight: .bold)).foregroundStyle(TukiPalette.dark).padding(.horizontal, 30).padding(.top, 24); ScrollView { VStack(spacing: 16) { ForEach(Array(choice.steps.enumerated()), id: \.offset) { _, step in HStack(spacing: 16) { Text(step.mode.lowercased() == "jeepney" ? "🚐" : step.mode.lowercased() == "tricycle" ? "🛴" : "🚶").font(.system(size: 20)).frame(width: 40, height: 40).background(TukiPalette.teal.opacity(0.1)).clipShape(RoundedRectangle(cornerRadius: 10)); VStack(alignment: .leading) { Text("\(step.mode) to \(step.to)").font(.system(size: 16, weight: .bold)); Text("\(step.minutes) mins · \(step.fare.map { "₱\(Int($0))" } ?? "Free")").font(.system(size: 14)).foregroundStyle(TukiPalette.gray) }; Spacer() }.padding(16).background(.white).clipShape(RoundedRectangle(cornerRadius: 16)) } }.padding(.horizontal, 30).padding(.top, 24) }; if let error { Text(error).font(.system(size: 13, weight: .semibold)).foregroundStyle(.red).padding(.horizontal, 30).padding(.bottom, 10) }; if active != nil { Button("Resume Active Trip") { if let active { onStarted(active, false) } }.parityPrimary(TukiPalette.teal); Button("End Active Trip") { Task { await endActive() } }.foregroundStyle(TukiPalette.orange).frame(maxWidth: .infinity).frame(height: 52).buttonStyle(.bordered).padding(.horizontal, 30).padding(.bottom, 8) }; Button { Task { await start() } } label: { HStack { if working { ProgressView().tint(.white) }; Text(working ? "Working..." : "Start Trip").font(.system(size: 20, weight: .bold)) }.foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 60).background(TukiPalette.teal.opacity(active == nil ? 1 : 0.45)).clipShape(RoundedRectangle(cornerRadius: 20)) }.disabled(working || active != nil).buttonStyle(.plain).padding(.horizontal, 30).padding(.bottom, 20) }.background(TukiPalette.cream.ignoresSafeArea()) }
    private func start() async { guard !working else { return }; working = true; error = nil; defer { working = false }; if auth.isGuest { onStarted(choice.guestSnapshot(destination: destination.name), true); return }; guard let api else { error = "Navigation is not configured."; return }; switch await api.startNavigation(recommendationId: choice.id) { case .success(let snapshot): onStarted(snapshot, false); case .failure(let e): if e.message.localizedCaseInsensitiveContains("active trip") || e.message.localizedCaseInsensitiveContains("ACTIVE_TRIP_EXISTS") { if case .success(let snapshot) = await api.activeNavigation() { active = snapshot; error = "You already have an active trip. Resume it or end it before starting this route." } else { error = e.message } } else { error = e.message } } }
    private func endActive() async { guard let api, let active else { return }; working = true; switch await api.cancel(sessionId: active.sessionId) { case .success: self.active = nil; error = nil; onEnded(); case .failure(let e): error = e.message }; working = false }
}
private extension View { func parityPrimary(_ color: Color) -> some View { self.font(.system(size: 16, weight: .bold)).foregroundStyle(.white).frame(maxWidth: .infinity).frame(height: 52).background(color).clipShape(RoundedRectangle(cornerRadius: 14)).buttonStyle(.plain).padding(.horizontal, 30).padding(.bottom, 8) } }

private extension TukiRouteChoice {
    func guestSnapshot(destination: String) -> TukiNavigationSnapshot {
        let first = steps.first
        let end = legEndPoints.first
        return TukiNavigationSnapshot(sessionId: "guest-\(UUID().uuidString)", state: "GuestActive", currentLegIndex: 0, currentLeg: first.map { TukiNavigationLeg(legIndex: 0, transportMode: $0.mode.uppercased(), routeName: nil, fromName: $0.from, toName: $0.to, startLatitude: nil, startLongitude: nil, endLatitude: end?.latitude, endLongitude: end?.longitude, distanceMeters: nil, fare: $0.fare ?? 0) }, nextInstruction: first.map { TukiNavigationInstruction(type: "Continue", routeName: nil, transportMode: $0.mode.uppercased(), distanceMeters: nil, requiresConfirmation: false) }, spokenInstruction: first.map { "Take \($0.mode) toward \($0.to)" }, remainingDistanceMeters: nil, progressMeters: 0, boardInfo: nil, alightInfo: nil, landmark: nil, requiresBoardingConfirmation: false, requiresAlightingConfirmation: first?.mode.lowercased() == "jeepney", rerouteRequired: false, status: "Guest navigation", triggeredEvents: [])
    }
}

private struct ParityTracking: View {
    let api: TukiPlatformAPI?; @ObservedObject var location: TukiLocationService; let originName: String; let destination: TukiPlace; let choice: TukiRouteChoice; let initialSnapshot: TukiNavigationSnapshot; let isGuest: Bool; let onBackToRoute: () -> Void; let onEnded: () -> Void
    @State private var snapshot: TukiNavigationSnapshot; @State private var error: String?; @State private var working = false; @State private var showExit = false; @State private var paraPo = false
    init(api: TukiPlatformAPI?, location: TukiLocationService, originName: String, destination: TukiPlace, choice: TukiRouteChoice, initialSnapshot: TukiNavigationSnapshot, isGuest: Bool, onBackToRoute: @escaping () -> Void, onEnded: @escaping () -> Void) { self.api = api; self.location = location; self.originName = originName; self.destination = destination; self.choice = choice; self.initialSnapshot = initialSnapshot; self.isGuest = isGuest; self.onBackToRoute = onBackToRoute; self.onEnded = onEnded; _snapshot = State(initialValue: initialSnapshot) }
    var body: some View { ZStack { ParityRouteMap(points: choice.legRoutePoints.flatMap { $0 }, destination: choice.legEndPoints.last); VStack { HStack(spacing: 16) { Button("‹") { requestBack() }.font(.system(size: 20, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(width: 32, height: 32).background(TukiPalette.cream).clipShape(RoundedRectangle(cornerRadius: 8)).buttonStyle(.plain); VStack(alignment: .leading) { Text("Current Trip").font(.system(size: 13, weight: .bold)).foregroundStyle(TukiPalette.gray); Text("\(originName) → \(destination.name)").font(.system(size: 16, weight: .heavy)).foregroundStyle(TukiPalette.dark); if let route = snapshot.currentLeg?.routeName { Text(route).font(.system(size: 12, weight: .bold)).foregroundStyle(TukiPalette.teal) } }; Spacer() }.padding(20).background(.white).clipShape(RoundedRectangle(cornerRadius: 20)).padding(30); Spacer(); VStack(alignment: .leading, spacing: 12) { HStack { VStack(alignment: .leading) { Text("NEXT STEP").font(.system(size: 12, weight: .heavy)).foregroundStyle(TukiPalette.teal); Text(snapshot.displayInstruction).font(.system(size: 19, weight: .heavy)).foregroundStyle(TukiPalette.dark); Text(snapshot.remainingDistanceMeters.map { $0 >= 1000 ? String(format: "%.1f km remaining", $0/1000) : "\(Int($0.rounded())) m remaining" } ?? "Waiting for location update").font(.system(size: 14)).foregroundStyle(TukiPalette.gray); if let error { Text(error).font(.system(size: 12)).foregroundStyle(TukiPalette.error) } }; Spacer(); Button("🔔") { paraPo = true }.font(.system(size: 28)).frame(width: 64, height: 64).background(TukiPalette.orange.opacity(canParaPo ? 0.2 : 0.08)).clipShape(Circle()).buttonStyle(.plain).disabled(!canParaPo) }; if snapshot.requiresBoardingConfirmation || snapshot.requiresAlightingConfirmation { Button(snapshot.requiresBoardingConfirmation ? "Confirm Board" : "Confirm Alight") { Task { await confirm() } }.parityPrimary(snapshot.requiresBoardingConfirmation ? TukiPalette.teal : TukiPalette.orange).padding(.horizontal, -30) }; ProgressView(value: progress).tint(TukiPalette.teal); Text(snapshot.status.replacingOccurrences(of: "_", with: " ")).font(.system(size: 10, weight: .bold)).foregroundStyle(TukiPalette.gray) }.padding(24).background(.white).clipShape(RoundedRectangle(cornerRadius: 24)).shadow(radius: 8).padding(20) } }.alert("Trip is still active", isPresented: $showExit) { Button("Continue Trip", role: .cancel) {}; Button("End Trip", role: .destructive) { Task { await end() } } } message: { Text("Going back will not end the navigation session. Continue the trip or end it first?") }.overlay { if paraPo { Color.black.opacity(0.4).ignoresSafeArea().onTapGesture { paraPo = false }; VStack(spacing: 10) { Text("🔔").font(.system(size: 52)); Text("Para po!").font(.system(size: 30, weight: .heavy)).foregroundStyle(TukiPalette.dark); Text("Get ready to alight at your stop.").foregroundStyle(TukiPalette.gray); Button("OK") { paraPo = false }.fontWeight(.bold).foregroundStyle(.white).padding(.horizontal, 36).padding(.vertical, 12).background(TukiPalette.orange).clipShape(Capsule()) }.padding(28).background(TukiPalette.cream).clipShape(RoundedRectangle(cornerRadius: 24)) } }.task(id: snapshot.sessionId) { await poll() }
    private var canParaPo: Bool { snapshot.requiresAlightingConfirmation || snapshot.nextInstruction?.type.localizedCaseInsensitiveContains("alight") == true }
    private var progress: Double { guard let distance = snapshot.currentLeg?.distanceMeters, distance > 0 else { return 0 }; return min(max(snapshot.progressMeters / distance, 0), 1) }
    private func requestBack() { if snapshot.state.lowercased() == "arrived" || snapshot.state.lowercased() == "cancelled" { onBackToRoute() } else { showExit = true } }
    private func end() async { guard !working else { return }; working = true; if isGuest { onEnded(); return }; guard let api else { working = false; return }; switch await api.cancel(sessionId: snapshot.sessionId) { case .success: onEnded(); case .failure(let e): error = e.message }; working = false }
    private func confirm() async { guard !working, let api, !isGuest else { return }; working = true; let result = snapshot.requiresBoardingConfirmation ? await api.board(sessionId: snapshot.sessionId) : await api.alight(sessionId: snapshot.sessionId); switch result { case .success(let value): snapshot = value; error = nil; case .failure(let e): error = e.message }; working = false }
    private func poll() async { guard !isGuest, let api else { return }; while !Task.isCancelled { if snapshot.state.lowercased() == "arrived" || snapshot.state.lowercased() == "cancelled" { return }; if let loc = await location.requestCurrentLocation() { let update = TukiNavigationLocationUpdate(latitude: loc.coordinate.latitude, longitude: loc.coordinate.longitude, accuracyMeters: loc.horizontalAccuracy, timestamp: ISO8601DateFormatter().string(from: loc.timestamp), speedMetersPerSecond: loc.speed >= 0 ? loc.speed : nil, bearingDegrees: loc.course >= 0 ? loc.course : nil); switch await api.updateLocation(sessionId: snapshot.sessionId, update: update) { case .success(let value): snapshot = value; error = nil; case .failure(let e): error = e.message } } else { error = TukiServiceArea.locationFailureMessage }; try? await Task.sleep(for: .seconds(5)) } }
}

private struct ParityRouteMap: View {
    let points: [TukiCoordinate]; let destination: TukiCoordinate?; @State private var region = MKCoordinateRegion(center: CLLocationCoordinate2D(latitude: 15.145, longitude: 120.59), span: MKCoordinateSpan(latitudeDelta: 0.13, longitudeDelta: 0.13))
    var body: some View { Map(coordinateRegion: $region, annotationItems: destination.map { [ParityPin(id: "dest", coordinate: CLLocationCoordinate2D(latitude: $0.latitude, longitude: $0.longitude))] } ?? []) { pin in MapMarker(coordinate: pin.coordinate, tint: .orange) }.ignoresSafeArea().onAppear { if let first = points.first { region.center = CLLocationCoordinate2D(latitude: first.latitude, longitude: first.longitude) } } }
}
private struct ParityPin: Identifiable { let id: String; let coordinate: CLLocationCoordinate2D }

private struct ParityCommuteDetail: View { let commute: RecentCommute; let back: () -> Void; var body: some View { ScrollView { VStack(alignment: .leading, spacing: 0) { Button("← Back", action: back).font(.system(size: 16, weight: .bold)).foregroundStyle(TukiPalette.teal).buttonStyle(.plain); Text("\(commute.origin) → \(commute.destination)").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark).padding(.top, 20); Text("\(commute.legs) legs · \(commute.minutes) min total").font(.system(size: 16, weight: .semibold)).foregroundStyle(TukiPalette.teal).padding(.top, 6); if commute.steps.isEmpty { Text("No step-by-step breakdown saved for this trip yet.").foregroundStyle(TukiPalette.gray).padding(.top, 24) } else { ForEach(Array(commute.steps.enumerated()), id: \.offset) { _, step in HStack(spacing: 12) { Capsule().fill(TukiPalette.orange).frame(width: 6, height: 36); VStack(alignment: .leading) { Text("\(step.mode): \(step.from) → \(step.to)").font(.system(size: 15, weight: .bold)); Text("\(step.minutes) min\(step.fare.map { " · ₱\(Int($0))" } ?? "")").font(.system(size: 13)).foregroundStyle(TukiPalette.gray) } }.padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14)).padding(.top, 10) } } }.padding(30) }.background(TukiPalette.cream.ignoresSafeArea()) } }

private struct ParityAIChat: View {
    let userName: String; let onBack: () -> Void; let onDestination: (String) -> Void
    @State private var messages: [AIMessage] = []; @State private var input = ""; @State private var thinking = false
    var body: some View { VStack(spacing: 0) { HStack(spacing: 10) { Button("←", action: onBack).font(.system(size: 24, weight: .bold)).foregroundStyle(TukiPalette.dark).buttonStyle(.plain); Text("✨").frame(width: 38, height: 38).background(TukiPalette.teal.opacity(0.12)).clipShape(RoundedRectangle(cornerRadius: 12)); VStack(alignment: .leading) { Text("Ask our AI").font(.system(size: 20, weight: .heavy)); Text("Tell me where you want to go").font(.system(size: 12)).foregroundStyle(TukiPalette.gray) }; Spacer() }.padding(.horizontal, 20).padding(.vertical, 14); ScrollView { LazyVStack(spacing: 12) { ForEach(messages) { msg in VStack(alignment: msg.user ? .trailing : .leading, spacing: 8) { Text(msg.text).foregroundStyle(.white).padding(.horizontal, 14).padding(.vertical, 10).background(msg.user ? TukiPalette.orange : Color(red: 31/255, green: 75/255, blue: 82/255)).clipShape(RoundedRectangle(cornerRadius: 16)); if msg.place { VStack(alignment: .leading, spacing: 6) { Text("📍 Jollibee SM Clark").fontWeight(.bold); Text("Clark Freeport Zone, Pampanga").font(.system(size: 12)).opacity(0.75); HStack { Button("Yes, that's it") { onDestination("Jollibee SM Clark") }; Button("Not quite") { send("Not quite, let me try again") } }.buttonStyle(.borderedProminent).tint(TukiPalette.orange) }.foregroundStyle(.white).padding(12).background(TukiPalette.teal).clipShape(RoundedRectangle(cornerRadius: 14)) } }.frame(maxWidth: .infinity, alignment: msg.user ? .trailing : .leading) }; if thinking { Text("Thinking…").foregroundStyle(TukiPalette.gray).frame(maxWidth: .infinity, alignment: .leading) }; if messages.count <= 1 { VStack(alignment: .leading) { Text("Try asking:").font(.system(size: 12, weight: .bold)).foregroundStyle(TukiPalette.gray); HStack { ForEach(["near the church in Angeles", "my lola's place sa Dau"], id: \.self) { prompt in Button(prompt) { send(prompt) }.font(.system(size: 12)).buttonStyle(.bordered) } } }.frame(maxWidth: .infinity, alignment: .leading) } }.padding(16) }; HStack { TextField("Type your message...", text: $input).foregroundStyle(.white).padding(12).background(.white.opacity(0.08)).clipShape(Capsule()); Button("➤") { send(input) }.font(.system(size: 17, weight: .bold)).foregroundStyle(.white).frame(width: 44, height: 44).background(TukiPalette.orange.opacity(input.isEmpty || thinking ? 0.45 : 1)).clipShape(Circle()).buttonStyle(.plain).disabled(input.isEmpty || thinking) }.padding(12).background(TukiPalette.dark) }.background(TukiPalette.cream).onAppear { if messages.isEmpty { messages = [AIMessage(text: "Hi \(userName)! Where would you like to go? You can describe it in your own words.", user: false, place: false)] } } }
    private func send(_ text: String) { guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !thinking else { return }; messages.append(AIMessage(text: text.trimmingCharacters(in: .whitespacesAndNewlines), user: true, place: false)); input = ""; thinking = true; Task { try? await Task.sleep(for: .milliseconds(700)); messages.append(AIMessage(text: "Got it — found a Jollibee near SM Clark, Clark Freeport Zone. Is this the one?", user: false, place: true)); thinking = false } }
}
private struct AIMessage: Identifiable { let id = UUID(); let text: String; let user: Bool; let place: Bool }
