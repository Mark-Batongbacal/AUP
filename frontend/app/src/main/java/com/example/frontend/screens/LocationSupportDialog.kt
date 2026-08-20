package com.example.frontend.screens

import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import com.example.frontend.core.location.LocationNotSupportedMessage
import com.example.frontend.core.location.LocationNotSupportedTitle

@Composable
fun LocationNotSupportedDialog(onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(LocationNotSupportedTitle) },
        text = { Text(LocationNotSupportedMessage) },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("OK")
            }
        }
    )
}
