package com.example.frontend.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.weight
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.data.places.DestinationSearchResultDto
import kotlinx.coroutines.delay
import java.math.BigDecimal

private val TripSheetScreen = Color(0xFFF8F5EC)
private val TripSheetSurface = Color(0xFFFFFBF0)
private val TripSheetTile = Color(0xFFF5F1E7)
private val TripSheetCream = Color(0xFFFFF0C7)
private val TripSheetTeal = Color(0xFF2C8E95)
private val TripSheetDark = Color(0xFF153E4B)
private val TripSheetDarkText = Color(0xFF244B58)
private val TripSheetMuted = Color(0xFF7A898E)
private val TripSheetOrange = Color(0xFFF59A3A)
private val TripSheetOutline = Color(0xFFDCD5C7)

private enum class TripOptionEditor { Preference, Budget, Destination }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TripOptionsSheet(
    isWorking: Boolean,
    onDismiss: () -> Unit,
    onRerouteNow: () -> Unit,
    onPreferenceChange: (String) -> Unit,
    onBudgetChange: (BigDecimal?, Boolean) -> Unit,
    onDestinationSearch: suspend (String) -> List<DestinationSearchResultDto>,
    onDestinationChange: (DestinationSearchResultDto) -> Unit
) {
    var editor by remember { mutableStateOf<TripOptionEditor?>(null) }

    if (editor == null) {
        ModalBottomSheet(
            onDismissRequest = onDismiss,
            containerColor = TripSheetScreen,
            contentColor = TripSheetDark,
            shape = RoundedCornerShape(topStart = 30.dp, topEnd = 30.dp),
            dragHandle = { TukiSheetHandle() }
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 22.dp)
                    .padding(bottom = 30.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Text(
                    "Trip options",
                    color = TripSheetDark,
                    fontSize = 25.sp,
                    fontWeight = FontWeight.ExtraBold
                )
                Text(
                    "Update your active trip without starting over.",
                    color = TripSheetMuted,
                    fontSize = 14.sp,
                    fontWeight = FontWeight.Medium
                )

                Spacer(Modifier.height(6.dp))

                TripOptionRow(
                    icon = "↻",
                    title = "Reroute now",
                    subtitle = "Find a new route from your current location.",
                    disabled = isWorking
                ) {
                    onDismiss()
                    onRerouteNow()
                }

                TripOptionRow(
                    icon = "☷",
                    title = "Change route preference",
                    subtitle = "Choose fastest, cheapest, or balanced.",
                    disabled = isWorking
                ) {
                    editor = TripOptionEditor.Preference
                }

                TripOptionRow(
                    icon = "₱",
                    title = "Change budget",
                    subtitle = "Set or remove your maximum fare budget.",
                    disabled = isWorking
                ) {
                    editor = TripOptionEditor.Budget
                }

                TripOptionRow(
                    icon = "⌖",
                    title = "Change destination",
                    subtitle = "Search for a new destination and reroute.",
                    disabled = isWorking
                ) {
                    editor = TripOptionEditor.Destination
                }

                Spacer(Modifier.height(10.dp))
            }
        }
    }

    when (editor) {
        TripOptionEditor.Preference -> PreferenceSheet(
            onDismiss = { editor = null },
            onConfirm = { preference ->
                editor = null
                onDismiss()
                onPreferenceChange(preference)
            }
        )
        TripOptionEditor.Budget -> BudgetSheet(
            onDismiss = { editor = null },
            onConfirm = { budget, clear ->
                editor = null
                onDismiss()
                onBudgetChange(budget, clear)
            }
        )
        TripOptionEditor.Destination -> DestinationSheet(
            onDismiss = { editor = null },
            onSearch = onDestinationSearch,
            onConfirm = { destination ->
                editor = null
                onDismiss()
                onDestinationChange(destination)
            }
        )
        null -> Unit
    }
}

@Composable
private fun TripOptionRow(
    icon: String,
    title: String,
    subtitle: String,
    disabled: Boolean,
    onClick: () -> Unit
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(enabled = !disabled, onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = TripSheetSurface,
        border = BorderStroke(1.dp, TripSheetOutline),
        shadowElevation = 1.dp
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Surface(
                modifier = Modifier.size(42.dp),
                shape = CircleShape,
                color = TripSheetCream.copy(alpha = 0.55f)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(icon, color = TripSheetTeal, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                }
            }
            Spacer(Modifier.width(13.dp))
            Column(Modifier.weight(1f)) {
                Text(title, color = TripSheetDark, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
                Spacer(Modifier.height(2.dp))
                Text(
                    subtitle,
                    color = TripSheetMuted,
                    fontSize = 12.sp,
                    lineHeight = 16.sp,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis
                )
            }
            Spacer(Modifier.width(8.dp))
            Text("›", color = TripSheetDarkText, fontSize = 26.sp, fontWeight = FontWeight.Medium)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun PreferenceSheet(onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var selected by remember { mutableStateOf("efficient") }
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = TripSheetScreen,
        contentColor = TripSheetDark,
        shape = RoundedCornerShape(topStart = 30.dp, topEnd = 30.dp),
        dragHandle = { TukiSheetHandle() }
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 20.dp)
                .padding(bottom = 30.dp)
        ) {
            Text("Choose route preference", color = TripSheetDark, fontSize = 24.sp, fontWeight = FontWeight.ExtraBold)
            Spacer(Modifier.height(4.dp))
            Text("Pick the route that works best for you.", color = TripSheetMuted, fontSize = 13.sp)
            Spacer(Modifier.height(16.dp))

            PreferenceCard(
                icon = "🛺",
                title = "Best Overall",
                subtitle = "Balanced time and cost",
                recommended = true,
                selected = selected == "efficient",
                stats = listOf("ETA" to "Balanced", "Fare" to "Balanced", "Walk" to "Balanced"),
                onClick = {
                    selected = "efficient"
                    onConfirm("efficient")
                }
            )
            Spacer(Modifier.height(10.dp))
            PreferenceCard(
                icon = "₱",
                title = "Cheapest",
                subtitle = "Lowest fare options",
                recommended = false,
                selected = selected == "cheapest",
                stats = listOf("ETA" to "Flexible", "Fare" to "Lowest", "Walk" to "Varies"),
                onClick = {
                    selected = "cheapest"
                    onConfirm("cheapest")
                }
            )
            Spacer(Modifier.height(10.dp))
            PreferenceCard(
                icon = "⚡",
                title = "Fastest",
                subtitle = "Quickest arrival",
                recommended = false,
                selected = selected == "fastest",
                stats = listOf("ETA" to "Lowest", "Fare" to "Varies", "Walk" to "Varies"),
                onClick = {
                    selected = "fastest"
                    onConfirm("fastest")
                }
            )
        }
    }
}

@Composable
private fun PreferenceCard(
    icon: String,
    title: String,
    subtitle: String,
    recommended: Boolean,
    selected: Boolean,
    stats: List<Pair<String, String>>,
    onClick: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
        shape = RoundedCornerShape(18.dp),
        color = TripSheetSurface,
        border = BorderStroke(if (selected) 2.dp else 1.dp, if (selected) TripSheetTeal else TripSheetOutline),
        shadowElevation = if (selected) 4.dp else 1.dp
    ) {
        Column(Modifier.padding(14.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Surface(
                    modifier = Modifier.size(42.dp),
                    shape = CircleShape,
                    color = when (title) {
                        "Fastest" -> TripSheetOrange.copy(alpha = 0.16f)
                        "Cheapest" -> Color(0xFFE7F1D8)
                        else -> Color(0xFFE5F1ED)
                    }
                ) {
                    Box(contentAlignment = Alignment.Center) { Text(icon, fontSize = 20.sp, color = TripSheetDark) }
                }
                Spacer(Modifier.width(11.dp))
                Column(Modifier.weight(1f)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(title, color = TripSheetDark, fontSize = 15.sp, fontWeight = FontWeight.ExtraBold)
                        if (recommended) {
                            Spacer(Modifier.width(7.dp))
                            Surface(shape = RoundedCornerShape(10.dp), color = TripSheetOrange.copy(alpha = 0.12f)) {
                                Text(
                                    "★ Recommended",
                                    modifier = Modifier.padding(horizontal = 7.dp, vertical = 4.dp),
                                    color = TripSheetOrange,
                                    fontSize = 9.sp,
                                    fontWeight = FontWeight.Bold
                                )
                            }
                        }
                    }
                    Text(subtitle, color = TripSheetMuted, fontSize = 11.sp, fontWeight = FontWeight.Medium)
                }
                Surface(
                    modifier = Modifier.size(28.dp),
                    shape = CircleShape,
                    color = if (selected) TripSheetTeal else Color.Transparent,
                    border = if (selected) null else BorderStroke(1.5.dp, TripSheetOutline)
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        if (selected) Text("✓", color = Color.White, fontSize = 16.sp, fontWeight = FontWeight.Bold)
                    }
                }
            }
            Spacer(Modifier.height(11.dp))
            Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                stats.forEach { (label, value) ->
                    PreferenceStat(label, value, Modifier.weight(1f))
                }
            }
        }
    }
}

@Composable
private fun PreferenceStat(label: String, value: String, modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .background(TripSheetTile, RoundedCornerShape(12.dp))
            .padding(horizontal = 6.dp, vertical = 8.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(label, color = TripSheetMuted, fontSize = 8.sp, fontWeight = FontWeight.Bold)
        Text(value, color = TripSheetDark, fontSize = 11.sp, fontWeight = FontWeight.ExtraBold, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun BudgetSheet(onDismiss: () -> Unit, onConfirm: (BigDecimal?, Boolean) -> Unit) {
    var text by remember { mutableStateOf("") }
    val parsed = text.trim().toBigDecimalOrNull()
    val valid = parsed != null && parsed > BigDecimal.ZERO
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = TripSheetScreen,
        contentColor = TripSheetDark,
        shape = RoundedCornerShape(topStart = 30.dp, topEnd = 30.dp),
        dragHandle = { TukiSheetHandle() }
    ) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 22.dp).padding(bottom = 30.dp)) {
            Text("Change budget", color = TripSheetDark, fontSize = 24.sp, fontWeight = FontWeight.ExtraBold)
            Spacer(Modifier.height(5.dp))
            Text("Set the maximum total fare TUKI should consider for the remaining trip.", color = TripSheetMuted, fontSize = 13.sp)
            Spacer(Modifier.height(16.dp))
            OutlinedTextField(
                value = text,
                onValueChange = { value -> text = value.filter { it.isDigit() || it == '.' } },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Budget (₱)") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                shape = RoundedCornerShape(16.dp)
            )
            Spacer(Modifier.height(12.dp))
            Button(
                onClick = { if (valid) onConfirm(parsed, false) },
                enabled = valid,
                modifier = Modifier.fillMaxWidth().height(48.dp),
                colors = ButtonDefaults.buttonColors(containerColor = TripSheetTeal),
                shape = RoundedCornerShape(16.dp)
            ) { Text("Apply budget", fontWeight = FontWeight.Bold) }
            Spacer(Modifier.height(8.dp))
            Surface(
                modifier = Modifier.fillMaxWidth().clickable { onConfirm(null, true) },
                shape = RoundedCornerShape(16.dp),
                color = TripSheetCream.copy(alpha = 0.38f)
            ) {
                Box(modifier = Modifier.padding(vertical = 13.dp), contentAlignment = Alignment.Center) {
                    Text("Remove budget limit", color = TripSheetOrange, fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DestinationSheet(
    onDismiss: () -> Unit,
    onSearch: suspend (String) -> List<DestinationSearchResultDto>,
    onConfirm: (DestinationSearchResultDto) -> Unit
) {
    var query by remember { mutableStateOf("") }
    var results by remember { mutableStateOf<List<DestinationSearchResultDto>>(emptyList()) }
    var loading by remember { mutableStateOf(false) }

    LaunchedEffect(query) {
        val text = query.trim()
        if (text.length < 2) {
            results = emptyList()
            loading = false
            return@LaunchedEffect
        }
        delay(300)
        loading = true
        results = onSearch(text).take(5)
        loading = false
    }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        containerColor = TripSheetScreen,
        contentColor = TripSheetDark,
        shape = RoundedCornerShape(topStart = 30.dp, topEnd = 30.dp),
        dragHandle = { TukiSheetHandle() }
    ) {
        Column(modifier = Modifier.fillMaxWidth().padding(horizontal = 22.dp).padding(bottom = 30.dp)) {
            Text("Change destination", color = TripSheetDark, fontSize = 24.sp, fontWeight = FontWeight.ExtraBold)
            Spacer(Modifier.height(5.dp))
            Text("Search for a new destination and TUKI will reroute from your current location.", color = TripSheetMuted, fontSize = 13.sp)
            Spacer(Modifier.height(14.dp))
            OutlinedTextField(
                value = query,
                onValueChange = { query = it },
                modifier = Modifier.fillMaxWidth(),
                label = { Text("New destination") },
                singleLine = true,
                shape = RoundedCornerShape(16.dp)
            )
            if (loading) {
                Spacer(Modifier.height(10.dp))
                LinearProgressIndicator(modifier = Modifier.fillMaxWidth(), color = TripSheetTeal, trackColor = TripSheetTeal.copy(alpha = 0.10f))
            }
            Spacer(Modifier.height(10.dp))
            results.forEach { place ->
                Surface(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp).clickable { onConfirm(place) },
                    shape = RoundedCornerShape(16.dp),
                    color = TripSheetSurface,
                    border = BorderStroke(1.dp, TripSheetOutline)
                ) {
                    Row(modifier = Modifier.padding(horizontal = 14.dp, vertical = 12.dp), verticalAlignment = Alignment.CenterVertically) {
                        Surface(modifier = Modifier.size(36.dp), shape = CircleShape, color = TripSheetCream.copy(alpha = 0.50f)) {
                            Box(contentAlignment = Alignment.Center) {
                                Text("⌖", color = TripSheetTeal, fontSize = 18.sp, fontWeight = FontWeight.Bold)
                            }
                        }
                        Spacer(Modifier.width(11.dp))
                        Column(Modifier.weight(1f)) {
                            Text(place.name, color = TripSheetDark, fontSize = 14.sp, fontWeight = FontWeight.ExtraBold)
                            place.address?.takeIf { it.isNotBlank() }?.let { address ->
                                Text(address, color = TripSheetMuted, fontSize = 11.sp, maxLines = 2, overflow = TextOverflow.Ellipsis)
                            }
                        }
                        Text("›", color = TripSheetDarkText, fontSize = 24.sp)
                    }
                }
            }
            if (!loading && query.trim().length >= 2 && results.isEmpty()) {
                Surface(modifier = Modifier.fillMaxWidth(), shape = RoundedCornerShape(16.dp), color = TripSheetTile) {
                    Text("No matching destinations found.", modifier = Modifier.padding(14.dp), color = TripSheetMuted, fontSize = 12.sp)
                }
            }
        }
    }
}

@Composable
private fun TukiSheetHandle() {
    Box(
        modifier = Modifier
            .padding(top = 10.dp, bottom = 8.dp)
            .width(38.dp)
            .height(4.dp)
            .background(TripSheetMuted.copy(alpha = 0.55f), RoundedCornerShape(4.dp))
    )
}
