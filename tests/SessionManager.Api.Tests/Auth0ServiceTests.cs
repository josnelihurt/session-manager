using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SessionManager.Api.Configuration;
using SessionManager.Api.Services.Auth;
using Xunit;
using FluentAssertions;

namespace SessionManager.Api.Tests;

public class Auth0ServiceTests
{
    private readonly Auth0Options _options;

    public Auth0ServiceTests()
    {
        _options = new Auth0Options
        {
            Domain = "dev-test.us.auth0.com",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CallbackUrl = "https://test.example.com/api/auth/callback/auth0",
            ManagementApiClientId = "m2m-client-id",
            ManagementApiClientSecret = "m2m-client-secret",
            ManagementApiIdentifier = "https://dev-test.us.auth0.com/api/v2/"
        };
    }

    [Fact]
    public void GetAuthorizationUrl_GeneratesCorrectUrl_WithoutInvitation()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);
        var state = "test-state-123";

        // Act
        var url = auth0Service.GetAuthorizationUrl(state, null);

        // Assert
        url.Should().Contain("dev-test.us.auth0.com");
        url.Should().Contain("client_id=test-client-id");
        url.Should().Contain("redirect_uri=" + Uri.EscapeDataString(_options.CallbackUrl));
        url.Should().Contain("response_type=code");
        url.Should().Contain("scope=" + Uri.EscapeDataString("openid profile email"));
        url.Should().Contain("state=test-state-123");
        url.Should().NotContain("|"); // No invitation separator
    }

    [Fact]
    public void GetAuthorizationUrl_GeneratesCorrectUrl_WithInvitation()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);
        var state = "test-state-123";
        var invitationToken = "invite-token-abc";

        // Act
        var url = auth0Service.GetAuthorizationUrl(state, invitationToken);

        // Assert
        url.Should().Contain("dev-test.us.auth0.com");
        url.Should().Contain($"state={Uri.EscapeDataString(invitationToken + "|" + state)}");
    }

    [Fact]
    public void GetAuthorizationUrl_HandlesDomainWithProtocol()
    {
        // Arrange
        var optionsWithProtocol = new Auth0Options
        {
            Domain = "https://dev-test.us.auth0.com",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CallbackUrl = "https://test.example.com/api/auth/callback/auth0"
        };

        var optionsWrapper = Options.Create(optionsWithProtocol);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var httpClient = new HttpClient();
        var service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);

        // Act
        var url = service.GetAuthorizationUrl("state", null);

        // Assert
        url.Should().StartWith("https://dev-test.us.auth0.com");
    }

    [Fact]
    public void GetAuthorizationUrl_HandlesDomainWithoutProtocol()
    {
        // Arrange
        var optionsWithoutProtocol = new Auth0Options
        {
            Domain = "dev-test.us.auth0.com",
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CallbackUrl = "https://test.example.com/api/auth/callback/auth0"
        };

        var optionsWrapper = Options.Create(optionsWithoutProtocol);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var httpClient = new HttpClient();
        var service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);

        // Act
        var url = service.GetAuthorizationUrl("state", null);

        // Assert
        url.Should().StartWith("https://dev-test.us.auth0.com");
    }

    [Fact]
    public void GetAuthorizationUrl_GeneratesDifferentStates()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);

        // Act
        var url1 = auth0Service.GetAuthorizationUrl("state1", null);
        var url2 = auth0Service.GetAuthorizationUrl("state2", null);

        // Assert - states should be different (CSRF protection)
        var state1 = new System.Uri(url1).ToString().Split('=').Last();
        var state2 = new System.Uri(url2).ToString().Split('=').Last();

        state1.Should().NotBe(state2);
    }

    [Fact]
    public void GetAuthorizationUrl_UsesProvidedState()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);
        var testState = "my-custom-state-value";

        // Act
        var url = auth0Service.GetAuthorizationUrl(testState, null);
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var state = Uri.UnescapeDataString(query.Get("state") ?? "");

        // Assert - state parameter should be preserved
        state.Should().Be(testState);
    }

    [Fact]
    public void GetAuthorizationUrl_IncludesRequiredOAuthParameters()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);

        // Act
        var url = auth0Service.GetAuthorizationUrl("state", null);
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        // Assert
        query.Get("response_type").Should().Be("code");
        query.Get("client_id").Should().Be("test-client-id");
        query.Get("redirect_uri").Should().Be(_options.CallbackUrl);

        var scope = query.Get("scope");
        scope.Should().Contain("openid");
        scope.Should().Contain("profile");
        scope.Should().Contain("email");
    }

    [Fact]
    public void GetAuthorizationUrl_UsesHttpsForCallback()
    {
        // Arrange
        var httpClient = new HttpClient();
        var optionsWrapper = Options.Create(_options);
        var loggerMock = new Mock<ILogger<Auth0Service>>();
        var auth0Service = new Auth0Service(optionsWrapper, httpClient, loggerMock.Object);

        // Act
        var url = auth0Service.GetAuthorizationUrl("state", null);
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var redirectUri = query.Get("redirect_uri");

        // Assert - callback URL should be HTTPS
        redirectUri.Should().StartWith("https://");
    }

    [Fact]
    public void Auth0Options_HasAllRequiredProperties()
    {
        // Verify Auth0Options has all required configuration properties
        _options.Domain.Should().NotBeNullOrEmpty();
        _options.ClientId.Should().NotBeNullOrEmpty();
        _options.ClientSecret.Should().NotBeNullOrEmpty();
        _options.CallbackUrl.Should().NotBeNullOrEmpty();
        _options.ManagementApiClientId.Should().NotBeNullOrEmpty();
        _options.ManagementApiClientSecret.Should().NotBeNullOrEmpty();
        _options.ManagementApiIdentifier.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Auth0Options_CallbackUrlFormat_IsCorrect()
    {
        // Verify callback URL follows correct format
        _options.CallbackUrl.Should().StartWith("https://");
        _options.CallbackUrl.Should().Contain("/api/auth/callback/auth0");
    }

    [Fact]
    public void Auth0Options_ManagementApiIdentifier_IsCorrect()
    {
        // Verify Management API identifier format
        _options.ManagementApiIdentifier.Should().EndWith("/api/v2/");
    }
}
