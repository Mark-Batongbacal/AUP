//
//  AppConfiguration.swift
//  TUKI
//

import Foundation

struct AppConfiguration {
    let backendBaseURL: URL
    let googleOAuth: GoogleOAuthConfiguration?
    let facebookOAuth: FacebookOAuthConfiguration?

    static func load(bundle: Bundle = .main) throws -> AppConfiguration {
        try load(infoDictionary: bundle.infoDictionary ?? [:])
    }

    static func load(infoDictionary: [String: Any]) throws -> AppConfiguration {
        guard
            let baseURLString = infoDictionary["TukiBackendBaseURL"] as? String,
            let backendBaseURL = URL(string: baseURLString.normalizedBaseURL)
        else {
            throw AppConfigurationError.missingBackendBaseURL
        }

        let googleClientID = infoDictionary["GIDClientID"] as? String
        let googleServerClientID = infoDictionary["GIDServerClientID"] as? String
        let googleOAuth = GoogleOAuthConfiguration(
            clientID: googleClientID,
            serverClientID: googleServerClientID
        )
        let facebookOAuth = FacebookOAuthConfiguration(
            appID: infoDictionary["FacebookAppID"] as? String,
            clientToken: infoDictionary["FacebookClientToken"] as? String
        )

        return AppConfiguration(
            backendBaseURL: backendBaseURL,
            googleOAuth: googleOAuth,
            facebookOAuth: facebookOAuth
        )
    }
}

struct GoogleOAuthConfiguration: Equatable {
    let clientID: String
    let serverClientID: String

    init?(clientID: String?, serverClientID: String?) {
        guard let clientID, clientID.isConfiguredValue,
              let serverClientID, serverClientID.isConfiguredValue else {
            return nil
        }

        self.clientID = clientID
        self.serverClientID = serverClientID
    }
}

struct FacebookOAuthConfiguration: Equatable {
    let appID: String
    let clientToken: String

    init?(appID: String?, clientToken: String?) {
        guard let appID, appID.isConfiguredValue,
              let clientToken, clientToken.isConfiguredValue else {
            return nil
        }

        self.appID = appID
        self.clientToken = clientToken
    }
}

enum AppConfigurationError: Error, Equatable {
    case missingBackendBaseURL
}

private extension String {
    var normalizedBaseURL: String {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.hasSuffix("/") ? trimmed : "\(trimmed)/"
    }

    var isConfiguredValue: Bool {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return !trimmed.isEmpty &&
            trimmed != "0" &&
            !trimmed.uppercased().hasPrefix("YOUR_") &&
            !trimmed.uppercased().hasPrefix("DEFAULT_")
    }
}
