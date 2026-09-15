using System.Text;
using System.Collections.Generic;
using ASPCache = Microsoft.Extensions.Caching.Memory;
using DecisionVault.API;
using DecisionVault.API.Configuration;
using DecisionVault.API.Middleware;
using DecisionVault.Application.Interfaces;
using DecisionVault.Application.Services;
using DecisionVault.Domain.Entities;
using DecisionVault.Infrastructure.Auth;
using DecisionVault.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
builder.Services.AddDbContext<DecisionVaultDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DecisionVault")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDecisionService, DecisionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IAdminService, AdminService>();

builder.Services.AddHttpContextAccessor();

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection(JwtSettings.SectionName).Bind(jwtSettings);
if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
    throw new InvalidOperationException(
        "Jwt:Secret is missing or shorter than 32 characters. Configure it via appsettings.Development.json, user-secrets or environment variables.");
builder.Services.AddSingleton(jwtSettings);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters =
        JwtTokenService.ValidationParameters(jwtSettings));

builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Date-only strings (e.g. "2026-09-15") deserialize with Kind=Unspecified, which
    // Npgsql rejects for timestamptz columns. Normalize to UTC at the boundary.
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
});
builder.Services.AddConsistentApiErrors();

// ---------- CORS / Swagger ----------
builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DecisionVault API",
        Version = "v1",
        Description = "Decision Intelligence & Outcome Tracking Platform — record decisions, evaluate outcomes, learn."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT returned by /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // Serve swagger UI assets with no-store: Chromium heuristically caches the ~1.4 MB
    // bundle (no Last-Modified on the response), so a Swashbuckle upgrade would otherwise
    // keep serving the stale UI from browser cache until a hard refresh.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
        await next();
    });
    // Explicit endpoint: the default multi-definition bootstrap resolves the spec
    // URL as "" and swagger-ui's version-pragma mis-detects it (isOAS3=false)
    // even though the document is valid OpenAPI 3.0.4.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "DecisionVault API v1"));
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ---------- Startup ----------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbSeeder.SeedAsync(app.Services, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database seeding failed");
        throw;
    }
}

app.Run();
