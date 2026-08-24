package com.example.frontend.screens

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.frontend.R
import com.example.frontend.core.localization.TukiInterfaceText
import com.example.frontend.ui.theme.TukiCream
import com.example.frontend.ui.theme.TukiDanger
import com.example.frontend.ui.theme.TukiInk
import com.example.frontend.ui.theme.TukiMuted
import com.example.frontend.ui.theme.TukiOrange
import com.example.frontend.ui.theme.TukiSurfaceRaised
import com.example.frontend.ui.theme.TukiTeal

private val SupportIconInk = Color(0xFF153E4B)
private val SupportIconSurface = Color(0xFFFFF0D5)

private const val FeedbackEmailStephen = "pinacate.stephen@gmail.com"
private const val FeedbackEmailMark = "batongbacalmark@gmail.com"

private fun supportCopy(english: String, filipino: String): String =
    if (TukiInterfaceText.isFilipino) filipino else english

@Composable
fun HelpCenterScreen(onBack: () -> Unit) {
    val faqItems = remember(TukiInterfaceText.isFilipino) {
        listOf(
            supportCopy("How do I plan a trip?", "Paano ako magpaplano ng biyahe?") to
                supportCopy(
                    "From Home, set your current location and destination, then choose Find Routes. TUKI will show available commute options when route data is available.",
                    "Sa Home, itakda ang kasalukuyang lokasyon at destinasyon, pagkatapos piliin ang Maghanap ng Ruta. Ipapakita ng TUKI ang available na commute options kapag may route data."
                ),
            supportCopy("How do Favorites work?", "Paano gumagana ang Favorites?") to
                supportCopy(
                    "Tap the star on a route to save it. Your saved routes appear in Favorites for quicker access later.",
                    "I-tap ang bituin sa isang ruta para i-save ito. Lalabas ang mga naka-save mong ruta sa Favorites para mas mabilis itong balikan."
                ),
            supportCopy("What appears in Recent Trips?", "Ano ang makikita sa Recent Trips?") to
                supportCopy(
                    "Recent Trips shows your saved journey history and its status, such as completed or cancelled trips.",
                    "Ipinapakita ng Recent Trips ang iyong journey history at status nito, gaya ng natapos o kinanselang biyahe."
                ),
            supportCopy("How do I change the app language?", "Paano palitan ang wika ng app?") to
                supportCopy(
                    "Open Profile, tap Language, choose English or Filipino, then save your selection.",
                    "Buksan ang Profile, i-tap ang Language, piliin ang English o Filipino, at i-save ang napili."
                ),
            supportCopy("How do I switch Light and Dark Mode?", "Paano magpalit ng Light at Dark Mode?") to
                supportCopy(
                    "Open Profile > Settings and use the Dark Mode switch under Appearance.",
                    "Buksan ang Profile > Settings at gamitin ang Dark Mode switch sa Appearance."
                )
        )
    }
    var expandedIndex by remember { mutableStateOf<Int?>(null) }

    SupportPageScaffold(
        title = supportCopy("Help Center", "Help Center"),
        onBack = onBack
    ) {
        item {
            Text(
                supportCopy(
                    "Find quick answers about using TUKI.",
                    "Makahanap ng mabilis na sagot tungkol sa paggamit ng TUKI."
                ),
                color = TukiMuted,
                style = MaterialTheme.typography.bodyMedium
            )
            Spacer(Modifier.height(18.dp))
        }

        faqItems.forEachIndexed { index, (question, answer) ->
            item {
                Surface(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable {
                            expandedIndex = if (expandedIndex == index) null else index
                        },
                    shape = RoundedCornerShape(16.dp),
                    color = TukiSurfaceRaised
                ) {
                    Column(Modifier.padding(horizontal = 16.dp, vertical = 15.dp)) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Box(
                                modifier = Modifier
                                    .size(36.dp)
                                    .background(SupportIconSurface, RoundedCornerShape(11.dp)),
                                contentAlignment = Alignment.Center
                            ) {
                                Text("?", color = SupportIconInk, fontWeight = FontWeight.Bold)
                            }
                            Spacer(Modifier.width(12.dp))
                            Text(
                                question,
                                modifier = Modifier.weight(1f),
                                color = TukiInk,
                                style = MaterialTheme.typography.titleMedium
                            )
                            Text(
                                if (expandedIndex == index) "⌃" else "⌄",
                                color = TukiMuted,
                                fontSize = 18.sp
                            )
                        }
                        if (expandedIndex == index) {
                            Spacer(Modifier.height(12.dp))
                            Text(answer, color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
                        }
                    }
                }
                Spacer(Modifier.height(10.dp))
            }
        }

        item {
            Spacer(Modifier.height(8.dp))
            Surface(
                modifier = Modifier.fillMaxWidth(),
                shape = RoundedCornerShape(16.dp),
                color = TukiTeal.copy(alpha = 0.10f)
            ) {
                Text(
                    supportCopy(
                        "Still need help? Go back to Settings and choose Send Feedback.",
                        "Kailangan pa ng tulong? Bumalik sa Settings at piliin ang Send Feedback."
                    ),
                    modifier = Modifier.padding(16.dp),
                    color = TukiInk,
                    style = MaterialTheme.typography.bodyMedium
                )
            }
        }
    }
}

@Composable
fun SendFeedbackScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val categories = listOf(
        supportCopy("General", "Pangkalahatan"),
        supportCopy("Routes", "Mga Ruta"),
        supportCopy("App issue", "Problema sa App"),
        supportCopy("Suggestion", "Mungkahi")
    )
    var selectedCategory by remember { mutableStateOf(categories.first()) }
    var message by remember { mutableStateOf("") }
    var shareError by remember { mutableStateOf<String?>(null) }
    val canSend = message.trim().length >= 10

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .imePadding()
    ) {
        Row(
            modifier = Modifier.padding(start = 24.dp, end = 24.dp, top = 14.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(Modifier.width(14.dp))
            Text(
                TukiInterfaceText.sendFeedback,
                color = TukiInk,
                style = MaterialTheme.typography.displaySmall
            )
        }

        Spacer(Modifier.height(22.dp))

        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
            contentPadding = PaddingValues(start = 24.dp, end = 24.dp, bottom = 16.dp)
        ) {
            item {
                Text(
                    supportCopy(
                        "Tell us what worked, what went wrong, or what you would like TUKI to improve.",
                        "Ibahagi kung ano ang gumana, ano ang naging problema, o ano ang gusto mong mapahusay sa TUKI."
                    ),
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodyMedium
                )
                Spacer(Modifier.height(20.dp))

                Text(
                    supportCopy("CATEGORY", "KATEGORYA"),
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    letterSpacing = 1.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(Modifier.height(10.dp))
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    categories.chunked(2).forEach { rowItems ->
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            rowItems.forEach { category ->
                                Surface(
                                    modifier = Modifier
                                        .weight(1f)
                                        .clickable { selectedCategory = category },
                                    shape = RoundedCornerShape(14.dp),
                                    color = if (selectedCategory == category) TukiTeal else TukiSurfaceRaised
                                ) {
                                    Text(
                                        category,
                                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 11.dp),
                                        color = if (selectedCategory == category) Color.White else TukiInk,
                                        style = MaterialTheme.typography.labelLarge
                                    )
                                }
                            }
                            if (rowItems.size == 1) Spacer(Modifier.weight(1f))
                        }
                    }
                }

                Spacer(Modifier.height(20.dp))
                Text(
                    supportCopy("YOUR FEEDBACK", "IYONG FEEDBACK"),
                    color = TukiInk,
                    style = MaterialTheme.typography.labelSmall,
                    letterSpacing = 1.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(Modifier.height(10.dp))
                TextField(
                    value = message,
                    onValueChange = {
                        message = it
                        shareError = null
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(160.dp),
                    placeholder = {
                        Text(
                            supportCopy(
                                "Describe your experience or suggestion...",
                                "Ilarawan ang iyong karanasan o mungkahi..."
                            ),
                            color = TukiMuted
                        )
                    },
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = TukiSurfaceRaised,
                        unfocusedContainerColor = TukiSurfaceRaised,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        focusedTextColor = TukiInk,
                        unfocusedTextColor = TukiInk
                    ),
                    shape = RoundedCornerShape(16.dp)
                )

                Spacer(Modifier.height(8.dp))
                Text(
                    supportCopy(
                        "Send Feedback opens your email app with both TUKI feedback recipients already filled in.",
                        "Bubuksan ng Send Feedback ang email app na nakalagay na ang dalawang TUKI feedback recipients."
                    ),
                    color = TukiMuted,
                    style = MaterialTheme.typography.bodySmall
                )

                shareError?.let { error ->
                    Spacer(Modifier.height(10.dp))
                    Text(error, color = TukiDanger, style = MaterialTheme.typography.bodySmall)
                }
            }
        }

        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(TukiCream)
                .navigationBarsPadding()
                .padding(start = 24.dp, end = 24.dp, top = 8.dp, bottom = 16.dp)
        ) {
            Button(
                enabled = canSend,
                onClick = {
                    val feedbackText = buildString {
                        appendLine("TUKI Feedback")
                        appendLine("Category: $selectedCategory")
                        appendLine()
                        append(message.trim())
                    }
                    val subject = "TUKI Feedback - $selectedCategory"
                    val mailToUri = Uri.parse(
                        "mailto:$FeedbackEmailStephen,$FeedbackEmailMark" +
                            "?subject=${Uri.encode(subject)}" +
                            "&body=${Uri.encode(feedbackText)}"
                    )
                    val intent = Intent(Intent.ACTION_SENDTO, mailToUri)

                    runCatching {
                        context.startActivity(
                            Intent.createChooser(
                                intent,
                                supportCopy("Send TUKI feedback", "Ipadala ang TUKI feedback")
                            )
                        )
                    }.onFailure {
                        shareError = supportCopy(
                            "No compatible email app was found on this device.",
                            "Walang compatible na email app na nakita sa device na ito."
                        )
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(54.dp),
                shape = RoundedCornerShape(18.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = TukiOrange,
                    contentColor = Color.White,
                    disabledContainerColor = TukiOrange.copy(alpha = 0.35f),
                    disabledContentColor = Color.White.copy(alpha = 0.75f)
                )
            ) {
                Text(TukiInterfaceText.sendFeedback, fontWeight = FontWeight.Bold)
            }
        }
    }
}

@Composable
fun AboutTukiScreen(onBack: () -> Unit) {
    SupportPageScaffold(
        title = TukiInterfaceText.aboutTuki,
        onBack = onBack
    ) {
        item {
            Column(
                modifier = Modifier.fillMaxWidth(),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Image(
                    painter = painterResource(R.drawable.tuki_logo),
                    contentDescription = "TUKI logo",
                    modifier = Modifier.size(86.dp)
                )
                Spacer(Modifier.height(8.dp))
                Text("TUKI.", color = TukiTeal, style = MaterialTheme.typography.displaySmall)
                Text("Version 1.0.0", color = TukiMuted, style = MaterialTheme.typography.bodySmall)
            }
            Spacer(Modifier.height(26.dp))

            AboutInfoCard(
                title = supportCopy("About the app", "Tungkol sa app"),
                body = supportCopy(
                    "TUKI is a smart commuting companion designed to help passengers compare and follow practical public-transport journeys using walking, tricycle, and jeepney options when route data is available.",
                    "Ang TUKI ay smart commuting companion na tumutulong sa mga pasahero na ikumpara at sundan ang praktikal na public-transport journeys gamit ang walking, tricycle, at jeepney options kapag may route data."
                )
            )
            Spacer(Modifier.height(12.dp))
            AboutInfoCard(
                title = supportCopy("What TUKI helps with", "Ano ang tinutulungan ng TUKI"),
                body = supportCopy(
                    "Route comparison, fare and time estimates, saved favorites, recent trips, location-based guidance, and live trip assistance are brought together in one app experience.",
                    "Pinagsasama ng TUKI sa isang app ang route comparison, fare at time estimates, Favorites, Recent Trips, location-based guidance, at live trip assistance."
                )
            )
            Spacer(Modifier.height(12.dp))
            AboutInfoCard(
                title = supportCopy("A note on travel information", "Paalala sa travel information"),
                body = supportCopy(
                    "Travel times, fares, service availability, and road conditions can change. Treat estimates as guidance and follow current local transport rules and conditions.",
                    "Maaaring magbago ang travel time, pamasahe, availability ng serbisyo, at kondisyon ng kalsada. Gamitin ang estimates bilang gabay at sundin ang kasalukuyang local transport rules at conditions."
                )
            )
        }
    }
}

@Composable
private fun AboutInfoCard(title: String, body: String) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = TukiSurfaceRaised
    ) {
        Column(Modifier.padding(16.dp)) {
            Text(title, color = TukiInk, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(6.dp))
            Text(body, color = TukiMuted, style = MaterialTheme.typography.bodyMedium)
        }
    }
}

@Composable
private fun SupportPageScaffold(
    title: String,
    onBack: () -> Unit,
    content: androidx.compose.foundation.lazy.LazyListScope.() -> Unit
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding(),
        contentPadding = PaddingValues(start = 24.dp, end = 24.dp, top = 14.dp, bottom = 32.dp)
    ) {
        item {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .background(TukiSurfaceRaised, RoundedCornerShape(12.dp))
                        .clickable(onClick = onBack),
                    contentAlignment = Alignment.Center
                ) {
                    Text("‹", color = TukiInk, style = MaterialTheme.typography.displaySmall)
                }
                Spacer(Modifier.width(14.dp))
                Text(title, color = TukiInk, style = MaterialTheme.typography.displaySmall)
            }
            Spacer(Modifier.height(28.dp))
        }
        content()
    }
}
