//
//  AppConfigurationTests.swift
//  TUKITests
//

import XCTest
@testable import TUKI

final class AppConfigurationTests: XCTestCase {
    func testLoadKeepsPlaceholderGoogleConfigurationInactive() throws {
        let configuration = try AppConfiguration.load(infoDictionary: [
            "TukiBackendBaseURL": "https://example.test",
            "GIDClientID": "YOUR_IOS_CLIENT_ID.apps.googleusercontent.com",
            "GIDServerClientID": "YOUR_WEB_OR_SERVER_CLIENT_ID.apps.googleusercontent.com",
            "FacebookAppID": "YOUR_FACEBOOK_APP_ID",
            "FacebookClientToken": "YOUR_FACEBOOK_CLIENT_TOKEN"
        ])

        XCTAssertEqual(configuration.backendBaseURL.absoluteString, "https://example.test/")
        XCTAssertNil(configuration.googleOAuth)
        XCTAssertNil(configuration.facebookOAuth)
    }

    func testLoadReadsConfiguredSocialClientValues() throws {
        let configuration = try AppConfiguration.load(infoDictionary: [
            "TukiBackendBaseURL": "https://example.test/api/",
            "GIDClientID": "ios-client-id.apps.googleusercontent.com",
            "GIDServerClientID": "server-client-id.apps.googleusercontent.com",
            "FacebookAppID": "1234567890",
            "FacebookClientToken": "facebook-client-token"
        ])

        XCTAssertEqual(configuration.backendBaseURL.absoluteString, "https://example.test/api/")
        XCTAssertEqual(configuration.googleOAuth?.clientID, "ios-client-id.apps.googleusercontent.com")
        XCTAssertEqual(configuration.googleOAuth?.serverClientID, "server-client-id.apps.googleusercontent.com")
        XCTAssertEqual(configuration.facebookOAuth?.appID, "1234567890")
        XCTAssertEqual(configuration.facebookOAuth?.clientToken, "facebook-client-token")
    }

    func testLoadRequiresBackendBaseURL() {
        XCTAssertThrowsError(try AppConfiguration.load(infoDictionary: [:])) { error in
            XCTAssertEqual(error as? AppConfigurationError, .missingBackendBaseURL)
        }
    }
}
