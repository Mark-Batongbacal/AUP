import CoreLocation
import MapKit
import SwiftUI

/// Which point this picker instance is choosing. Mirrors Android's `HomeMapPickMode`
/// (screens/HomeScreen.kt) — `.destination` doubles as the live-trip "Change Destination"
/// case, where the origin is fixed to the trip's current location.
enum TukiPlacePickerMode: Equatable {
    case origin
    case destination
}

/// Fixed chrome colors for the picker overlay — always dark regardless of the app's
/// light/dark theme, matching Android's flat (non-theme-reactive) `MapPanel`/
/// `MapSelector`/`MapAction` constants in HomeScreen.kt / LiveTripDestinationPickerScreen.kt.
private enum TukiPickerPalette {
    static let panel = Color(red: 0x0C / 255, green: 0x30 / 255, blue: 0x3A / 255)
    static let selectorChip = Color(red: 0xF8 / 255, green: 0xF5 / 255, blue: 0xEC / 255)
    static let action = Color(red: 0xFF / 255, green: 0x8A / 255, blue: 0x1D / 255)
    static let markerFill = Color(red: 0x2C / 255, green: 0x8E / 255, blue: 0x95 / 255)
}

/// Full-screen map + search destination picker. Ported from Android's
/// `HomeMapPickerOverlay` (screens/HomeScreen.kt) and `LiveTripDestinationPickerScreen.kt`,
/// which share this exact UX: full-screen map → current-area chip → search field →
/// results (with "More places" pagination) → tap-map-to-pin → reverse-geocode-on-pin →
/// selected place row → Done. One component serves Home's pickup/destination selection
/// and the in-trip "Change Destination" flow.
struct TukiUnifiedDestinationPickerScreen: View {
    let api: TukiPlatformAPI?
    let mode: TukiPlacePickerMode
    let focusLatitude: Double?
    let focusLongitude: Double?
    let initialSelection: TukiPlace?
    let onBack: () -> Void
    let onDone: (TukiPlace) -> Void

    @State private var currentFocusLatitude: Double?
    @State private var currentFocusLongitude: Double?
    @State private var areaLabel = TukiInterfaceText.currentArea
    @State private var selection: TukiPlace?
    @State private var searchText = ""
    @State private var searchResults: [TukiPlace] = []
    @State private var isSearching = false
    @State private var isSearchingMore = false
    @State private var searchExpanded = false
    @State private var searchError: String?
    @State private var showUnsupportedLocationDialog = false
    @State private var cameraPosition: MapCameraPosition

    init(
        api: TukiPlatformAPI?,
        mode: TukiPlacePickerMode,
        focusLatitude: Double?,
        focusLongitude: Double?,
        initialSelection: TukiPlace?,
        onBack: @escaping () -> Void,
        onDone: @escaping (TukiPlace) -> Void
    ) {
        self.api = api
        self.mode = mode
        self.focusLatitude = focusLatitude
        self.focusLongitude = focusLongitude
        self.initialSelection = initialSelection
        self.onBack = onBack
        self.onDone = onDone
        _currentFocusLatitude = State(initialValue: focusLatitude)
        _currentFocusLongitude = State(initialValue: focusLongitude)
        _selection = State(initialValue: initialSelection)
        let center = CLLocationCoordinate2D(
            latitude: focusLatitude ?? TukiMapCameraFraming.defaultCenter.latitude,
            longitude: focusLongitude ?? TukiMapCameraFraming.defaultCenter.longitude
        )
        _cameraPosition = State(initialValue: .region(
            MKCoordinateRegion(center: center, span: MKCoordinateSpan(latitudeDelta: 0.05, longitudeDelta: 0.05))
        ))
    }

    private var canSearchMore: Bool {
        searchText.trimmingCharacters(in: .whitespacesAndNewlines).count >= 2 && !isSearching && !searchExpanded
    }

    private var focusCoordinate: CLLocationCoordinate2D? {
        guard let currentFocusLatitude, let currentFocusLongitude else { return nil }
        return CLLocationCoordinate2D(latitude: currentFocusLatitude, longitude: currentFocusLongitude)
    }

    private var selectionCoordinate: CLLocationCoordinate2D? {
        guard let selection else { return nil }
        return CLLocationCoordinate2D(latitude: selection.latitude, longitude: selection.longitude)
    }

    var body: some View {
        MapReader { proxy in
            ZStack {
                Map(position: $cameraPosition) {
                    if let focusCoordinate {
                        Marker(TukiInterfaceText.currentLocation, coordinate: focusCoordinate)
                            .tint(TukiPickerPalette.markerFill)
                    }
                    if let selectionCoordinate {
                        Marker(selection?.name ?? TukiInterfaceText.destination, coordinate: selectionCoordinate)
                            .tint(TukiPickerPalette.action)
                    }
                }
                .ignoresSafeArea()
                .simultaneousGesture(
                    SpatialTapGesture().onEnded { value in
                        if let coordinate = proxy.convert(value.location, from: .local) {
                            handleMapTap(coordinate)
                        }
                    }
                )

                VStack(spacing: 0) {
                    topBar
                    if isSearching || isSearchingMore || canSearchMore || searchError != nil || !searchResults.isEmpty {
                        resultsPanel
                    }
                    Spacer(minLength: 0)
                }
                .padding(.horizontal, 14)
                .padding(.top, 10)

                VStack(spacing: 0) {
                    Spacer(minLength: 0)
                    bottomSheet
                }
            }
        }
        .task { await resolveFocus() }
        .task(id: searchText) { await search() }
        .task(id: mapSelectionResolutionKey) { await reverseGeocodeMapSelection() }
        .alert(TukiServiceArea.title, isPresented: $showUnsupportedLocationDialog) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(TukiServiceArea.message)
        }
    }

    private var mapSelectionResolutionKey: String {
        guard let selection else { return "" }
        return "\(selection.latitude)-\(selection.longitude)-\(selection.source)"
    }

    // MARK: - Top bar

    private var topBar: some View {
        HStack(spacing: 8) {
            Button(action: onBack) {
                Text("‹")
                    .font(.system(size: 30, weight: .bold))
                    .foregroundStyle(Color(red: 0x75 / 255, green: 0xC7 / 255, blue: 0xE8 / 255))
                    .frame(width: 34, height: 34)
            }
            .buttonStyle(.plain)

            Text(areaLabel.isEmpty ? TukiInterfaceText.currentArea : areaLabel)
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(Color(red: 0x15 / 255, green: 0x3E / 255, blue: 0x4B / 255))
                .lineLimit(1)
                .truncationMode(.tail)
                .padding(.horizontal, 10)
                .padding(.vertical, 8)
                .frame(maxWidth: 120)
                .background(TukiPickerPalette.selectorChip)
                .clipShape(RoundedRectangle(cornerRadius: 10))

            HStack(spacing: 4) {
                TextField("", text: $searchText, prompt: Text(TukiInterfaceText.searchLocation).foregroundStyle(.white.opacity(0.55)))
                    .foregroundStyle(.white)
                    .submitLabel(.search)
                if !searchText.isEmpty {
                    Button("✕") { searchText = "" }
                        .font(.system(size: 16))
                        .foregroundStyle(.white.opacity(0.7))
                        .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 4)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .background(TukiPickerPalette.panel.opacity(0.95))
        .clipShape(RoundedRectangle(cornerRadius: 24))
    }

    // MARK: - Results panel

    private var resultsPanel: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 0) {
                if isSearching {
                    HStack(spacing: 9) {
                        ProgressView().tint(TukiPickerPalette.markerFill)
                        Text(TukiInterfaceText.searchingNearbyPlaces).font(.system(size: 13)).foregroundStyle(.white.opacity(0.75))
                    }
                    .padding(.horizontal, 14).padding(.vertical, 9)
                }
                if let searchError {
                    Text(searchError).font(.system(size: 12)).foregroundStyle(TukiPalette.error)
                        .padding(.horizontal, 14).padding(.vertical, 8)
                }
                ForEach(searchResults) { place in
                    Button {
                        selection = place
                        searchText = place.name
                        searchResults = []
                        searchExpanded = false
                        searchError = nil
                    } label: {
                        HStack(spacing: 10) {
                            ZStack {
                                Circle().fill(Color.white.opacity(0.12)).frame(width: 30, height: 30)
                                Text("⌖").font(.system(size: 17)).foregroundStyle(TukiPickerPalette.markerFill)
                            }
                            VStack(alignment: .leading, spacing: 2) {
                                Text(place.name).font(.system(size: 13, weight: .heavy)).foregroundStyle(.white).lineLimit(2)
                                if let address = place.address, !address.isEmpty {
                                    Text(address).font(.system(size: 11)).foregroundStyle(.white.opacity(0.62)).lineLimit(2)
                                }
                            }
                            Spacer(minLength: 0)
                        }
                        .padding(.horizontal, 14).padding(.vertical, 9)
                    }
                    .buttonStyle(.plain)
                }
                if isSearchingMore {
                    HStack(spacing: 9) {
                        ProgressView().tint(TukiPickerPalette.markerFill)
                        Text(TukiInterfaceText.searchingMorePlaces).font(.system(size: 13)).foregroundStyle(.white.opacity(0.75))
                    }
                    .padding(.horizontal, 14).padding(.vertical, 10)
                } else if canSearchMore {
                    Button { Task { await searchMore() } } label: {
                        Text(TukiInterfaceText.morePlaces)
                            .font(.system(size: 13, weight: .heavy))
                            .foregroundStyle(TukiPickerPalette.markerFill)
                            .frame(maxWidth: .infinity)
                            .padding(.horizontal, 14).padding(.vertical, 12)
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.vertical, 7)
        }
        .frame(maxHeight: 300)
        .background(TukiPickerPalette.panel.opacity(0.95))
        .clipShape(RoundedRectangle(cornerRadius: 18))
        .padding(.top, 8)
    }

    // MARK: - Bottom sheet

    private var bottomSheet: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(mode == .origin ? TukiInterfaceText.pickupPoint : TukiInterfaceText.destination)
                .font(.system(size: 25, weight: .heavy))
                .foregroundStyle(.white)

            HStack(spacing: 16) {
                ZStack {
                    Circle().fill(Color.white.opacity(0.16)).frame(width: 34, height: 34)
                    Circle().fill(TukiPickerPalette.markerFill).frame(width: 25, height: 25)
                    Circle().fill(TukiPickerPalette.panel).frame(width: 15, height: 15)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(selection?.name ?? TukiInterfaceText.tapMapOrSearchPlace)
                        .font(.system(size: 18))
                        .foregroundStyle(.white)
                        .lineLimit(1)
                    Text(selection?.address ?? TukiInterfaceText.moveMapThenDone)
                        .font(.system(size: 13))
                        .foregroundStyle(.white.opacity(0.55))
                        .lineLimit(1)
                }
                Spacer(minLength: 0)
            }

            Button {
                confirmSelection()
            } label: {
                Text(TukiInterfaceText.done)
                    .font(.system(size: 20, weight: .heavy))
                    .foregroundStyle(.white)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 17)
                    .background(selection != nil ? TukiPickerPalette.action : TukiPickerPalette.action.opacity(0.45))
                    .clipShape(RoundedRectangle(cornerRadius: 28))
            }
            .buttonStyle(.plain)
            .disabled(selection == nil)
        }
        .padding(.horizontal, 22)
        .padding(.vertical, 22)
        .padding(.bottom, 8)
        .frame(maxWidth: .infinity)
        .background(TukiPickerPalette.panel)
        .clipShape(RoundedCornersShape(radius: 28, corners: [.topLeft, .topRight]))
        .ignoresSafeArea(edges: .bottom)
    }

    // MARK: - Behavior

    private func resolveFocus() async {
        if currentFocusLatitude == nil || currentFocusLongitude == nil {
            return
        }
        await resolveAreaLabel()
    }

    private func resolveAreaLabel() async {
        guard let api, let lat = currentFocusLatitude, let lon = currentFocusLongitude else { return }
        if case .success(let place) = await api.reverseGeocode(lat: lat, lon: lon) {
            areaLabel = place.locality?.trimmingCharacters(in: .whitespaces).isEmpty == false
                ? place.locality!
                : TukiInterfaceText.currentArea
        }
    }

    private func search() async {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        searchExpanded = false
        isSearchingMore = false
        guard query.count >= 2 else {
            searchResults = []
            searchError = nil
            isSearching = false
            return
        }
        try? await Task.sleep(for: .milliseconds(300))
        guard !Task.isCancelled, let api else { return }
        isSearching = true
        searchError = nil
        switch await api.searchPlaces(query, focusLat: currentFocusLatitude, focusLon: currentFocusLongitude) {
        case .success(let values): searchResults = Array(values.prefix(5))
        case .failure(let error): searchResults = []; searchError = error.message
        }
        isSearching = false
    }

    private func searchMore() async {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard query.count >= 2, !isSearching, !isSearchingMore, !searchExpanded, let api else { return }
        isSearchingMore = true
        searchError = nil
        switch await api.searchMorePlaces(query, focusLat: currentFocusLatitude, focusLon: currentFocusLongitude) {
        case .success(let values):
            if searchText.trimmingCharacters(in: .whitespacesAndNewlines) == query {
                searchResults = Array(mergePlaceResults(searchResults, values).prefix(12))
                searchExpanded = true
            }
        case .failure(let error):
            if searchText.trimmingCharacters(in: .whitespacesAndNewlines) == query {
                searchError = error.message
            }
        }
        isSearchingMore = false
    }

    private func handleMapTap(_ coordinate: CLLocationCoordinate2D) {
        selection = TukiPlace(
            id: "map-\(coordinate.latitude)-\(coordinate.longitude)",
            name: mode == .origin ? "Pinned pickup" : TukiInterfaceText.pinnedDestination,
            latitude: coordinate.latitude,
            longitude: coordinate.longitude,
            category: "map",
            source: "map",
            address: nil
        )
    }

    private func reverseGeocodeMapSelection() async {
        guard let selection, selection.source == "map", let api else { return }
        if case .success(let place) = await api.reverseGeocode(lat: selection.latitude, lon: selection.longitude) {
            self.selection = TukiPlace(
                id: selection.id,
                name: place.name,
                latitude: selection.latitude,
                longitude: selection.longitude,
                category: place.category,
                source: "map-resolved",
                address: place.address,
                locality: place.locality
            )
            if mode == .origin, let locality = place.locality, !locality.trimmingCharacters(in: .whitespaces).isEmpty {
                areaLabel = locality
            }
        }
    }

    private func confirmSelection() {
        guard let selection else { return }
        guard TukiServiceArea.contains(latitude: selection.latitude, longitude: selection.longitude) else {
            showUnsupportedLocationDialog = true
            return
        }
        onDone(selection)
    }

}

/// Deduplicates "More places" results against what's already shown. Ported from Android's
/// `mergeHomePlaceResults`/`homePlacesLikelySame`/`normalizeHomePlaceText` (screens/HomeScreen.kt) —
/// free functions there too, so kept that way here for the same direct testability.
func mergePlaceResults(_ existing: [TukiPlace], _ expanded: [TukiPlace]) -> [TukiPlace] {
    var merged: [TukiPlace] = []
    for candidate in existing + expanded where !merged.contains(where: { placesLikelySame($0, candidate) }) {
        merged.append(candidate)
    }
    return merged
}

func placesLikelySame(_ first: TukiPlace, _ second: TukiPlace) -> Bool {
    let firstName = normalizedPlaceText(first.name)
    let secondName = normalizedPlaceText(second.name)
    guard !firstName.isEmpty, firstName == secondName else { return false }

    let closeCoordinates = abs(first.latitude - second.latitude) <= 0.002
        && abs(first.longitude - second.longitude) <= 0.002
    let firstAddress = normalizedPlaceText(first.address ?? "")
    let secondAddress = normalizedPlaceText(second.address ?? "")
    let sameAddress = !firstAddress.isEmpty && firstAddress == secondAddress
    return closeCoordinates || sameAddress
}

func normalizedPlaceText(_ value: String) -> String {
    value.lowercased().filter { $0.isLetter || $0.isNumber }
}

/// Rounds only the given corners — SwiftUI has no built-in for this pre-iOS 26 shape API.
private struct RoundedCornersShape: Shape {
    var radius: CGFloat
    var corners: UIRectCorner

    func path(in rect: CGRect) -> Path {
        let path = UIBezierPath(
            roundedRect: rect,
            byRoundingCorners: corners,
            cornerRadii: CGSize(width: radius, height: radius)
        )
        return Path(path.cgPath)
    }
}
