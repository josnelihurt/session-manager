using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Services;

public interface IInvitationService
{
    Task<IEnumerable<InvitationDto>> GetAllAsync();
    Task<InvitationDto?> CreateAsync(CreateInvitationRequest request, Guid createdByUserId);
    Task<bool> DeleteAsync(Guid id);
    Task<InvitationDto?> GetByTokenAsync(string token);
    Task<bool> MarkAsUsedAsync(Guid id, Guid usedById);
}
