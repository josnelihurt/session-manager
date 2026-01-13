using System.Text.Json.Serialization;

namespace SessionManager.Api.Models.Invitations;

public record InvitationDto(
    Guid Id,
    string Token,
    string Email,
    string Provider,
    string[] PreAssignedRoles,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? UsedAt,
    bool IsUsed,
    string InviteUrl
);

public record CreateInvitationRequest(
    string Email,
    string Provider,
    Guid[]? PreAssignedRoleIds,
    bool SendEmail = false
);

[JsonSerializable(typeof(InvitationDto))]
[JsonSerializable(typeof(InvitationDto[]))]
[JsonSerializable(typeof(CreateInvitationRequest))]
internal partial class AppJsonContextInvitations : JsonSerializerContext { }
