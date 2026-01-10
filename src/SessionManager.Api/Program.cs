using Microsoft.Extensions.Options;
using SessionManager.Api.Configuration;
using SessionManager.Api.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Bind configuration sections
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

// Add services to the container.
builder.Services.AddControllers();

// Redis connection
var redisOptions = builder.Configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 10000;
    configuration.AsyncTimeout = 10000;
    configuration.SyncTimeout = 10000;
    configuration.ReconnectRetryPolicy = new ExponentialRetry(1000);
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddSingleton<ISessionService, RedisSessionService>();

// CORS
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
var allowedOrigins = new List<string> { corsOptions.FrontendUrl };

if (builder.Environment.IsDevelopment())
{
    allowedOrigins.Add("http://localhost:5173");
}

if (corsOptions.AdditionalOrigins.Length > 0)
{
    allowedOrigins.AddRange(corsOptions.AdditionalOrigins);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Session Manager API",
        Version = "v1",
        Description = "OAuth2 Proxy Session Management API"
    });
});

var app = builder.Build();

// Always enable Swagger for internal tools
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
