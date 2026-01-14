using SessionManager.Api.Configuration;
using SessionManager.Api.Entities;
using SessionManager.Api.Models.Invitations;

namespace SessionManager.Api.Mappers;

public static class InvitationMapper
{
    public static InvitationDto ToDto(Invitation invitation)
    {
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
            InviteUrl: $"{SessionManagerConstants.Urls.BaseUrl}/register?token={invitation.Token}"
        );
    }

    public static List<InvitationDto> ToDto(IEnumerable<Invitation> invitations)
        => invitations.Select(ToDto).ToList();
}
