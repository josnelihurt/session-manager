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
    }
}
