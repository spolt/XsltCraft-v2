using System.Text;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using XsltCraft.Api.Preview;
using XsltCraft.Api.Xslt;
using XsltCraft.Infrastructure.DependencyInjection;
using XsltCraft.Infrastructure.Persistence;
using XsltCraft.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Çekirdek dökümü / ptrace ile bellekteki sırların (JWT anahtarı, DB parolası) okunmasını engelle (Linux; Production).
ProcessHardening.Apply(builder.Configuration);

builder.Services.AddControllers();
// Docker HEALTHCHECK (`curl /health`) için liveness — süreç ayakta ve istek işliyor mu.
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddXsltCraft(builder.Configuration);
builder.Services.AddScoped<PreviewAssetEmbedder>();

builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.OnRejected = RateLimitPolicies.OnRejectedAsync;

    // XSLT önizleme (xslt-preview) + doğrulama (xslt-validate): 120 req/dk/user.
    opts.AddXsltPolicies();

    // Login / refresh / google: 10 istek / dakika / IP (G3: önceden tek global kova — bir saldırgan herkesi kilitliyordu)
    opts.AddPolicy("auth-sensitive", httpContext => RateLimitPolicies.PerIp(httpContext, 10, TimeSpan.FromMinutes(1)));

    // Register: 5 istek / 10 dakika / IP
    opts.AddPolicy("auth-register", httpContext => RateLimitPolicies.PerIp(httpContext, 5, TimeSpan.FromMinutes(10)));

    // AI panel istekleri: 30 req/dk/user
    opts.AddPolicy("ai-assistant", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Refresh: IP başına 30 istek/dk. Login ile ortak global limiti paylaşmaz — aksi halde
    // yoğun anda kullanıcıların sessiz yenilemesi 429 alıp oturumdan düşerdi.
    // Tema logo/imza birleştirme (önizleme + indirme): sürükle-bırak art arda istek üretir.
    opts.AddPolicy("theme-compose", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    opts.AddPolicy("auth-refresh", httpContext =>
        RateLimitPolicies.PerRefreshTokenOrIp(httpContext, "refreshToken", 30, TimeSpan.FromMinutes(1)));

    // İstemci tarafı indirme bildirimleri: 60 req/dk/user
    opts.AddPolicy("activity", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anon",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

});

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            // Varsayılan 5 dk tolerans 15 dk'lık token'ı fiilen 20 dk yapıyordu.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    options.AddPolicy("prod", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-apply migrations + seed admin on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    db.Database.Migrate();

    var adminUsername = config["Admin:Username"] ?? "admin";
    var adminEmail = config["Admin:Email"];
    var adminPassword = config["Admin:Password"];

    if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
    {
        var exists = await db.Users.AnyAsync(u => u.Email == adminEmail);
        if (!exists)
        {
            db.Users.Add(new XsltCraft.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                Username = adminUsername,
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                DisplayName = "Admin",
                Role = XsltCraft.Domain.Entities.UserRole.Admin,
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// UseCors MUST come before UseRouting when using named policies with endpoint routing
app.UseCors(app.Environment.IsDevelopment() ? "dev" : "prod");
// Kimlik doğrulamadan SONRA: kullanıcı bölümlü politikalar (ai-assistant, xslt-preview, ...) claim'i görebilsin
// (önceden User boştu → hepsi IP'ye düşüyordu). IP bölümlü auth politikaları etkilenmez.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
