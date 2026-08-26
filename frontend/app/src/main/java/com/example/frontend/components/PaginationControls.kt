package com.example.frontend.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.frontend.core.localization.AppLanguagePreference

@Composable
fun PaginationControls(
    currentPage: Int,
    totalPages: Int,
    onPageChange: (Int) -> Unit,
    modifier: Modifier = Modifier
) {
    if (totalPages <= 1) return

    val filipino = AppLanguagePreference.isFilipino()
    Row(
        modifier = modifier
            .fillMaxWidth()
            .padding(vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        TextButton(
            enabled = currentPage > 0,
            onClick = { onPageChange((currentPage - 1).coerceAtLeast(0)) }
        ) {
            Text(
                text = if (filipino) "Nakaraan" else "Previous",
                style = MaterialTheme.typography.labelLarge
            )
        }

        Text(
            text = if (filipino) {
                "Pahina ${currentPage + 1} ng $totalPages"
            } else {
                "Page ${currentPage + 1} of $totalPages"
            },
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            style = MaterialTheme.typography.labelLarge
        )

        TextButton(
            enabled = currentPage < totalPages - 1,
            onClick = { onPageChange((currentPage + 1).coerceAtMost(totalPages - 1)) }
        ) {
            Text(
                text = if (filipino) "Susunod" else "Next",
                style = MaterialTheme.typography.labelLarge
            )
        }
    }
}
