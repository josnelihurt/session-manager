namespace SessionManager.Api.Models.Auth;

public record RegisterRequest(string Token, string Provider, string Username, string Password);
