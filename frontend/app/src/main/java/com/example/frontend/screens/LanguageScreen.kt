package com.example.frontend.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

private val TukiCream = Color(0xFFFFF8E8)
private val TukiCream2 = Color(0xFFFAEBC7)
private val TukiDark = Color(0xFF173B43)
private val TukiGray = Color(0xFF9AA6A9)
private val TukiOrange = Color(0xFFE28B2B)

enum class LanguageOption(val title: String, val subtitle: String) {
    ENGLISH("English", "English (United States)"),
    FILIPINO("Filipino", "Tagalog")
}

@Composable
fun LanguageScreen(
    initialLanguage: LanguageOption = LanguageOption.ENGLISH,
    onBack: () -> Unit = {},
    onSaveLanguage: (LanguageOption) -> Unit = {}
) {
    var selectedLanguage by remember { mutableStateOf(initialLanguage) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(TukiCream)
            .statusBarsPadding()
            .navigationBarsPadding()
            .padding(horizontal = 24.dp, vertical = 20.dp)
    ) {
        // Header
        Row(verticalAlignment = Alignment.CenterVertically) {
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .background(TukiCream2, CircleShape)
                    .clickable(onClick = onBack),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "\u2039",
                    color = TukiDark,
                    fontSize = 22.sp,
                    fontWeight = FontWeight.Bold
                )
            }
            Spacer(modifier = Modifier.width(16.dp))
            Text(
                text = "Language",
                color = TukiDark,
                fontSize = 24.sp,
                fontWeight = FontWeight.ExtraBold
            )
        }

        Spacer(modifier = Modifier.height(28.dp))

        // Section Title
        Text(
            text = "SELECT LANGUAGE",
            color = TukiGray,
            fontSize = 12.sp,
            fontWeight = FontWeight.Bold
        )

        Spacer(modifier = Modifier.height(12.dp))

        // Language Options
        LanguageOption.values().forEach { option ->
            LanguageCard(
                option = option,
                isSelected = option == selectedLanguage,
                onClick = { selectedLanguage = option }
            )
            Spacer(modifier = Modifier.height(12.dp))
        }

        Spacer(modifier = Modifier.weight(1f))

        // Save Button
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(52.dp)
                .background(TukiOrange, RoundedCornerShape(16.dp))
                .clickable { onSaveLanguage(selectedLanguage) },
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "Save",
                color = Color.White,
                fontSize = 16.sp,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

@Composable
private fun LanguageCard(
    option: LanguageOption,
    isSelected: Boolean,
    onClick: () -> Unit
) {
    val shape = RoundedCornerShape(18.dp)

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(TukiCream2, shape)
            .then(
                if (isSelected) {
                    Modifier.border(BorderStroke(2.dp, TukiOrange), shape)
                } else {
                    Modifier
                }
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 20.dp, vertical = 18.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = option.title,
                    color = TukiDark,
                    fontSize = 17.sp,
                    fontWeight = FontWeight.Bold
                )
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = option.subtitle,
                    color = TukiGray,
                    fontSize = 13.sp
                )
            }

            if (isSelected) {
                Text(
                    text = "\u2713",
                    color = TukiOrange,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.Bold
                )
            }
        }
    }
}