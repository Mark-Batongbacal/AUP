package com.example.frontend.screens

import android.content.Context
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.example.frontend.R
import com.example.frontend.components.OtpCodeField
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.core.network.ApiResult
import com.example.frontend.data.TukiDataProvider
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private enum class ForgotPasswordStage { EMAIL, OTP, PASSWORD }

private const val ForgotPasswordOtpCooldownMillis = 180_000L
private const val ForgotPasswordOtpPreferences = "forgot_password_otp_cooldown"
private const val ForgotPasswordOtpEmailKey = "email"
private const val ForgotPasswordOtpUntilKey = "cooldown_until"

private class ForgotPasswordOtpCooldownStore(context: Context) {
    private val preferences = context.getSharedPreferences(
        ForgotPasswordOtpPreferences,
        Context.MODE_PRIVATE
    )

    fun restoredEmail(): String = preferences.getString(ForgotPasswordOtpEmailKey, "").orEmpty()

    fun cooldownUntil(email: String): Long {
        val normalized = normalizeCooldownEmail(email)
        val storedEmail = preferences.getString(ForgotPasswordOtpEmailKey, null)
            ?.let(::normalizeCooldownEmail)
            .orEmpty()
        if (normalized.isBlank() || !normalized.equals(storedEmail, ignoreCase = true)) {
            return 0L
        }

        return preferences.getLong(ForgotPasswordOtpUntilKey, 0L)
    }

    fun start(email: String, nowMillis: Long = System.currentTimeMillis()): Long {
        val normalized = normalizeCooldownEmail(email)
        val until = nowMillis + ForgotPasswordOtpCooldownMillis
        preferences.edit()
            .putString(ForgotPasswordOtpEmailKey, normalized)
            .putLong(ForgotPasswordOtpUntilKey, until)
            .apply()
        return until
    }

    fun clear(email: String) {
        val normalized = normalizeCooldownEmail(email)
        val storedEmail = preferences.getString(ForgotPasswordOtpEmailKey, null)
            ?.let(::normalizeCooldownEmail)
            .orEmpty()
        if (normalized.equals(storedEmail, ignoreCase = true)) {
            preferences.edit().clear().apply()
        }
    }
}

private fun normalizeCooldownEmail(email: String): String = email.trim().lowercase()

internal fun forgotPasswordResendSecondsRemaining(
    cooldownUntilMillis: Long,
    nowMillis: Long
): Int {
    val remainingMillis = cooldownUntilMillis - nowMillis
    if (remainingMillis <= 0L) return 0
    return ((remainingMillis + 999L) / 1_000L).toInt()
}

internal fun forgotPasswordResendLabel(secondsRemaining: Int): String {
    val safeSeconds = secondsRemaining.coerceAtLeast(0)
    val minutes = safeSeconds / 60
    val seconds = safeSeconds % 60
    return "$minutes:${seconds.toString().padStart(2, '0')}"
}

@Composable
fun ForgotPasswordScreen(
    onBack: () -> Unit = {},
    onResetSent: () -> Unit = {}
) {
    val context = LocalContext.current.applicationContext
    val authRepository = remember(context) { TukiDataProvider(context).authRepository }
    val cooldownStore = remember(context) { ForgotPasswordOtpCooldownStore(context) }
    val coroutineScope = rememberCoroutineScope()

    var stage by remember { mutableStateOf(ForgotPasswordStage.EMAIL) }
    var email by remember { mutableStateOf(cooldownStore.restoredEmail()) }
    var code by remember { mutableStateOf("") }
    var newPassword by remember { mutableStateOf("") }
    var confirmPassword by remember { mutableStateOf("") }
    var cooldownUntilMillis by remember {
        mutableLongStateOf(cooldownStore.cooldownUntil(email))
    }
    var clockMillis by remember { mutableLongStateOf(System.currentTimeMillis()) }
    var isWorking by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var info by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(email) {
        cooldownUntilMillis = cooldownStore.cooldownUntil(email)
        clockMillis = System.currentTimeMillis()
    }

    LaunchedEffect(cooldownUntilMillis) {
        while (true) {
            val now = System.currentTimeMillis()
            clockMillis = now
            if (cooldownUntilMillis <= now) break
            delay(1_000)
        }
    }

    val resendSecondsRemaining = forgotPasswordResendSecondsRemaining(
        cooldownUntilMillis,
        clockMillis
    )
    val resendClockLabel = forgotPasswordResendLabel(resendSecondsRemaining)

    fun enterExistingOtpFlow(normalizedEmail: String) {
        email = normalizedEmail
        stage = ForgotPasswordStage.OTP
        code = ""
        error = null
        info = if (TukiInterfaceText.isFilipino) {
            "May naipadalang code na sa $normalizedEmail. Maaari kang mag-resend pag natapos ang $resendClockLabel."
        } else {
            "A code was already sent to $normalizedEmail. You can resend after $resendClockLabel."
        }
    }

    fun requestCode() {
        if (isWorking) return
        val normalizedEmail = normalizeCooldownEmail(email)
        if (normalizedEmail.isBlank() || !normalizedEmail.contains("@")) {
            error = if (TukiInterfaceText.isFilipino) "Maglagay ng valid na email address." else "Enter a valid email address."
            return
        }

        val existingCooldownUntil = cooldownStore.cooldownUntil(normalizedEmail)
        val existingSeconds = forgotPasswordResendSecondsRemaining(
            existingCooldownUntil,
            System.currentTimeMillis()
        )
        if (existingSeconds > 0) {
            cooldownUntilMillis = existingCooldownUntil
            clockMillis = System.currentTimeMillis()
            enterExistingOtpFlow(normalizedEmail)
            return
        }

        coroutineScope.launch {
            isWorking = true
            error = null
            info = null
            when (val result = authRepository.requestPasswordReset(normalizedEmail)) {
                is ApiResult.Success -> {
                    val now = System.currentTimeMillis()
                    email = normalizedEmail
                    cooldownUntilMillis = cooldownStore.start(normalizedEmail, now)
                    clockMillis = now
                    stage = ForgotPasswordStage.OTP
                    code = ""
                    info = if (TukiInterfaceText.isFilipino) {
                        "Nagpadala kami ng 8-digit code sa $normalizedEmail."
                    } else {
                        "We sent an 8-digit code to $normalizedEmail."
                    }
                }
                is ApiResult.Failure -> error = result.message
            }
            isWorking = false
        }
    }

    fun verifyCode() {
        if (isWorking) return
        if (code.length != 8) {
            error = if (TukiInterfaceText.isFilipino) "Ilagay ang kumpletong 8-digit code." else "Enter the complete 8-digit code."
            return
        }

        coroutineScope.launch {
            isWorking = true
            error = null
            info = null
            when (val result = authRepository.verifyPasswordResetOtp(email.trim(), code)) {
                is ApiResult.Success -> stage = ForgotPasswordStage.PASSWORD
                is ApiResult.Failure -> error = result.message
            }
            isWorking = false
        }
    }

    fun resetPassword() {
        if (isWorking) return
        when {
            newPassword.length < 8 -> error = if (TukiInterfaceText.isFilipino) "Dapat hindi bababa sa 8 character ang bagong password." else "New password must be at least 8 characters."
            newPassword != confirmPassword -> error = if (TukiInterfaceText.isFilipino) "Hindi magkapareho ang bagong password at confirmation." else "New password and confirmation do not match."
            else -> coroutineScope.launch {
                isWorking = true
                error = null
                info = null
                when (val result = authRepository.resetPassword(email.trim(), code, newPassword)) {
                    is ApiResult.Success -> {
                        cooldownStore.clear(email)
                        cooldownUntilMillis = 0L
                        clockMillis = System.currentTimeMillis()
                        info = if (TukiInterfaceText.isFilipino) "Matagumpay na na-reset ang password." else "Password reset successfully."
                        delay(800)
                        onResetSent()
                    }
                    is ApiResult.Failure -> error = result.message
                }
                isWorking = false
            }
        }
    }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(TukiCream)
            .statusBarsPadding()
            .padding(start = 34.dp, end = 34.dp, top = 12.dp, bottom = 28.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.Start) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                    .clickable(enabled = !isWorking) {
                        when (stage) {
                            ForgotPasswordStage.EMAIL -> onBack()
                            ForgotPasswordStage.OTP -> {
                                stage = ForgotPasswordStage.EMAIL
                                error = null
                                info = null
                            }
                            ForgotPasswordStage.PASSWORD -> {
                                stage = ForgotPasswordStage.OTP
                                error = null
                                info = null
                            }
                        }
                    },
                contentAlignment = Alignment.Center
            ) {
                Text(text = "‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
        }

        Spacer(modifier = Modifier.height(18.dp))
        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = "TUKI logo",
            modifier = Modifier.size(72.dp),
            contentScale = ContentScale.Fit
        )
        Text("TUKI.", color = TukiTeal, style = MaterialTheme.typography.displaySmall)

        Spacer(modifier = Modifier.height(28.dp))
        Text(
            text = when (stage) {
                ForgotPasswordStage.EMAIL -> TukiInterfaceText.resetPassword
                ForgotPasswordStage.OTP -> TukiInterfaceText.checkYourEmail
                ForgotPasswordStage.PASSWORD -> if (TukiInterfaceText.isFilipino) "Gumawa ng bagong password" else "Create new password"
            },
            color = TukiInk,
            style = MaterialTheme.typography.displaySmall
        )

        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = when (stage) {
                ForgotPasswordStage.EMAIL -> if (TukiInterfaceText.isFilipino) "Ilagay ang email na naka-link sa registered TUKI account mo." else "Enter the email linked to your registered TUKI account."
                ForgotPasswordStage.OTP -> if (TukiInterfaceText.isFilipino) "Nagpadala kami ng 8-digit OTP sa ${email.trim()}." else "We've sent an 8-digit OTP to ${email.trim()}."
                ForgotPasswordStage.PASSWORD -> if (TukiInterfaceText.isFilipino) "Na-verify na ang code mo. Pumili ng bagong password." else "Your code is verified. Choose a new password."
            },
            color = TukiMuted,
            style = MaterialTheme.typography.bodyLarge,
            modifier = Modifier.padding(horizontal = 8.dp)
        )

        Spacer(modifier = Modifier.height(30.dp))

        when (stage) {
            ForgotPasswordStage.EMAIL -> ResetTextField(
                label = TukiInterfaceText.email,
                value = email,
                enabled = !isWorking,
                trailingText = if (resendSecondsRemaining > 0) "Resend $resendClockLabel" else null,
                onValueChange = {
                    email = it
                    error = null
                    info = null
                }
            )
            ForgotPasswordStage.OTP -> {
                OtpCodeField(code = code, onCodeChange = { code = it; error = null }, enabled = !isWorking)
                Spacer(modifier = Modifier.height(10.dp))
                TextButton(
                    onClick = { requestCode() },
                    enabled = !isWorking && resendSecondsRemaining == 0
                ) {
                    Text(
                        text = if (resendSecondsRemaining > 0) {
                            "Resend in $resendClockLabel"
                        } else {
                            "Resend OTP"
                        },
                        color = if (!isWorking && resendSecondsRemaining == 0) TukiTeal else TukiMuted,
                        fontWeight = FontWeight.Bold
                    )
                }
            }
            ForgotPasswordStage.PASSWORD -> {
                ResetTextField(
                    label = TukiInterfaceText.newPassword,
                    value = newPassword,
                    enabled = !isWorking,
                    isPassword = true,
                    onValueChange = { newPassword = it; error = null }
                )
                Spacer(modifier = Modifier.height(18.dp))
                ResetTextField(
                    label = TukiInterfaceText.confirmNewPassword,
                    value = confirmPassword,
                    enabled = !isWorking,
                    isPassword = true,
                    onValueChange = { confirmPassword = it; error = null }
                )
            }
        }

        error?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(message, color = TukiDanger, style = MaterialTheme.typography.labelLarge)
        }
        info?.let { message ->
            Spacer(modifier = Modifier.height(14.dp))
            Text(message, color = TukiTeal, style = MaterialTheme.typography.labelLarge)
        }

        Spacer(modifier = Modifier.height(26.dp))
        Button(
            onClick = {
                when (stage) {
                    ForgotPasswordStage.EMAIL -> requestCode()
                    ForgotPasswordStage.OTP -> verifyCode()
                    ForgotPasswordStage.PASSWORD -> resetPassword()
                }
            },
            modifier = Modifier.fillMaxWidth().height(58.dp),
            enabled = !isWorking,
            shape = RoundedCornerShape(18.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isWorking) CircularProgressIndicator(color = Color.White, modifier = Modifier.size(22.dp))
            else {
                Text(
                    text = when (stage) {
                        ForgotPasswordStage.EMAIL -> if (resendSecondsRemaining > 0) {
                            if (TukiInterfaceText.isFilipino) "Ilagay ang OTP" else "Enter OTP"
                        } else {
                            TukiInterfaceText.sendOtp
                        }
                        ForgotPasswordStage.OTP -> TukiInterfaceText.verifyOtp
                        ForgotPasswordStage.PASSWORD -> TukiInterfaceText.resetPassword
                    },
                    style = MaterialTheme.typography.titleLarge
                )
            }
        }
    }
}

@Composable
private fun ResetTextField(
    label: String,
    value: String,
    enabled: Boolean,
    isPassword: Boolean = false,
    trailingText: String? = null,
    onValueChange: (String) -> Unit
) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(text = label, color = TukiInk, style = MaterialTheme.typography.titleMedium)
        Spacer(modifier = Modifier.height(8.dp))
        TextField(
            value = value,
            onValueChange = onValueChange,
            modifier = Modifier.fillMaxWidth().height(58.dp),
            enabled = enabled,
            singleLine = true,
            shape = RoundedCornerShape(15.dp),
            trailingIcon = if (trailingText is null) {
                null
            } else {
                {
                    Text(
                        text = trailingText,
                        color = TukiMuted,
                        style = MaterialTheme.typography.labelSmall
                    )
                }
            },
            visualTransformation = if (isPassword) PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
            colors = TextFieldDefaults.colors(
                focusedContainerColor = TukiSurfaceRaised,
                unfocusedContainerColor = TukiSurfaceRaised,
                disabledContainerColor = TukiSurfaceRaised.copy(alpha = 0.65f),
                focusedIndicatorColor = Color.Transparent,
                unfocusedIndicatorColor = Color.Transparent,
                disabledIndicatorColor = Color.Transparent,
                focusedTextColor = TukiInk,
                unfocusedTextColor = TukiInk
            ),
            textStyle = MaterialTheme.typography.bodyLarge
        )
    }
}
