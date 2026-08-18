package com.example.frontend.auth

import android.app.Activity
import androidx.credentials.CredentialManager
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import androidx.credentials.exceptions.GetCredentialCancellationException
import androidx.credentials.exceptions.GetCredentialException
import androidx.credentials.exceptions.NoCredentialException
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import com.google.android.libraries.identity.googleid.GoogleIdTokenParsingException

class GoogleSignInClient(
    private val credentialManager: CredentialManager
) {
    suspend fun getIdToken(
        activity: Activity,
        serverClientId: String
    ): GoogleSignInResult {
        if (serverClientId.isBlank() ||
            serverClientId.startsWith("YOUR_", ignoreCase = true)
        ) {
            return GoogleSignInResult.Failure("Google login is not configured.")
        }

        val googleOption = GetSignInWithGoogleOption.Builder(serverClientId)
            .build()
        val request = GetCredentialRequest.Builder()
            .addCredentialOption(googleOption)
            .build()

        return try {
            val response = credentialManager.getCredential(
                context = activity,
                request = request
            )
            val credential = response.credential
            if (credential is CustomCredential &&
                credential.type == GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
            ) {
                val googleCredential = GoogleIdTokenCredential.createFrom(credential.data)
                val idToken = googleCredential.idToken
                if (idToken.isBlank()) {
                    GoogleSignInResult.Failure("Google sign-in returned an invalid credential.")
                } else {
                    GoogleSignInResult.Success(idToken)
                }
            } else {
                GoogleSignInResult.Failure("Google sign-in returned an unsupported credential.")
            }
        } catch (_: GetCredentialCancellationException) {
            GoogleSignInResult.Failure("Google sign-in was canceled.")
        } catch (_: NoCredentialException) {
            GoogleSignInResult.Failure("No Google account is available on this device.")
        } catch (_: GoogleIdTokenParsingException) {
            GoogleSignInResult.Failure("Google sign-in returned an invalid credential.")
        } catch (_: GetCredentialException) {
            GoogleSignInResult.Failure("Google sign-in failed. Try again.")
        }
    }
}

sealed interface GoogleSignInResult {
    data class Success(val idToken: String) : GoogleSignInResult
    data class Failure(val message: String) : GoogleSignInResult
}
