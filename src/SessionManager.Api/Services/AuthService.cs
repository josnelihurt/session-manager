using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Models.Auth;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Services;

public class AuthService : IAuthService
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly ISessionService _sessionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IInvitationService _invitationService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SessionManagerDbContext dbContext,
        ISessionService sessionService,
        IPasswordHasher passwordHasher,
        IInvitationService invitationService,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _sessionService = sessionService;
        _passwordHasher = passwordHasher;
        _invitationService = invitationService;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, string ipAddress, string userAgent)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Provider == "local");

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            _logger.LogWarning("Login failed: user {Username} not found", request.Username);
            return new LoginResponse(Success: false, Error: "Invalid credentials");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {Username} is inactive", request.Username);
            return new LoginResponse(Success: false, Error: "Account is disabled");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed: invalid password for {Username}", request.Username);
            return new LoginResponse(Success: false, Error: "Invalid credentials");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Create session
        var sessionKey = await _sessionService.CreateSessionAsync(user.Id, user.Username, user.Email, user.IsSuperAdmin, ipAddress, userAgent);

        _logger.LogInformation("User {Username} logged in successfully", user.Username);

        return new LoginResponse(
            Success: true,
            SessionKey: sessionKey,
            User: new UserInfo(
                Id: user.Id,
                Username: user.Username,
                Email: user.Email,
                IsSuperAdmin: user.IsSuperAdmin,
                Provider: user.Provider
            )
        );
    }

    public async Task<UserInfo?> ValidateCredentialsAsync(string username, string password)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == username && u.Provider == "local");

        if (user == null || string.IsNullOrEmpty(user.PasswordHash))
        {
            _logger.LogWarning("Credential validation failed: user {Username} not found", username);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Credential validation failed: user {Username} is inactive", username);
            return null;
        }

        if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Credential validation failed: invalid password for {Username}", username);
            return null;
        }

        _logger.LogInformation("Credentials validated for user {Username}", username);

        return new UserInfo(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            IsSuperAdmin: user.IsSuperAdmin,
            Provider: user.Provider
        );
    }

    public async Task<LoginResponse> CreateSessionForUserAsync(Guid userId, string ipAddress, string userAgent)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Session creation failed: user {UserId} not found", userId);
            return new LoginResponse(Success: false, Error: "User not found");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Session creation failed: user {Username} is inactive", user.Username);
            return new LoginResponse(Success: false, Error: "Account is disabled");
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // Create session
        var sessionKey = await _sessionService.CreateSessionAsync(user.Id, user.Username, user.Email, user.IsSuperAdmin, ipAddress, userAgent);

        _logger.LogInformation("Session created for user {Username}", user.Username);

        return new LoginResponse(
            Success: true,
            SessionKey: sessionKey,
            User: new UserInfo(
                Id: user.Id,
                Username: user.Username,
                Email: user.Email,
                IsSuperAdmin: user.IsSuperAdmin,
                Provider: user.Provider
            )
        );
    }

    public async Task<LoginResponse> RegisterAsync(RegisterRequest request, string ipAddress, string userAgent)
    {
        // Validate invitation token
        var invitation = await _invitationService.GetByTokenAsync(request.Token);
        if (invitation == null)
        {
            _logger.LogWarning("Registration failed: invalid invitation token");
            return new LoginResponse(Success: false, Error: "Invalid or expired invitation token");
        }

        if (invitation.IsUsed)
        {
            _logger.LogWarning("Registration failed: invitation token already used");
            return new LoginResponse(Success: false, Error: "Invitation token has already been used");
        }

        // Check if provider is supported
        if (request.Provider != SessionManagerConstants.LocalProvider)
        {
            _logger.LogWarning("Registration failed: unsupported provider {Provider}", request.Provider);
            return new LoginResponse(Success: false, Error: "Only local registration is supported. Use Google OAuth button instead.");
        }

        // Check if username already exists
        var existingUsername = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUsername != null)
        {
            _logger.LogWarning("Registration failed: username {Username} already exists", request.Username);
            return new LoginResponse(Success: false, Error: "Username already exists");
        }

        // Check if the invitation's email is already registered to a different user
        var existingEmailUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == invitation.Email && u.Username != request.Username);
        if (existingEmailUser != null)
        {
            _logger.LogWarning("Registration failed: email {Email} already registered to user {Username}", invitation.Email, existingEmailUser.Username);
            return new LoginResponse(Success: false, Error: "This email is already registered. Please log in or use a different invitation.");
        }

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        // Create new user
        var user = new User
        {
            Username = request.Username,
            Email = invitation.Email,
            PasswordHash = passwordHash,
            Provider = "local",
            IsActive = true,
            IsSuperAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("New user {Username} created with invitation token", user.Username);

        // Mark invitation as used
        await _invitationService.MarkAsUsedAsync(invitation.Id, user.Id);
        _logger.LogInformation("Invitation {Token} marked as used", request.Token.Substring(0, 8) + "...");

        // Assign pre-configured roles from invitation (if any)
        if (invitation.PreAssignedRoles != null && invitation.PreAssignedRoles.Length > 0)
        {
            foreach (var roleIdStr in invitation.PreAssignedRoles)
            {
                if (Guid.TryParse(roleIdStr, out var roleId))
                {
                    var userRole = new UserRole
                    {
                        UserId = user.Id,
                        RoleId = roleId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.UserRoles.Add(userRole);
                }
            }
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Assigned {Count} pre-configured roles to user {Username}", invitation.PreAssignedRoles.Length, user.Username);
        }

        // Create session
        var sessionKey = await _sessionService.CreateSessionAsync(user.Id, user.Username, user.Email, user.IsSuperAdmin, ipAddress, userAgent);

        _logger.LogInformation("User {Username} registered and logged in successfully", user.Username);

        return new LoginResponse(
            Success: true,
            SessionKey: sessionKey,
            User: new UserInfo(
                Id: user.Id,
                Username: user.Username,
                Email: user.Email,
                IsSuperAdmin: user.IsSuperAdmin,
                Provider: user.Provider
            )
        );
    }

    public async Task<bool> LogoutAsync(string sessionKey)
    {
        return await _sessionService.InvalidateSessionAsync(sessionKey);
    }

    public async Task<UserInfo?> GetCurrentUserAsync(string sessionKey)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);
        if (session == null) return null;

        return new UserInfo(
            Id: session.UserId,
            Username: session.Username,
            Email: session.Email,
            IsSuperAdmin: session.IsSuperAdmin,
            Provider: "session" // From session, not direct DB lookup
        );
    }

    public async Task<IEnumerable<ProviderInfo>> GetProvidersAsync()
    {
        var providers = await _dbContext.AuthProviders
            .Where(p => p.IsEnabled)
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new ProviderInfo(
                p.Name,
                p.DisplayName,
                p.IconUrl
            ))
            .ToListAsync();

        return providers;
    }

    public async Task<bool> CanAccessApplicationAsync(string sessionKey, string applicationUrl)
    {
        var session = await _sessionService.GetSessionAsync(sessionKey);
        if (session == null) return false;

        // Super admin can access everything
        if (session.IsSuperAdmin) return true;

        // Normalize URL (lowercase, trim)
        var normalizedUrl = applicationUrl?.ToLowerInvariant()?.Trim() ?? "";

        // Check if user has any role for this application
        var hasAccess = await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .ThenInclude(r => r.Application)
            .AnyAsync(ur =>
                ur.UserId == session.UserId &&
                ur.Role.Application.Url == normalizedUrl &&
                ur.Role.Application.IsActive);

        return hasAccess;
    }
}
