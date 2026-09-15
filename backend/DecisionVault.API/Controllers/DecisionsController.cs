using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DecisionVault.Application.DTOs;
using DecisionVault.Application.Interfaces;

namespace DecisionVault.API.Controllers;

[ApiController]
[Route("api/decisions")]
[Authorize]
public class DecisionsController(IDecisionService decisionService) : ControllerBase
{
    private int UserId => User.GetUserId();
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet]
    public async Task<ActionResult<PagedResult<DecisionListItemDto>>> GetDecisions(
        [FromQuery] string? search, [FromQuery] int? categoryId, [FromQuery] string? status,
        [FromQuery] string? outcome, [FromQuery] int? minConfidence, [FromQuery] int? maxConfidence,
        [FromQuery] string sortBy = "CreatedAt", [FromQuery] string sortDir = "desc",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var query = new DecisionQuery(search, categoryId, status, outcome, minConfidence, maxConfidence,
            sortBy, sortDir, page, pageSize);
        return Ok(await decisionService.GetPagedForUserAsync(UserId, query, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DecisionDto>> GetDecision(int id, CancellationToken ct) =>
        Ok(await decisionService.GetByIdForUserAsync(id, UserId, IsAdmin, ct));

    [HttpPost]
    [ProducesResponseType(typeof(DecisionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DecisionDto>> Create(DecisionCreateRequest request, CancellationToken ct)
    {
        var created = await decisionService.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(GetDecision), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DecisionDto>> Update(int id, DecisionUpdateRequest request, CancellationToken ct) =>
        Ok(await decisionService.UpdateAsync(id, UserId, request, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await decisionService.DeleteAsync(id, UserId, ct);
        return NoContent();
    }

    // ---------- Options ----------

    [HttpPost("{id:int}/options")]
    [ProducesResponseType(typeof(DecisionOptionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DecisionOptionDto>> AddOption(int id, DecisionOptionRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(GetDecision), new { id },
            await decisionService.AddOptionAsync(id, UserId, request, ct));

    [HttpPut("{id:int}/options/{optionId:int}")]
    public async Task<ActionResult<DecisionOptionDto>> UpdateOption(int id, int optionId, DecisionOptionRequest request, CancellationToken ct) =>
        Ok(await decisionService.UpdateOptionAsync(id, optionId, UserId, request, ct));

    [HttpDelete("{id:int}/options/{optionId:int}")]
    public async Task<IActionResult> DeleteOption(int id, int optionId, CancellationToken ct)
    {
        await decisionService.DeleteOptionAsync(id, optionId, UserId, ct);
        return NoContent();
    }

    // ---------- Reasons ----------

    [HttpPost("{id:int}/reasons")]
    [ProducesResponseType(typeof(DecisionReasonDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DecisionReasonDto>> AddReason(int id, DecisionReasonRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(GetDecision), new { id },
            await decisionService.AddReasonAsync(id, UserId, request, ct));

    [HttpDelete("{id:int}/reasons/{reasonId:int}")]
    public async Task<IActionResult> DeleteReason(int id, int reasonId, CancellationToken ct)
    {
        await decisionService.DeleteReasonAsync(id, reasonId, UserId, ct);
        return NoContent();
    }

    // ---------- Lifecycle ----------

    [HttpPost("{id:int}/select-option")]
    public async Task<ActionResult<DecisionDto>> SelectOption(int id, SelectOptionRequest request, CancellationToken ct) =>
        Ok(await decisionService.SelectOptionAsync(id, UserId, request, ct));

    [HttpPost("{id:int}/transition")]
    public async Task<ActionResult<DecisionDto>> Transition(int id, TransitionRequest request, CancellationToken ct) =>
        Ok(await decisionService.TransitionAsync(id, UserId, request, ct));

    [HttpPost("{id:int}/finalize")]
    public async Task<ActionResult<DecisionDto>> Finalize(int id, FinalizeRequest request, CancellationToken ct) =>
        Ok(await decisionService.FinalizeAsync(id, UserId, request, ct));

    // ---------- Review & Timeline ----------

    [HttpPost("{id:int}/review")]
    [ProducesResponseType(typeof(DecisionReviewDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DecisionReviewDto>> SubmitReview(int id, DecisionReviewRequest request, CancellationToken ct) =>
        CreatedAtAction(nameof(GetDecision), new { id },
            await decisionService.SubmitReviewAsync(id, UserId, request, ct));

    [HttpGet("{id:int}/review")]
    public async Task<ActionResult<DecisionReviewDto>> GetReview(int id, CancellationToken ct) =>
        Ok(await decisionService.GetReviewAsync(id, UserId, IsAdmin, ct));

    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<IReadOnlyList<DecisionEventDto>>> GetTimeline(int id, CancellationToken ct) =>
        Ok(await decisionService.GetTimelineAsync(id, UserId, IsAdmin, ct));
}
