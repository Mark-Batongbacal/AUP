import XCTest
@testable import TUKI

final class TukiThemeRuntimeTests: XCTestCase {
    private func isolatedDefaults() -> UserDefaults {
        let suiteName = "TukiThemeRuntimeTests.\(UUID().uuidString)"
        return UserDefaults(suiteName: suiteName)!
    }

    func testDefaultsToLightModeWhenNoPreferenceStored() {
        let runtime = TukiThemeRuntime(defaults: isolatedDefaults())
        XCTAssertFalse(runtime.isDarkMode)
    }

    func testTogglingPersistsAcrossInstances() {
        let defaults = isolatedDefaults()
        let first = TukiThemeRuntime(defaults: defaults)
        first.isDarkMode = true

        let second = TukiThemeRuntime(defaults: defaults)
        XCTAssertTrue(second.isDarkMode)
    }

    func testTogglingBackToFalsePersists() {
        let defaults = isolatedDefaults()
        let first = TukiThemeRuntime(defaults: defaults)
        first.isDarkMode = true
        first.isDarkMode = false

        let second = TukiThemeRuntime(defaults: defaults)
        XCTAssertFalse(second.isDarkMode)
    }

    func testSharedInstanceIsSingleton() {
        XCTAssertTrue(TukiThemeRuntime.shared === TukiThemeRuntime.shared)
    }
}
