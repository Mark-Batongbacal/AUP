import CoreLocation
import MapKit
import SwiftUI

struct TukiUnifiedDestinationSearchView: View {
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
    @State private var searching = false

    var body: some View {
        VStack(spacing: 0) {
            routeHeader("Where are you going?", onBack: onBack)
            VStack(spacing: 14) {
                HStack(spacing: 10) {
                    Circle().fill(TukiPalette.teal).frame(width: 9, height: 9)
                    Text("\(initialOriginName) (current location)")
                        .font(.system(size: 14, weight: .bold)).foregroundStyle(TukiPalette.dark)
                    Spacer()
                }
                .padding(14).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))

                TextField("Type or search a place", text: $destinationText)
                    .padding(14).background(.white).clipShape(RoundedRectangle(cornerRadius: 14))

                if searching { ProgressView().tint(TukiPalette.teal) }

                ScrollView {
                    LazyVStack(spacing: 8) {
                        ForEach(results) { place in
                            Button {
                                selected = place
                                destinationText = place.name
                                unsupported = !TukiServiceArea.contains(latitude: place.latitude, longitude: place.longitude)
                            } label: {
                                HStack(spacing: 10) {
                                    Text("📍")
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text(place.name).font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                                        if let address = place.address, !address.isEmpty {
                                            Text(address).font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                                        }
                                    }
                                    Spacer()
                                }
                                .padding(13).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12))
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }

                TukiPrimaryButton(title: "Find Routes", isEnabled: selected != nil) {
                    guard let selected else { return }
                    Task {
                        let origin = initialOrigin ?? await location.requestCurrentLocation()?.coordinate
                        guard let origin else { return }
                        onFind(initialOriginName, origin, selected)
                    }
                }
            }
            .padding(.horizontal, 24)
            .padding(.bottom, 20)
        }
        .background(TukiPalette.cream.ignoresSafeArea())
        .task(id: destinationText) { await search() }
        .alert(TukiServiceArea.title, isPresented: $unsupported) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(TukiServiceArea.message)
        }
    }

    private func search() async {
        let query = destinationText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard query.count >= 2, selected?.name != query, let api else {
            results = []
            return
        }
        searching = true
        try? await Task.sleep(for: .milliseconds(300))
        guard !Task.isCancelled else { searching = false; return }
        let focus = initialOrigin ?? location.currentLocation?.coordinate
        if case .success(let values) = await api.searchPlaces(query, focusLat: focus?.latitude, focusLon: focus?.longitude) {
            results = Array(values.prefix(6))
        }
        searching = false
    }
}

struct TukiUnifiedRouteResultsView: View {
    let api: TukiPlatformAPI?
    let originName: String
    let origin: CLLocationCoordinate2D
    let destination: TukiPlace
    let presetRoute: TukiRouteChoice?
    let onBack: () -> Void
    let onSelect: (TukiRouteChoice) -> Void

    @State private var routes: [TukiRouteChoice] = []
    @State private var loading = true
    @State private var error: String?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Button("← Back", action: onBack).foregroundStyle(TukiPalette.teal).fontWeight(.bold).buttonStyle(.plain)
                Text("Where are you going?").font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                originDestinationCard

                if loading {
                    HStack { Spacer(); ProgressView("Finding routes...").tint(TukiPalette.teal); Spacer() }.padding(.vertical, 30)
                } else if let error {
                    Text(error).foregroundStyle(TukiPalette.error).fontWeight(.semibold)
                } else {
                    Text("ROUTE OPTIONS · \(originName) → \(destination.name)".uppercased())
                        .font(.system(size: 11, weight: .bold)).foregroundStyle(TukiPalette.gray)
                    ForEach(routes) { route in
                        routeCard(route)
                    }
                }
            }
            .padding(.horizontal, 24)
            .padding(.vertical, 14)
        }
        .background(TukiPalette.cream)
        .task { await load() }
    }

    private var originDestinationCard: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(spacing: 10) {
                Circle().fill(TukiPalette.teal).frame(width: 9, height: 9)
                Text("\(originName) (current location)").font(.system(size: 14, weight: .bold))
            }
            HStack(spacing: 10) {
                RoundedRectangle(cornerRadius: 2).fill(TukiPalette.orange).frame(width: 9, height: 9)
                Text(destination.name).font(.system(size: 14, weight: .medium))
            }
        }
        .foregroundStyle(TukiPalette.dark)
        .padding(16).frame(maxWidth: .infinity, alignment: .leading)
        .background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 14))
    }

    private func routeCard(_ route: TukiRouteChoice) -> some View {
        VStack(alignment: .leading, spacing: 13) {
            HStack {
                Text(route.isRecommended ? "⭐ \(route.label)" : route.label)
                    .font(.system(size: 18, weight: .bold))
                Spacer()
                if route.isRecommended {
                    Text("RECOMMENDED").font(.system(size: 9, weight: .bold)).padding(.horizontal, 9).padding(.vertical, 5).background(TukiPalette.orange).clipShape(Capsule())
                }
            }
            HStack(spacing: 10) {
                stat("~\(route.totalMinutes) min", "EST. TIME")
                stat("₱\(Int(route.totalFare))", "EST. FARE")
            }
            HStack(spacing: 10) {
                stat("\(route.walkMeters) m", "WALK")
                stat("\(route.steps.count) legs", route.transfers == 1 ? "1 TRANSFER" : "\(route.transfers) TRANSFERS")
            }
            HStack {
                VStack(alignment: .leading) {
                    Text("GEN. COST").font(.system(size: 10, weight: .bold)).opacity(0.6)
                    Text("Fare + time value").font(.system(size: 10)).opacity(0.5)
                }
                Spacer()
                Text("₱\(Int(route.generalCost))").font(.system(size: 18, weight: .heavy)).foregroundStyle(TukiPalette.orange)
            }
            .padding(12).background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 12))

            Text("Estimates only — actual time and fare may vary with traffic and driver")
                .font(.system(size: 10)).opacity(0.45)

            Button { onSelect(route) } label: {
                Text("Select This Route")
                    .font(.system(size: 15, weight: .bold)).foregroundStyle(.white)
                    .frame(maxWidth: .infinity).frame(height: 48)
                    .background(TukiPalette.orange).clipShape(RoundedRectangle(cornerRadius: 14))
            }
            .buttonStyle(.plain)
        }
        .foregroundStyle(.white)
        .padding(20).background(TukiPalette.dark).clipShape(RoundedRectangle(cornerRadius: 18))
    }

    private func stat(_ value: String, _ title: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value).font(.system(size: 15, weight: .bold))
            Text(title).font(.system(size: 9, weight: .bold)).opacity(0.55)
        }
        .padding(11).frame(maxWidth: .infinity, alignment: .leading)
        .background(.white.opacity(0.08)).clipShape(RoundedRectangle(cornerRadius: 12))
    }

    private func load() async {
        if let presetRoute {
            routes = [presetRoute]
            loading = false
            return
        }
        guard TukiServiceArea.contains(latitude: origin.latitude, longitude: origin.longitude),
              TukiServiceArea.contains(latitude: destination.latitude, longitude: destination.longitude) else {
            error = TukiServiceArea.shortMessage
            loading = false
            return
        }
        guard let api else { error = "Routing is not configured."; loading = false; return }
        switch await api.plan(originName: originName, originLat: origin.latitude, originLon: origin.longitude, destination: destination) {
        case .success(let values): routes = values
        case .failure(let value): error = value.message
        }
        loading = false
    }
}

struct TukiUnifiedRouteDetailView: View {
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
        VStack(spacing: 0) {
            routeHeader("Route Details", onBack: onBack)
            ScrollView {
                VStack(alignment: .leading, spacing: 14) {
                    Text("\(originName) → \(destination.name)")
                        .font(.system(size: 15, weight: .semibold)).foregroundStyle(TukiPalette.gray)
                    summaryCard
                    ForEach(Array(choice.steps.enumerated()), id: \.offset) { index, step in
                        HStack(alignment: .top, spacing: 12) {
                            Text("\(index + 1)").font(.system(size: 13, weight: .bold)).foregroundStyle(.white).frame(width: 30, height: 30).background(TukiPalette.teal).clipShape(Circle())
                            VStack(alignment: .leading, spacing: 3) {
                                Text("\(step.mode) to \(step.to)").font(.system(size: 15, weight: .bold)).foregroundStyle(TukiPalette.dark)
                                Text("~\(step.minutes) min" + (step.fare.map { " · ₱\(Int($0))" } ?? ""))
                                    .font(.system(size: 12)).foregroundStyle(TukiPalette.gray)
                            }
                            Spacer()
                        }
                        .padding(15).background(.white).clipShape(RoundedRectangle(cornerRadius: 16))
                    }
                    if let error { Text(error).foregroundStyle(TukiPalette.error).fontWeight(.semibold) }
                    if let active {
                        Button("Resume Active Trip") { onStarted(active, false) }
                            .buttonStyle(.borderedProminent).tint(TukiPalette.teal)
                        Button("End Active Trip") { Task { await endActive() } }
                            .foregroundStyle(TukiPalette.orange).fontWeight(.bold)
                    }
                    TukiPrimaryButton(title: working ? "Working..." : "Start Trip", isLoading: working, isEnabled: !working && active == nil) {
                        Task { await start() }
                    }
                }
                .padding(.horizontal, 24)
                .padding(.bottom, 24)
            }
        }
        .background(TukiPalette.cream.ignoresSafeArea())
    }

    private var summaryCard: some View {
        HStack(spacing: 10) {
            detailStat("~\(choice.totalMinutes) min", "TIME")
            detailStat("₱\(Int(choice.totalFare))", "FARE")
            detailStat("\(choice.walkMeters) m", "WALK")
        }
    }

    private func detailStat(_ value: String, _ label: String) -> some View {
        VStack(spacing: 2) {
            Text(value).font(.system(size: 14, weight: .bold)).foregroundStyle(TukiPalette.dark)
            Text(label).font(.system(size: 9, weight: .bold)).foregroundStyle(TukiPalette.gray)
        }
        .frame(maxWidth: .infinity).padding(.vertical, 12).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12))
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
            if value.message.localizedCaseInsensitiveContains("active trip"),
               case .success(let snapshot) = await api.activeNavigation() {
                active = snapshot
                error = "You already have an active trip. Resume it or end it before starting this route."
            } else {
                error = value.message
            }
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
            sessionId: "guest-\(UUID().uuidString)",
            state: "GuestActive",
            currentLegIndex: 0,
            currentLeg: first.map {
                TukiNavigationLeg(
                    legIndex: 0, transportMode: $0.mode.uppercased(), routeName: nil,
                    fromName: $0.from, toName: $0.to,
                    startLatitude: nil, startLongitude: nil,
                    endLatitude: end?.latitude, endLongitude: end?.longitude,
                    distanceMeters: nil, fare: $0.fare ?? 0
                )
            },
            nextInstruction: first.map {
                TukiNavigationInstruction(type: "Continue", routeName: nil, transportMode: $0.mode.uppercased(), distanceMeters: nil, requiresConfirmation: false)
            },
            spokenInstruction: first.map { "Take \($0.mode) toward \($0.to)" },
            remainingDistanceMeters: nil,
            progressMeters: 0,
            boardInfo: nil,
            alightInfo: nil,
            landmark: nil,
            requiresBoardingConfirmation: false,
            requiresAlightingConfirmation: first?.mode.lowercased() == "jeepney",
            rerouteRequired: false,
            status: "Guest navigation",
            triggeredEvents: []
        )
    }
}

struct TukiUnifiedTrackingView: View {
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

    init(
        api: TukiPlatformAPI?,
        location: TukiLocationService,
        originName: String,
        destination: TukiPlace,
        choice: TukiRouteChoice,
        initialSnapshot: TukiNavigationSnapshot,
        isGuest: Bool,
        onEnded: @escaping () -> Void
    ) {
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
            routeMap
            VStack {
                HStack {
                    Button("‹") { showExit = true }.font(.system(size: 24, weight: .bold)).buttonStyle(.plain)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Current Trip").font(.system(size: 13, weight: .bold)).foregroundStyle(TukiPalette.gray)
                        Text("\(originName) → \(destination.name)").font(.system(size: 16, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                    }
                    Spacer()
                }
                .padding(20).background(.white).clipShape(RoundedRectangle(cornerRadius: 20)).padding(24)
                Spacer()
                VStack(alignment: .leading, spacing: 12) {
                    Text("NEXT STEP").font(.system(size: 12, weight: .heavy)).foregroundStyle(TukiPalette.teal)
                    Text(snapshot.displayInstruction).font(.system(size: 19, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                    if let following = snapshot.followingDisplayInstruction {
                        VStack(alignment: .leading, spacing: 3) {
                            Text("THEN").font(.system(size: 10, weight: .bold)).foregroundStyle(TukiPalette.gray)
                            Text(following).font(.system(size: 13, weight: .semibold)).foregroundStyle(TukiPalette.dark.opacity(0.75))
                        }
                    }
                    Text(snapshot.remainingDistanceMeters.map { "\(Int($0.rounded())) m remaining" } ?? "Waiting for location update")
                        .foregroundStyle(TukiPalette.gray)
                    HStack(spacing: 12) {
                        Text("Spent ₱\(Int(snapshot.approxFareSpent))")
                        Text("Remaining ~₱\(Int(snapshot.estimatedRemainingFare))")
                    }
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(TukiPalette.gray)
                    if let error { Text(error).foregroundStyle(TukiPalette.error).font(.system(size: 12)) }
                    HStack {
                        if snapshot.requiresBoardingConfirmation || snapshot.requiresAlightingConfirmation {
                            Button(snapshot.requiresBoardingConfirmation ? "Confirm Board" : "Confirm Alight") { Task { await confirm() } }
                                .buttonStyle(.borderedProminent)
                                .tint(snapshot.requiresBoardingConfirmation ? TukiPalette.teal : TukiPalette.orange)
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

    private var routeMap: some View {
        Map {
            if let destinationPoint = choice.legEndPoints.last {
                Marker("Destination", coordinate: CLLocationCoordinate2D(latitude: destinationPoint.latitude, longitude: destinationPoint.longitude)).tint(.orange)
            }
        }
        .ignoresSafeArea()
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

struct TukiUnifiedCommuteDetailView: View {
    let commute: RecentCommute
    let onBack: () -> Void
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Button("← Back", action: onBack).foregroundStyle(TukiPalette.teal).fontWeight(.bold).buttonStyle(.plain)
                Text("\(commute.origin) → \(commute.destination)").font(.system(size: 24, weight: .heavy)).foregroundStyle(TukiPalette.dark)
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

private func routeHeader(_ title: String, onBack: @escaping () -> Void) -> some View {
    HStack(spacing: 14) {
        Button(action: onBack) {
            Text("‹").font(.system(size: 22, weight: .bold)).foregroundStyle(TukiPalette.dark).frame(width: 40, height: 40).background(TukiPalette.creamCard).clipShape(RoundedRectangle(cornerRadius: 12))
        }
        .buttonStyle(.plain)
        Text(title).font(.system(size: 22, weight: .heavy)).foregroundStyle(TukiPalette.dark)
        Spacer()
    }
    .padding(.horizontal, 24)
    .padding(.vertical, 20)
}
