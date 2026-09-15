using System.Text;
using ASPCache = Microsoft.Extensions.Caching.Memory;
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
using Microsoft.OpenApi.Models;

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
builder.Services.AddControllers();
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
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ---------- Pipeline ----------
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
