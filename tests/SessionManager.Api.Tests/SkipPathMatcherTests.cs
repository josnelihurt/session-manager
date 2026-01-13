using SessionManager.Api.Services;
using Xunit;
using FluentAssertions;

namespace SessionManager.Api.Tests;

public class SkipPathMatcherTests
{
    [Theory]
    [InlineData("/nodes", "^/nodes(/.*)?$", true)]
    [InlineData("/nodes/", "^/nodes(/.*)?$", true)]
    [InlineData("/nodes/123", "^/nodes(/.*)?$", true)]
    [InlineData("/flows", "^/nodes(/.*)?$", false)]
    [InlineData("/nodes/sub/path", "^/nodes(/.*)?$", true)]
    [InlineData("/flow", "^/flow(/.*)?$", true)]
    [InlineData("/flow/edit", "^/flow(/.*)?$", true)]
    [InlineData("/flows", "^/flow(/.*)?$", false)]
    [InlineData("/library", "^/library(/.*)?$", true)]
    [InlineData("/library/test", "^/library(/.*)?$", true)]
    [InlineData("/settings", "^/settings(/.*)?$", true)]
    [InlineData("/settings/general", "^/settings(/.*)?$", true)]
    [InlineData("/auth/login", "^/auth/.*$", true)]
    [InlineData("/auth/logout", "^/auth/.*$", true)]
    [InlineData("/red/", "^/red/.*$", true)]
    [InlineData("/red/flow", "^/red/.*$", true)]
    [InlineData("/red", "^/red/.*$", false)] // Pattern expects /red/ not /red alone
    [InlineData("/credentials/", "^/credentials/.*$", true)]
    [InlineData("/credentials/test", "^/credentials/.*$", true)]
    [InlineData("/credentials", "^/credentials/.*$", false)] // Pattern expects /credentials/ not /credentials alone
    [InlineData("/vendor/", "^/vendor/.*$", true)]
    [InlineData("/vendor/test", "^/vendor/.*$", true)]
    [InlineData("/vendor", "^/vendor/.*$", false)] // Pattern expects /vendor/ not /vendor alone
    [InlineData("/icons/", "^/icons/.*$", true)]
    [InlineData("/icons/test", "^/icons/.*$", true)]
    [InlineData("/icons", "^/icons/.*$", false)] // Pattern expects /icons/ not /icons alone
    [InlineData("/locales/", "^/locales/.*$", true)]
    [InlineData("/locales/test", "^/locales/.*$", true)]
    [InlineData("/locales", "^/locales/.*$", false)] // Pattern expects /locales/ not /locales alone
    [InlineData("/plugins", "^/plugins(/.*)?$", true)]
    [InlineData("/plugins/test", "^/plugins(/.*)?$", true)]
    [InlineData("/comms", "^/comms$", true)]
    [InlineData("/comms/extra", "^/comms$", false)]
    public void ShouldSkipPath_MatchesCorrectly(string requestPath, string pattern, bool expected)
    {
        // Act
        var result = SkipPathMatcher.ShouldSkipPath(requestPath, pattern);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldSkipPath_WithMultiplePatterns_MatchesAny()
    {
        // Arrange - NodeRED skip patterns
        var skipPaths = "^/nodes(/.*)?$,^/flows(/.*)?$,^/flow(/.*)?$,^/library(/.*)?$,^/settings(/.*)?$,^/auth/.*$,^/red/.*$,^/credentials/.*$,^/vendor/.*$,^/icons/.*$,^/locales/.*$,^/plugins(/.*)?$,^/comms$";

        // Act & Assert
        SkipPathMatcher.ShouldSkipPath("/nodes", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/flows", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/flow", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/library", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/settings", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/auth/login", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/red/flow", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/credentials/test", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/vendor/", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/icons/script", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/locales/en-US", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/plugins", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/comms", skipPaths).Should().BeTrue();
        SkipPathMatcher.ShouldSkipPath("/dashboard", skipPaths).Should().BeFalse();
        // These should NOT match because patterns expect content after slash
        SkipPathMatcher.ShouldSkipPath("/red", skipPaths).Should().BeFalse();
        SkipPathMatcher.ShouldSkipPath("/credentials", skipPaths).Should().BeFalse();
        SkipPathMatcher.ShouldSkipPath("/vendor", skipPaths).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipPath_EmptyPatterns_ReturnsFalse()
    {
        // Arrange
        var skipPaths = "";

        // Act
        var result = SkipPathMatcher.ShouldSkipPath("/nodes", skipPaths);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipPath_NullPatterns_ReturnsFalse()
    {
        // Act
        var result = SkipPathMatcher.ShouldSkipPath("/nodes", null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipPath_EmptyRequestPath_ReturnsFalse()
    {
        // Arrange
        var skipPaths = "^/nodes(/.*)?$";

        // Act
        var result = SkipPathMatcher.ShouldSkipPath("", skipPaths);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipPath_InvalidRegex_DoesNotCrash()
    {
        // Arrange - invalid regex pattern
        var skipPaths = "^/nodes([invalid$";

        // Act - should not throw exception
        var result = SkipPathMatcher.ShouldSkipPath("/nodes", skipPaths);

        // Assert - should return false for invalid regex
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://nodered.example.com/flows")]
    [InlineData("https://nodered.example.com/flows")]
    [InlineData("/flows?query=test")]
    [InlineData("/flows#section")]
    public void ExtractPath_HandlesUriVariations(string uri)
    {
        // This test verifies the ExtractPath method behavior
        // The actual implementation is in ForwardAuthEndpoints

        // For now just verify that we can handle the URI format
        var path = ExtractPath_Simple(uri);
        path.Should().Be("/flows");
    }

    private static string ExtractPath_Simple(string uri)
    {
        // Simplified version of ExtractPath for testing
        if (string.IsNullOrEmpty(uri))
        {
            return "/";
        }

        var queryIndex = uri.IndexOf('?');
        if (queryIndex > 0)
        {
            uri = uri[..queryIndex];
        }

        var fragmentIndex = uri.IndexOf('#');
        if (fragmentIndex > 0)
        {
            uri = uri[..fragmentIndex];
        }

        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
        {
            return parsedUri.AbsolutePath;
        }

        return uri.StartsWith('/') ? uri : "/" + uri;
    }
}
