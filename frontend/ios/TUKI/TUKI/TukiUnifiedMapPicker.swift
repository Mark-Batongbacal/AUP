import CoreLocation
import MapKit
import SwiftUI

enum TukiMapPickMode: String, Identifiable {
    case origin
    case destination
    var id: String { rawValue }
    var title: String { self == .origin ? "Pick origin" : "Pick destination" }
    var useTitle: String { self == .origin ? "Use This Origin" : "Use This Destination" }
}

struct TukiUnifiedMapPicker: View {
    let mode: TukiMapPickMode
    let initialCoordinate: CLLocationCoordinate2D?
    let onCancel: () -> Void
    let onUse: (CLLocationCoordinate2D) -> Void

    @State private var selected: CLLocationCoordinate2D?
    @State private var position: MapCameraPosition

    init(
        mode: TukiMapPickMode,
        initialCoordinate: CLLocationCoordinate2D?,
        onCancel: @escaping () -> Void,
        onUse: @escaping (CLLocationCoordinate2D) -> Void
    ) {
        self.mode = mode
        self.initialCoordinate = initialCoordinate
        self.onCancel = onCancel
        self.onUse = onUse
        _selected = State(initialValue: initialCoordinate)
        let center = initialCoordinate ?? CLLocationCoordinate2D(latitude: 15.145, longitude: 120.59)
        _position = State(initialValue: .region(MKCoordinateRegion(
            center: center,
            span: MKCoordinateSpan(latitudeDelta: 0.08, longitudeDelta: 0.08)
        )))
    }

    var body: some View {
        VStack(spacing: 14) {
            HStack {
                Text(mode.title).font(.system(size: 20, weight: .heavy)).foregroundStyle(TukiPalette.dark)
                Spacer()
                Button("✕", action: onCancel).font(.system(size: 18, weight: .bold)).foregroundStyle(TukiPalette.dark).buttonStyle(.plain)
            }

            MapReader { proxy in
                Map(position: $position) {
                    if let selected {
                        Marker(mode == .origin ? "Origin" : "Destination", coordinate: selected)
                            .tint(mode == .origin ? TukiPalette.teal : TukiPalette.orange)
                    }
                }
                .mapControls { MapUserLocationButton(); MapCompass() }
                .clipShape(RoundedRectangle(cornerRadius: 18))
                .simultaneousGesture(
                    SpatialTapGesture().onEnded { value in
                        if let coordinate = proxy.convert(value.location, from: .local) {
                            selected = coordinate
                        }
                    }
                )
            }
            .frame(height: 420)

            if let selected {
                Text("📍 %.5f, %.5f".formatted(selected.latitude, selected.longitude))
                    .font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
                TukiPrimaryButton(title: mode.useTitle) { onUse(selected) }
            } else {
                Text("Tap the map to choose a \(mode.rawValue).")
                    .font(.system(size: 13)).foregroundStyle(TukiPalette.gray)
            }
        }
        .padding(18)
        .background(TukiPalette.cream.ignoresSafeArea())
    }
}

private extension String {
    func formatted(_ latitude: Double, _ longitude: Double) -> String {
        String(format: self, latitude, longitude)
    }
}
