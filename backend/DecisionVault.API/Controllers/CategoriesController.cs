using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;

namespace DecisionVault.API.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await categoryService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryUpsertRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(GetAll), await categoryService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryUpsertRequest request, CancellationToken ct) =>
        Ok(await categoryService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await categoryService.DeleteAsync(id, ct);
        return NoContent();
    }
}
