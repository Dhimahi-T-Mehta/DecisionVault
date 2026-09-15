using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;
using DecisionVault.Domain;
using DecisionVault.Domain.Entities;
using DecisionVault.Domain.Enums;
using DecisionVault.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace DecisionVault.Application.Services;

public class CategoryService(IUnitOfWork uow) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default) =>
        await uow.Categories.Query()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Kind.ToString(), c.Description))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateAsync(CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var (name, kind, description) = Validate(request);
        await EnsureUniqueNameAsync(name, null, ct);

        var category = new Category
        {
            Name = name,
            Kind = kind,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        await uow.Categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Kind.ToString(), category.Description);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var (name, kind, description) = Validate(request);
        await EnsureUniqueNameAsync(name, id, ct);

        var category = await uow.Categories.QueryWhere(c => c.Id == id, asNoTracking: false).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Category not found.");

        category.Name = name;
        category.Kind = kind;
        category.Description = description;
        category.UpdatedAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Kind.ToString(), category.Description);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await uow.Categories.QueryWhere(c => c.Id == id, asNoTracking: false).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Category not found.");

        var inUse = await uow.Decisions.QueryWhere(d => d.CategoryId == id).AnyAsync(ct);
        if (inUse)
            throw new ConflictException("Category is used by existing decisions and cannot be deleted.");

        uow.Categories.Remove(category);
        await uow.SaveChangesAsync(ct);
    }

    private static (string Name, CategoryKind Kind, string? Description) Validate(CategoryUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 60)
            throw new Domain.Exceptions.ValidationException("Category name must be 2-60 characters.");
        if (!Enum.TryParse<CategoryKind>(request.Kind, ignoreCase: true, out var kind))
            throw new Domain.Exceptions.ValidationException("Unknown category kind.");
        if (request.Description?.Length > 300)
            throw new Domain.Exceptions.ValidationException("Description must not exceed 300 characters.");
        return (request.Name.Trim(), kind, request.Description?.Trim());
    }

    private async Task EnsureUniqueNameAsync(string name, int? excludeId, CancellationToken ct)
    {
        var taken = await uow.Categories.QueryWhere(c => c.Name == name && (excludeId == null || c.Id != excludeId)).AnyAsync(ct);
        if (taken)
            throw new ConflictException("A category with this name already exists.");
    }
}
