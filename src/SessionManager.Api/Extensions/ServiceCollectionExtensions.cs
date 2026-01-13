using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Data;
using SessionManager.Api.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace SessionManager.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSessionManagerServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        // Bind configuration sections
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        // Add DbContext
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions { ConnectionString = "" };

        if (string.IsNullOrEmpty(databaseOptions.ConnectionString))
        {
            throw new InvalidOperationException("Database__ConnectionString is required");
        }

        services.AddDbContext<SessionManagerDbContext>(options =>
        {
            options.UseNpgsql(databaseOptions.ConnectionString);
        });

        // Add Redis
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IOptions<RedisOptions>>().Value ?? new RedisOptions();
            var configuration = ConfigurationOptions.Parse(config.ConnectionString);
            configuration.AbortOnConnectFail = false;
            configuration.ConnectRetry = 3;
            configuration.ConnectTimeout = 10000;
            configuration.AsyncTimeout = 10000;
            configuration.SyncTimeout = 10000;
            configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);
            return ConnectionMultiplexer.Connect(configuration);
        });

        // Add services
        services.AddSingleton<ISessionService, RedisSessionService>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddHttpClient<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IEmailService, EmailService>();

        // Add background services
        services.AddHostedService<EmailQueueConsumer>();

        // Add controllers
        services.AddControllers();

        // Configure JSON serialization
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        // CORS
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
        var allowedOrigins = new List<string> { corsOptions.FrontendUrl };

        if (env.IsDevelopment())
        {
            allowedOrigins.Add("http://localhost:5173");
        }

        if (corsOptions.AdditionalOrigins.Length > 0)
        {
            allowedOrigins.AddRange(corsOptions.AdditionalOrigins);
        }

        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(allowedOrigins.ToArray())
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
