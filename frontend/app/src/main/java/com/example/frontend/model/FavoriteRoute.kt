package com.example.frontend.model

data class FavoriteRoute (
    val id: String,
    val origin: String,
    val destination: String,
    val timesUsed: Int,
    val note: String //short desc
)