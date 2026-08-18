package com.example.frontend.model.network

/**
 * Matches backend.Controllers.LoginRequest
 */
data class LoginRequest(
    val userName: String,
    val password: String
)

/**
 * Matches backend.Controllers.LoginResponse
 * Note: A similar class exists in com.example.frontend.auth, but this aligns
 * with the username/password flow and the backend's explicit DTO structure.
 */
data class AuthResponse(
    val apiKey: String,
    val expiresAt: String,
    val authenticationScheme: String,
    val headerName: String
)
