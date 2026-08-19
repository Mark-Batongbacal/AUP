package com.example.frontend.screens

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

private val TukiTeal = Color(0xFF15919B)
private val TukiOrange = Color(0xFFFF9318)
private val TukiCream = Color(0xFFFFF8E8)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiError = Color(0xFFB00020)

@Composable
fun ForgotPasswordScreen(
    onBack: () -> Unit = {},
    onResetSent: () -> Unit = {}
) {
    var email by remember { mutableStateOf("") }
    var isSending by remember { mutableStateOf(false) }
    var error by remember { mutableStateOf<String?>(null) }
    var isSuccess by remember { mutableStateOf(false) }
    
    val coroutineScope = rememberCoroutineScope()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .background(Color.White)
            .padding(start = 34.dp, end = 34.dp, top = 35.dp, bottom = 15.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Back Button
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Start
        ) {
            Box(
                modifier = Modifier
                    .size(38.dp)
                    .background(TukiCream, RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(text = "\u2039", color = TukiDark, fontSize = 22.sp, fontWeight = FontWeight.Bold)
            }
        }

        Spacer(modifier = Modifier.height(20.dp))

        Image(
            painter = painterResource(R.drawable.tuki_logo),
            contentDescription = "TUKI logo",
            modifier = Modifier.size(75.dp),
            contentScale = ContentScale.Fit
        )

        Text(
            text = "TUKI.",
            color = TukiTeal,
            fontSize = 34.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(35.dp))

        Text(
            text = "Reset Password",
            color = Color.Black,
            fontSize = 26.sp,
            fontWeight = FontWeight.ExtraBold
        )

        Spacer(modifier = Modifier.height(8.dp))

        Text(
            text = "Enter your email to receive a reset link",
            color = TukiGray,
            fontSize = 18.sp,
            fontWeight = FontWeight.SemiBold,
            modifier = Modifier.padding(horizontal = 20.dp),
            textAlign = androidx.compose.ui.text.style.TextAlign.Center
        )

        Spacer(modifier = Modifier.height(40.dp))

        Column(modifier = Modifier.fillMaxWidth()) {
            Text(text = "Email", color = Color.Black, fontSize = 18.sp)
            Spacer(modifier = Modifier.height(8.dp))

            TextField(
                value = email,
                onValueChange = {
                    email = it
                    error = null
                },
                modifier = Modifier.fillMaxWidth().height(60.dp),
                enabled = !isSending && !isSuccess,
                singleLine = true,
                shape = RoundedCornerShape(15.dp),
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = TukiCream,
                    unfocusedContainerColor = TukiCream,
                    disabledContainerColor = TukiCream,
                    focusedIndicatorColor = Color.Transparent,
                    unfocusedIndicatorColor = Color.Transparent,
                    disabledIndicatorColor = Color.Transparent
                )
            )
        }

        if (error != null) {
            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = error!!,
                color = TukiError,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )
        }

        if (isSuccess) {
            Spacer(modifier = Modifier.height(12.dp))
            Text(
                text = "Reset link sent! Check your inbox.",
                color = TukiTeal,
                fontSize = 14.sp,
                fontWeight = FontWeight.SemiBold
            )
        }

        Spacer(modifier = Modifier.height(40.dp))

        Button(
            onClick = {
                if (email.isBlank() || !email.contains("@")) {
                    error = "Enter a valid email address."
                    return@Button
                }
                coroutineScope.launch {
                    isSending = true
                    error = null
                    // Mock API call
                    delay(1500)
                    isSending = false
                    isSuccess = true
                    delay(2000)
                    onResetSent()
                }
            },
            modifier = Modifier.fillMaxWidth().height(60.dp),
            enabled = !isSending && !isSuccess,
            shape = RoundedCornerShape(22.dp),
            colors = ButtonDefaults.buttonColors(containerColor = TukiOrange, contentColor = Color.White)
        ) {
            if (isSending) {
                CircularProgressIndicator(color = Color.White, modifier = Modifier.size(24.dp))
            } else {
                Text(
                    text = if (isSuccess) "Sent!" else "Send Reset Link",
                    fontSize = 20.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}
