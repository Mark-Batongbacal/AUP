import XCTest
@testable import TUKI

final class TukiDestinationPickerMergeTests: XCTestCase {
    private func place(
        id: String = UUID().uuidString,
        name: String,
        lat: Double,
        lon: Double,
        address: String? = nil
    ) -> TukiPlace {
        TukiPlace(id: id, name: name, latitude: lat, longitude: lon, category: "poi", source: "search", address: address)
    }

    func testMergeDropsDuplicateByCloseCoordinatesAndMatchingName() {
        let existing = [place(name: "SM City Clark", lat: 15.1900, lon: 120.5400)]
        let expanded = [place(name: "SM City Clark", lat: 15.1901, lon: 120.5401)]
        let merged = mergePlaceResults(existing, expanded)
        XCTAssertEqual(merged.count, 1)
    }

    func testMergeDropsDuplicateByMatchingAddressEvenIfFar() {
        let existing = [place(name: "Jollibee", lat: 15.10, lon: 120.50, address: "MacArthur Hwy, Dau")]
        let expanded = [place(name: "Jollibee", lat: 15.30, lon: 120.68, address: "MacArthur Hwy, Dau")]
        let merged = mergePlaceResults(existing, expanded)
        XCTAssertEqual(merged.count, 1)
    }

    func testMergeKeepsDistinctPlacesWithSameNameDifferentLocation() {
        let existing = [place(name: "Jollibee", lat: 15.10, lon: 120.50, address: "Dau Branch")]
        let expanded = [place(name: "Jollibee", lat: 15.25, lon: 120.65, address: "Porac Branch")]
        let merged = mergePlaceResults(existing, expanded)
        XCTAssertEqual(merged.count, 2)
    }

    func testMergePreservesOrderExistingFirst() {
        let existing = [place(name: "A", lat: 1, lon: 1)]
        let expanded = [place(name: "B", lat: 2, lon: 2), place(name: "C", lat: 3, lon: 3)]
        let merged = mergePlaceResults(existing, expanded)
        XCTAssertEqual(merged.map(\.name), ["A", "B", "C"])
    }

    func testPlacesLikelySameIsFalseForEmptyNames() {
        let a = place(name: "", lat: 15.1, lon: 120.5)
        let b = place(name: "", lat: 15.1, lon: 120.5)
        XCTAssertFalse(placesLikelySame(a, b))
    }

    func testNormalizedPlaceTextStripsPunctuationAndCase() {
        XCTAssertEqual(normalizedPlaceText("SM City Clark!"), "smcityclark")
        XCTAssertEqual(normalizedPlaceText("Dau, Mabalacat"), "daumabalacat")
    }
}
