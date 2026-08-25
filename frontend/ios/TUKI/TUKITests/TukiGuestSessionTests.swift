import XCTest
@testable import TUKI

final class TukiGuestSessionTests: XCTestCase {
    /// Whole-second `now`, so formatting `expiry` through `ISO8601DateFormatter` (which
    /// truncates sub-second precision) can't shift a clean minute offset by a hair.
    private func wholeSecondNow() -> Date {
        Date(timeIntervalSince1970: floor(Date().timeIntervalSince1970))
    }

    func testReturnsFallbackTextWhenExpiresAtIsMissing() {
        XCTAssertEqual(tukiGuestRemainingText(expiresAt: nil), "24-hour access")
    }

    func testReturnsFallbackTextWhenExpiresAtIsUnparseable() {
        XCTAssertEqual(tukiGuestRemainingText(expiresAt: "not-a-date"), "24-hour access")
    }

    func testReturnsExpiredWhenDeadlineHasPassed() {
        let now = Date()
        let past = now.addingTimeInterval(-60)
        let text = tukiGuestRemainingText(expiresAt: ISO8601DateFormatter().string(from: past), now: now)
        XCTAssertEqual(text, "expired")
    }

    func testFormatsHoursAndMinutesRemaining() {
        let now = wholeSecondNow()
        let expiry = now.addingTimeInterval(3 * 3600 + 25 * 60)
        let text = tukiGuestRemainingText(expiresAt: ISO8601DateFormatter().string(from: expiry), now: now)
        XCTAssertEqual(text, "3h 25m remaining")
    }

    func testFormatsMinutesOnlyWhenUnderAnHour() {
        let now = wholeSecondNow()
        let expiry = now.addingTimeInterval(45 * 60)
        let text = tukiGuestRemainingText(expiresAt: ISO8601DateFormatter().string(from: expiry), now: now)
        XCTAssertEqual(text, "45m remaining")
    }

    func testNeverShowsZeroMinutesWhileStillPositive() {
        let now = wholeSecondNow()
        let expiry = now.addingTimeInterval(10) // 10 seconds left, rounds down to 0 minutes without the floor
        let text = tukiGuestRemainingText(expiresAt: ISO8601DateFormatter().string(from: expiry), now: now)
        XCTAssertEqual(text, "1m remaining")
    }

    func testParsesFractionalSecondsTimestamp() {
        let now = wholeSecondNow()
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let expiry = now.addingTimeInterval(60 * 60)
        let text = tukiGuestRemainingText(expiresAt: formatter.string(from: expiry), now: now)
        XCTAssertEqual(text, "1h 0m remaining")
    }
}
