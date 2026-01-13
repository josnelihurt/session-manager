using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Services;

public class InvitationService : IInvitationService
{
    private readonly SessionManagerDbContext _dbContext;
    private readonly ILogger<InvitationService> _logger;
    private readonly InvitationOptions _options;

    public InvitationService(
        SessionManagerDbContext dbContext,
        ILogger<InvitationService> _logger,
        IOptions<InvitationOptions> options)
    {
        _dbContext = dbContext;
        this._logger = _logger;
        _options = options.Value;
    }

    public async Task<IEnumerable<InvitationDto>> GetAllAsync()
    {
        var invitations = await _dbContext.Invitations
            .Include(i => i.CreatedBy)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return invitations.Select(MapToDto);
    }

    public async Task<InvitationDto?> CreateAsync(CreateInvitationRequest request, Guid createdByUserId)
    {
        // Check if email already exists in Users table (case-insensitive comparison)
        var emailLower = request.Email.ToLowerInvariant();
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

        if (existingUser != null)
        {
            _logger.LogWarning("Attempted to create invitation for existing email {Email}", request.Email);
            throw new InvalidOperationException($"A user with email '{request.Email}' already exists.");
        }

        var token = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddDays(_options.TokenLifetimeDays);

        var invitation = new Invitation
        {
            Token = token,
            Email = emailLower,
            Provider = request.Provider.ToLowerInvariant(),
            PreAssignedRoles = request.PreAssignedRoleIds ?? Array.Empty<Guid>(),
            CreatedById = createdByUserId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created invitation for {Email}, expires at {ExpiresAt}", request.Email, expiresAt);

        return await GetByIdAsync(invitation.Id);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var invitation = await _dbContext.Invitations.FindAsync(id);
        if (invitation == null) return false;

        _dbContext.Invitations.Remove(invitation);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted invitation {Id}", id);

        return true;
    }

    public async Task<InvitationDto?> GetByTokenAsync(string token)
    {
        var invitation = await _dbContext.Invitations
            .Include(i => i.CreatedBy)
            .FirstOrDefaultAsync(i => i.Token == token);

        return invitation != null ? MapToDto(invitation) : null;
    }

    public async Task<bool> MarkAsUsedAsync(Guid id, Guid usedById)
    {
        var invitation = await _dbContext.Invitations.FindAsync(id);
        if (invitation == null) return false;

        invitation.UsedAt = DateTime.UtcNow;
        invitation.UsedById = usedById;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Invitation {Id} marked as used by user {UserId}", id, usedById);

        return true;
    }

    public async Task<InvitationDto?> GetByIdAsync(Guid id)
    {
        var invitation = await _dbContext.Invitations
            .Include(i => i.CreatedBy)
            .FirstOrDefaultAsync(i => i.Id == id);

        return invitation != null ? MapToDto(invitation) : null;
    }

    private static InvitationDto MapToDto(Invitation invitation)
    {
        var baseUrl = "https://session-manager.lab.josnelihurt.me";
        return new InvitationDto(
            Id: invitation.Id,
            Token: invitation.Token,
            Email: invitation.Email,
            Provider: invitation.Provider,
            PreAssignedRoles: invitation.PreAssignedRoles?.Select(r => r.ToString()).ToArray() ?? Array.Empty<string>(),
            CreatedAt: invitation.CreatedAt,
            ExpiresAt: invitation.ExpiresAt,
            UsedAt: invitation.UsedAt,
            IsUsed: invitation.UsedAt != null,
            InviteUrl: $"{baseUrl}/register?token={invitation.Token}"
        );
    }
}
