//
//  TukiCredentialStore.swift
//  TUKI
//

import Combine
import Foundation
import Security

protocol TukiCredentialStore {
    var credential: TukiCredential? { get }
    func save(_ credential: TukiCredential) throws
    func clear() throws
}

final class KeychainTukiCredentialStore: TukiCredentialStore {
    private let service: String
    private let account = "tuki-api-credential"
    private let encoder = JSONEncoder()
    private let decoder = JSONDecoder()

    init(service: String = Bundle.main.bundleIdentifier ?? "com.aup.TUKI") {
        self.service = service
    }

    var credential: TukiCredential? {
        let query = baseQuery.merging([
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne
        ]) { current, _ in current }

        var item: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &item)
        guard status == errSecSuccess, let data = item as? Data else {
            return nil
        }

        return try? decoder.decode(TukiCredential.self, from: data)
    }

    func save(_ credential: TukiCredential) throws {
        let data = try encoder.encode(credential)
        let query = baseQuery
        let attributes = [kSecValueData as String: data]

        let status = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if status == errSecSuccess {
            return
        }

        guard status == errSecItemNotFound else {
            throw KeychainCredentialStoreError.unhandled(status)
        }

        var newItem = query
        newItem[kSecValueData as String] = data
        newItem[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly

        let addStatus = SecItemAdd(newItem as CFDictionary, nil)
        guard addStatus == errSecSuccess else {
            throw KeychainCredentialStoreError.unhandled(addStatus)
        }
    }

    func clear() throws {
        let status = SecItemDelete(baseQuery as CFDictionary)
        guard status == errSecSuccess || status == errSecItemNotFound else {
            throw KeychainCredentialStoreError.unhandled(status)
        }
    }

    private var baseQuery: [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
    }
}

enum KeychainCredentialStoreError: Error, Equatable {
    case unhandled(OSStatus)
}
