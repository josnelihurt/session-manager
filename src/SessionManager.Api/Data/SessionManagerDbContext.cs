using Microsoft.EntityFrameworkCore;
using SessionManager.Api.Entities;

namespace SessionManager.Api.Data;

public class SessionManagerDbContext : DbContext
{
    public SessionManagerDbContext(DbContextOptions<SessionManagerDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<OtpAttempt> OtpAttempts => Set<OtpAttempt>();
    public DbSet<ImpersonationSession> ImpersonationSessions => Set<ImpersonationSession>();
    public DbSet<ImpersonationAuditLog> ImpersonationAuditLogs => Set<ImpersonationAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("session_manager");

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(100);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
            entity.Property(e => e.ProviderId).HasColumnName("provider_id").HasMaxLength(255);
            entity.Property(e => e.IsSuperAdmin).HasColumnName("is_super_admin");
            entity.Property(e => e.CanImpersonate).HasColumnName("can_impersonate");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Application
        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("applications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Url).HasColumnName("url").HasMaxLength(500);
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.Url).IsUnique();
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ApplicationId).HasColumnName("application_id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(50);
            entity.Property(e => e.PermissionsJson).HasColumnName("permissions_json").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.Application).WithMany(a => a.Roles).HasForeignKey(e => e.ApplicationId);
            entity.HasIndex(e => new { e.ApplicationId, e.Name }).IsUnique();
        });

        // UserRole
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.GrantedBy).HasColumnName("granted_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasOne(e => e.User).WithMany(u => u.UserRoles).HasForeignKey(e => e.UserId);
            entity.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
        });

        // Invitation
        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Token).HasColumnName("token").HasMaxLength(64);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
            entity.Property(e => e.PreAssignedRoles).HasColumnName("pre_assigned_roles");
            entity.Property(e => e.CreatedById).HasColumnName("created_by");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");
            entity.Property(e => e.UsedById).HasColumnName("used_by");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.Token).IsUnique();
        });

        // AuthProvider
        modelBuilder.Entity<AuthProvider>(entity =>
        {
            entity.ToTable("auth_providers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(100);
            entity.Property(e => e.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled");
            entity.Property(e => e.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb");
            entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Session
        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.SessionKey).HasColumnName("session_key").HasMaxLength(255);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.HasOne(e => e.User).WithMany(u => u.Sessions).HasForeignKey(e => e.UserId);
            entity.HasIndex(e => e.SessionKey).IsUnique();
        });

        // OtpAttempt
        modelBuilder.Entity<OtpAttempt>(entity =>
        {
            entity.ToTable("otp_attempts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(10);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");
            entity.HasIndex(e => new { e.Email, e.Code });
        });

        // ImpersonationSession
        modelBuilder.Entity<ImpersonationSession>(entity =>
        {
            entity.ToTable("impersonation_sessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ImpersonatorId).HasColumnName("impersonator_id");
            entity.Property(e => e.ImpersonatorSessionKey).HasColumnName("impersonator_session_key").HasMaxLength(255);
            entity.Property(e => e.TargetUserId).HasColumnName("target_user_id");
            entity.Property(e => e.ImpersonatedSessionKey).HasColumnName("impersonated_session_key").HasMaxLength(255);
            entity.Property(e => e.Reason).HasColumnName("reason").HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.StartedAt).HasColumnName("started_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.EndedAt).HasColumnName("ended_at");
            entity.Property(e => e.EndReason).HasColumnName("end_reason").HasMaxLength(50);

            entity.HasOne(e => e.Impersonator).WithMany().HasForeignKey(e => e.ImpersonatorId);
            entity.HasOne(e => e.TargetUser).WithMany().HasForeignKey(e => e.TargetUserId);
        });

        // ImpersonationAuditLog
        modelBuilder.Entity<ImpersonationAuditLog>(entity =>
        {
            entity.ToTable("impersonation_audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ImpersonationSessionId).HasColumnName("impersonation_session_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(100);
            entity.Property(e => e.ResourceType).HasColumnName("resource_type").HasMaxLength(100);
            entity.Property(e => e.ResourceId).HasColumnName("resource_id").HasMaxLength(255);
            entity.Property(e => e.HttpMethod).HasColumnName("http_method").HasMaxLength(10);
            entity.Property(e => e.Endpoint).HasColumnName("endpoint").HasMaxLength(500);
            entity.Property(e => e.RequestBodyHash).HasColumnName("request_body_hash").HasMaxLength(64);
            entity.Property(e => e.ResponseStatusCode).HasColumnName("response_status_code");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            entity.HasOne(e => e.ImpersonationSession).WithMany(i => i.AuditLogs).HasForeignKey(e => e.ImpersonationSessionId);
        });
    }
}
