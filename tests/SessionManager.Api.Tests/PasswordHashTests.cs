using SessionManager.Api.Services;
using Xunit;
using FluentAssertions;

namespace SessionManager.Api.Tests;

public class PasswordHashTests
{
    [Fact]
    public void Can_Hash_And_Verify_Password()
    {
        // Arrange
        var hasher = new BCryptPasswordHasher();
        var password = "test123";

        // Act
        var hash = hasher.HashPassword(password);
        var isValid = hasher.VerifyPassword(password, hash);

        // Assert
        isValid.Should().BeTrue();
        hash.Should().NotBe(password);
        hash.Should().StartWith("$2a$12$"); // BCrypt format
    }

    [Fact]
    public void Wrong_Password_Returns_False()
    {
        // Arrange
        var hasher = new BCryptPasswordHasher();
        var password = "test123";
        var hash = hasher.HashPassword(password);

        // Act
        var isValid = hasher.VerifyPassword("wrong", hash);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void Generate_Hash_For_Super_Admin_Password()
    {
        // Arrange
        var hasher = new BCryptPasswordHasher();
        var password = "fakeAdminPassword123!";

        // Act
        var hash = hasher.HashPassword(password);
        var isValid = hasher.VerifyPassword(password, hash);

        // Assert & Output
        isValid.Should().BeTrue();
        Console.WriteLine($"Password: {password}");
        Console.WriteLine($"Hash: {hash}");
        Console.WriteLine();
        Console.WriteLine($"SQL UPDATE command:");
        Console.WriteLine($"UPDATE session_manager.users SET password_hash = '{hash}' WHERE username = 'fakeadmin';");
    }

    [Theory]
    [InlineData("admin123")]
    [InlineData("password")]
    [InlineData("fakeAdminPassword123!")]
    public void Can_Hash_Common_Passwords(string password)
    {
        // Arrange
        var hasher = new BCryptPasswordHasher();

        // Act
        var hash = hasher.HashPassword(password);
        var isValid = hasher.VerifyPassword(password, hash);

        // Assert
        isValid.Should().BeTrue();
        Console.WriteLine($"Password: {password} -> Hash: {hash}");
    }

    [Fact]
    public void Verify_Known_Hash_Format()
    {
        // Arrange - known BCrypt hash for "password" (generated with work factor 10)
        var hasher = new BCryptPasswordHasher();
        var knownHash = "$2a$10$WaM.MRj./nW4sooetdYYperex.zjrjuaGKMeqh3PVkghy.qd2pVTm";

        // Act
        var isValid = hasher.VerifyPassword("password", knownHash);

        // Assert
        isValid.Should().BeTrue();
    }
}
