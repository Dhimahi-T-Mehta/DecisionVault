using System.Net;
using System.Text.Json;
using DecisionVault.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DecisionVault.API.Middleware;

public record ApiError(bool Success, string Message, IReadOnlyList<string> Errors, string Timestamp)
{
    public static ApiError Create(string message, IEnumerable<string>? errors = null) =>
        new(false, message, errors?.ToList() ?? [], DateTime.UtcNow.ToString("o"));
}

/// <summary>
/// Single place that converts exceptions to consistent JSON error responses.
/// Controllers never need try/catch for expected domain failures.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, message, errors) = exception switch
        {
            NotFoundException nf => (HttpStatusCode.NotFound, nf.Message, (IReadOnlyList<string>?)null),
            ConflictException cf => (HttpStatusCode.Conflict, cf.Message, null),
            ForbiddenException fb => (HttpStatusCode.Forbidden, fb.Message, null),
            ValidationException ve => (HttpStatusCode.BadRequest, ve.Message, ve.Errors),
            UnauthorizedException ue => (HttpStatusCode.Unauthorized, ue.Message, null),
            // Unique-index race (e.g. simultaneous registrations with the same email):
            // the service pre-checks, but only the DB constraint is authoritative.
            DbUpdateException dbEx when dbEx.InnerException is PostgresException pg
                                      && pg.SqlState == PostgresErrorCodes.UniqueViolation
                => (HttpStatusCode.Conflict, "A record with these details already exists.", (IReadOnlyList<string>?)null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again.", null)
        };

        if (status == HttpStatusCode.InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Handled {Status} on {Method} {Path}: {Message}", (int)status, context.Request.Method, context.Request.Path, message);

        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(ApiError.Create(message, errors), JsonOptions));
    }
}

/// <summary>Maps ProblemDetails produced by the framework (model binding 400s) onto the same envelope.</summary>
public static class ApiErrorExtensions
{
    public static IServiceCollection AddConsistentApiErrors(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(kv => kv.Value?.Errors.Count > 0)
                    .SelectMany(kv => kv.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? $"{kv.Key} is invalid." : e.ErrorMessage))
                    .ToList();

                var payload = ApiError.Create("One or more validation errors occurred.", errors);
                return new BadRequestObjectResult(payload);
            };
        });
}
