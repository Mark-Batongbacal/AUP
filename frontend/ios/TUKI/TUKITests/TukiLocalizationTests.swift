import XCTest
@testable import TUKI

final class TukiLocalizationTests: XCTestCase {
    private func isolatedDefaults() -> UserDefaults {
        let suiteName = "TukiLocalizationTests.\(UUID().uuidString)"
        return UserDefaults(suiteName: suiteName)!
    }

    func testDefaultsToEnglishWhenNoPreferenceStored() {
        let preference = TukiLanguagePreference(defaults: isolatedDefaults())
        XCTAssertEqual(preference.currentLanguage, "English")
        XCTAssertFalse(preference.isFilipino())
    }

    func testNormalizesFilipinoTagalogAndFilRegionVariants() {
        XCTAssertEqual(TukiLanguagePreference.normalize("Filipino"), "Filipino")
        XCTAssertEqual(TukiLanguagePreference.normalize("filipino"), "Filipino")
        XCTAssertEqual(TukiLanguagePreference.normalize("Tagalog"), "Filipino")
        XCTAssertEqual(TukiLanguagePreference.normalize("fil-PH"), "Filipino")
    }

    func testNormalizesAnythingElseToEnglish() {
        XCTAssertEqual(TukiLanguagePreference.normalize("English"), "English")
        XCTAssertEqual(TukiLanguagePreference.normalize(nil), "English")
        XCTAssertEqual(TukiLanguagePreference.normalize("es"), "English")
    }

    func testUpdatePersistsAcrossInstances() {
        let defaults = isolatedDefaults()
        let first = TukiLanguagePreference(defaults: defaults)
        first.update("Filipino")

        let second = TukiLanguagePreference(defaults: defaults)
        XCTAssertEqual(second.currentLanguage, "Filipino")
        XCTAssertTrue(second.isFilipino())
    }

    func testInterfaceTextFlipsWithSharedPreference() {
        // TukiInterfaceText reads the shared singleton, so drive it directly and restore
        // afterward to avoid leaking state into other tests.
        let original = TukiLanguagePreference.shared.currentLanguage
        defer { TukiLanguagePreference.shared.update(original) }

        TukiLanguagePreference.shared.update("English")
        XCTAssertEqual(TukiInterfaceText.hello, "Hello")

        TukiLanguagePreference.shared.update("Filipino")
        XCTAssertEqual(TukiInterfaceText.hello, "Kamusta")
    }

    func testStatusOnlyTranslatesKnownStatusVocabulary() {
        let original = TukiLanguagePreference.shared.currentLanguage
        defer { TukiLanguagePreference.shared.update(original) }

        TukiLanguagePreference.shared.update("Filipino")
        XCTAssertEqual(TukiInterfaceText.status("Completed"), "Natapos")
        XCTAssertEqual(TukiInterfaceText.status("Cancelled"), "Kinansela")
        XCTAssertEqual(TukiInterfaceText.status("WaitingToBoard"), "Naghihintay Sumakay")
        XCTAssertEqual(TukiInterfaceText.status("SomeOtherRawStatus"), "SomeOtherRawStatus")

        TukiLanguagePreference.shared.update("English")
        XCTAssertEqual(TukiInterfaceText.status("Completed"), "Completed")
    }
}
