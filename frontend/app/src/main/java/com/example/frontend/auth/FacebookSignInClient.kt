package com.example.frontend.auth

import android.app.Activity
import android.content.Intent
import com.facebook.CallbackManager
import com.facebook.FacebookCallback
import com.facebook.FacebookException
import com.facebook.login.LoginManager
import com.facebook.login.LoginResult
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

class FacebookSignInClient(
    private val loginManager: LoginManager = LoginManager.getInstance(),
    private val callbackManager: CallbackManager = CallbackManager.Factory.create()
) {
    suspend fun getAccessToken(
        activity: Activity,
        appId: String,
        clientToken: String
    ): FacebookSignInResult {
        if (appId.isMissingFacebookConfig() || clientToken.isMissingFacebookConfig()) {
            return FacebookSignInResult.Failure("Facebook login is not configured.")
        }

        return suspendCancellableCoroutine { continuation ->
            loginManager.registerCallback(
                callbackManager,
                object : FacebookCallback<LoginResult> {
                    override fun onSuccess(result: LoginResult) {
                        loginManager.unregisterCallback(callbackManager)
                        val accessToken = result.accessToken.token
                        if (accessToken.isBlank()) {
                            continuation.resumeIfActive(
                                FacebookSignInResult.Failure(
                                    "Facebook sign-in returned an invalid credential."
                                )
                            )
                        } else {
                            continuation.resumeIfActive(
                                FacebookSignInResult.Success(accessToken)
                            )
                        }
                    }

                    override fun onCancel() {
                        loginManager.unregisterCallback(callbackManager)
                        continuation.resumeIfActive(FacebookSignInResult.Canceled)
                    }

                    override fun onError(error: FacebookException) {
                        loginManager.unregisterCallback(callbackManager)
                        continuation.resumeIfActive(
                            FacebookSignInResult.Failure(
                                "Facebook sign-in failed. Try again."
                            )
                        )
                    }
                }
            )

            continuation.invokeOnCancellation {
                loginManager.unregisterCallback(callbackManager)
            }

            loginManager.logInWithReadPermissions(
                activity,
                listOf("public_profile", "email")
            )
        }
    }

    fun onActivityResult(
        requestCode: Int,
        resultCode: Int,
        data: Intent?
    ): Boolean = callbackManager.onActivityResult(requestCode, resultCode, data)

    private fun String.isMissingFacebookConfig(): Boolean {
        return isBlank() ||
            this == "0" ||
            startsWith("YOUR_", ignoreCase = true) ||
            startsWith("DEFAULT_", ignoreCase = true)
    }

    private fun kotlinx.coroutines.CancellableContinuation<FacebookSignInResult>.resumeIfActive(
        result: FacebookSignInResult
    ) {
        if (isActive) {
            resume(result)
        }
    }
}

sealed interface FacebookSignInResult {
    data class Success(val accessToken: String) : FacebookSignInResult
    data object Canceled : FacebookSignInResult
    data class Failure(val message: String) : FacebookSignInResult
}
