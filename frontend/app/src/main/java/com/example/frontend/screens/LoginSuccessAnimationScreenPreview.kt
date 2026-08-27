package com.example.frontend.screens

import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.tooling.preview.Preview

@Preview(showBackground = true)
@Composable
fun LoginSuccessAnimationScreenPreview() {
    MaterialTheme {
        LoginSuccessAnimationScreen(onAnimationComplete = {})
    }
}
