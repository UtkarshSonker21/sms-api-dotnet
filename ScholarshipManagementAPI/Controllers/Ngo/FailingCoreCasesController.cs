using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.Ngo.FailingCoreCases;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Ngo;

namespace ScholarshipManagementAPI.Controllers.Ngo;

[ApiController]
[Authorize]
[Route("api/ngo/failing-core-cases")]
public sealed class FailingCoreCasesController : ControllerBase
{
    private readonly IFailingCoreCaseService _service;
    private readonly CurrentUserContextService _currentUser;

    public FailingCoreCasesController(IFailingCoreCaseService service, CurrentUserContextService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetCases([FromQuery] string? status)
    {
        var result = await _service.GetCasesAsync(status, await _currentUser.GetCurrentUserAsync());
        return Ok(new ApiResponseDto { Success = true, Result = result, Message = "Failing-core cases fetched successfully." });
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var result = await _service.GetByIdAsync(id, await _currentUser.GetCurrentUserAsync());
        if (result == null)
            return NotFound(new ApiResponseDto { Success = false, Result = null, Message = "Failing-core case not found." });
        return Ok(new ApiResponseDto { Success = true, Result = result, Message = "Failing-core case fetched successfully." });
    }

    [HttpPost("{id:long}/decision")]
    public async Task<IActionResult> RecordDecision(long id, [FromBody] FailingCoreCaseDecisionRequestDto request)
    {
        var result = await _service.RecordDecisionAsync(id, request, await _currentUser.GetCurrentUserAsync());
        return Ok(new ApiResponseDto { Success = true, Result = result, Message = "Committee decision recorded successfully." });
    }
}
