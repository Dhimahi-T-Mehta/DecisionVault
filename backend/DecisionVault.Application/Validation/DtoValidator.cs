using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;

namespace DecisionVault.Application.Validation;

/// <summary>Server-side field validation. Complements DataAnnotations on DTO records.</summary>
public static partial class DtoValidator
{
    public static void ValidateRegistration(string fullName, string email, string password)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2 || fullName.Length > 100)
            errors.Add("Full name must be 2-100 characters.");
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email) || email.Length > 255)
            errors.Add("A valid email address is required.");
        ValidatePassword(password, errors);
        if (errors.Count > 0)
            throw new ValidationException("Registration failed validation.", errors);
    }

    public static void ValidatePassword(string? password, List<string>? errors = null)
    {
        var local = errors ?? [];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 72)
            local.Add("Password must be 8-72 characters.");
        else if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
            local.Add("Password must contain an uppercase letter, a lowercase letter and a digit.");
        if (errors is null && local.Count > 0)
            throw new ValidationException("Password failed validation.", local);
    }

    public static void ValidateDecisionRequest(string title, int categoryId, int? confidence, int? expectedSuccess,
        string? expectedOutcome, DateTime? decisionDate, DateTime? reviewDate)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 3 || title.Length > 200)
            errors.Add("Title must be 3-200 characters.");
        if (confidence is < 1 or > 100)
            errors.Add("Confidence score must be between 1 and 100.");
        if (expectedSuccess is < 1 or > 100)
            errors.Add("Expected success score must be between 1 and 100.");
        if (expectedOutcome?.Length > 1000)
            errors.Add("Expected outcome must not exceed 1000 characters.");
        if (reviewDate.HasValue && decisionDate.HasValue && reviewDate < decisionDate)
            errors.Add("Review date cannot be before the decision date.");
        if (categoryId <= 0)
            errors.Add("A valid category is required.");
        if (errors.Count > 0)
            throw new ValidationException("Decision failed validation.", errors);
    }

    public static void ValidateOptionRequest(string name, int? score, int weight)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 1 || name.Length > 100)
            errors.Add("Option name must be 1-100 characters.");
        if (score is < 0 or > 10)
            errors.Add("Option score must be between 0 and 10.");
        if (weight is < 0 or > 10)
            errors.Add("Option weight must be between 0 and 10.");
        if (errors.Count > 0)
            throw new ValidationException("Option failed validation.", errors);
    }

    public static void ValidateReasonRequest(string type, string category, string text)
    {
        var errors = new List<string>();
        if (!Enum.TryParse<ReasonType>(type, ignoreCase: true, out _))
            errors.Add("Reason type must be Pro, Con or Note.");
        if (string.IsNullOrWhiteSpace(category) || category.Length > 60)
            errors.Add("Reason category is required (max 60 characters).");
        if (string.IsNullOrWhiteSpace(text) || text.Length > 500)
            errors.Add("Reason text is required (max 500 characters).");
        if (errors.Count > 0)
            throw new ValidationException("Reason failed validation.", errors);
    }

    public static void ValidateReviewRequest(string actualOutcome, int outcomeRating)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(actualOutcome) || actualOutcome.Length > 1000)
            errors.Add("Actual outcome is required (max 1000 characters).");
        if (outcomeRating is < 1 or > 5)
            errors.Add("Outcome rating must be between 1 and 5.");
        if (errors.Count > 0)
            throw new ValidationException("Review failed validation.", errors);
    }

    public static DecisionStatus ParseStatus(string status, string? allowedHint = null)
    {
        if (!Enum.TryParse<DecisionStatus>(status, ignoreCase: true, out var parsed))
            throw new ValidationException($"Unknown status '{status}'.{(allowedHint is null ? "" : $" {allowedHint}")}");
        return parsed;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial System.Text.RegularExpressions.Regex EmailRegex();
}
