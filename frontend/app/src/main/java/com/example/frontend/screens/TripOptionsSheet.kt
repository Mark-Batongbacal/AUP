package com.example.frontend.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Divider
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import java.math.BigDecimal

private val TripOptionsTeal = Color(0xFF15919B)
private val TripOptionsOrange = Color(0xFFFF9318)
private val TripOptionsDark = Color(0xFF173B43)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TripOptionsSheet(
    isWorking: Boolean,
    onDismiss: () -> Unit,
    onRerouteNow: () -> Unit,
    onPreferenceChange: (String) -> Unit,
    onBudgetChange: (BigDecimal?, Boolean) -> Unit,
    onDestinationChange: (String) -> Unit,
    onEndTrip: () -> Unit
) {
    var editor by remember { mutableStateOf<TripOptionEditor?>(null) }

    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 24.dp)
                .padding(bottom = 28.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text("Trip options", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.ExtraBold, color = TripOptionsDark)
            Text("Update your active trip without starting over.", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(6.dp))

            TripOptionButton("↻", "Reroute now", "Find a new route from your current location.", isWorking) {
                onDismiss(); onRerouteNow()
            }
            TripOptionButton("⇄", "Change route preference", "Choose fastest, cheapest, or balanced.", isWorking) {
                editor = TripOptionEditor.Preference
            }
            TripOptionButton("₱", "Change budget", "Set or remove your maximum fare budget.", isWorking) {
                editor = TripOptionEditor.Budget
            }
            TripOptionButton("⌖", "Change destination", "Search for a new destination and reroute.", isWorking) {
                editor = TripOptionEditor.Destination
            }

            Divider(Modifier.padding(vertical = 6.dp))
            Button(
                onClick = { onDismiss(); onEndTrip() },
                enabled = !isWorking,
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                shape = RoundedCornerShape(14.dp)
            ) { Text("End trip", fontWeight = FontWeight.Bold) }
        }
    }

    when (editor) {
        TripOptionEditor.Preference -> PreferenceDialog(
            onDismiss = { editor = null },
            onConfirm = { preference -> editor = null; onDismiss(); onPreferenceChange(preference) }
        )
        TripOptionEditor.Budget -> BudgetDialog(
            onDismiss = { editor = null },
            onConfirm = { budget, clear -> editor = null; onDismiss(); onBudgetChange(budget, clear) }
        )
        TripOptionEditor.Destination -> DestinationDialog(
            onDismiss = { editor = null },
            onConfirm = { query -> editor = null; onDismiss(); onDestinationChange(query) }
        )
        null -> Unit
    }
}

@Composable
private fun TripOptionButton(icon: String, title: String, subtitle: String, disabled: Boolean, onClick: () -> Unit) {
    OutlinedButton(
        onClick = onClick,
        enabled = !disabled,
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(14.dp)
    ) {
        Row(Modifier.fillMaxWidth().padding(vertical = 5.dp), verticalAlignment = Alignment.CenterVertically) {
            Text(icon, color = TripOptionsTeal, style = MaterialTheme.typography.titleLarge)
            Column(Modifier.padding(start = 14.dp).weight(1f)) {
                Text(title, fontWeight = FontWeight.Bold, color = TripOptionsDark)
                Text(subtitle, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Composable
private fun PreferenceDialog(onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var selected by remember { mutableStateOf("efficient") }
    val choices = listOf(
        "fastest" to "Fastest",
        "cheapest" to "Cheapest",
        "efficient" to "Balanced"
    )
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Route preference") },
        text = {
            Column {
                choices.forEach { (value, label) ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        RadioButton(selected = selected == value, onClick = { selected = value })
                        Text(label)
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = { onConfirm(selected) }) { Text("Apply") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun BudgetDialog(onDismiss: () -> Unit, onConfirm: (BigDecimal?, Boolean) -> Unit) {
    var text by remember { mutableStateOf("") }
    val parsed = text.trim().toBigDecimalOrNull()
    val valid = parsed != null && parsed > BigDecimal.ZERO
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Change budget") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Enter the maximum total fare TUKI should consider for the remaining trip.")
                OutlinedTextField(
                    value = text,
                    onValueChange = { value -> text = value.filter { it.isDigit() || it == '.' } },
                    label = { Text("Budget (₱)") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal)
                )
                TextButton(onClick = { onConfirm(null, true) }) { Text("Remove budget limit", color = TripOptionsOrange) }
            }
        },
        confirmButton = { TextButton(onClick = { if (valid) onConfirm(parsed, false) }, enabled = valid) { Text("Apply") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun DestinationDialog(onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var query by remember { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Change destination") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Enter your new destination. TUKI will search from your current location.")
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("New destination") },
                    singleLine = true
                )
            }
        },
        confirmButton = {
            TextButton(onClick = { onConfirm(query.trim()) }, enabled = query.trim().length >= 2) { Text("Find & reroute") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

private enum class TripOptionEditor { Preference, Budget, Destination }
