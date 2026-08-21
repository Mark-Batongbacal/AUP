package com.example.frontend.screens

import androidx.compose.runtime.Composable

/**
 * Compatibility overload for ProfileScreen's callback-driven invocation.
 *
 * The current ChangePasswordScreen owns the AuthRepository and performs the
 * request-OTP/change-password calls itself. ProfileScreen still supplies the
 * older callbacks, so keep this overload until ProfileScreen is simplified.
 * The actual UI and network flow are delegated to the canonical two-callback
 * ChangePasswordScreen implementation.
 */
sealed interface ChangePasswordResult {
    data object Success : ChangePasswordResult
    data class Error(val message: String) : ChangePasswordResult
}

@Composable
fun ChangePasswordScreen(
    onBack: () -> Unit = {},
    onRequestOtp: suspend (currentPassword: String) -> ChangePasswordResult,
    onChangePassword: suspend (
        currentPassword: String,
        code: String,
        newPassword: String
    ) -> ChangePasswordResult,
    onPasswordChanged: () -> Unit = {}
) {
    // The canonical screen currently performs these operations through its own
    // TukiDataProvider/AuthRepository. Keeping the parameters in this overload
    // preserves ProfileScreen source compatibility without creating a second
    // password-change implementation.
    @Suppress("UNUSED_VARIABLE")
    val callbacks = onRequestOtp to onChangePassword

    ChangePasswordScreen(
        onBack = onBack,
        onPasswordChanged = onPasswordChanged
    )
}
