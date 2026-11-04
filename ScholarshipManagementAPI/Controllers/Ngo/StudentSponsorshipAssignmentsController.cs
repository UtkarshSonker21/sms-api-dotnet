using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.Ngo.SponsorshipAssignments;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Ngo;

namespace ScholarshipManagementAPI.Controllers.Ngo;

[ApiController]
[Route("api/ngo/student-sponsorship-assignments")]
[Authorize]
public sealed class StudentSponsorshipAssignmentsController : ControllerBase
{
    private readonly IStudentSponsorshipAssignmentService _service;
    private readonly CurrentUserContextService _currentUser;

    public StudentSponsorshipAssignmentsController(
        IStudentSponsorshipAssignmentService service,
        CurrentUserContextService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("options/by-category/{studentCategoryId:long}")]
    public async Task<IActionResult> GetOptionsByCategory(long studentCategoryId, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.GetOptionsByCategoryAsync(studentCategoryId, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = result.Count == 0
                ? "No active sponsorship mappings are available for this category."
                : "Sponsorship options loaded successfully."
        });
    }

    [HttpGet("applications/{applicationId:long}")]
    public async Task<IActionResult> GetByApplication(long applicationId, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.GetByApplicationAsync(applicationId, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "Sponsorship assignments loaded successfully."
        });
    }

    [HttpPost("applications/{applicationId:long}")]
    public async Task<IActionResult> Create(
        long applicationId,
        [FromBody] CreateSponsorshipAssignmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.CreateAsync(applicationId, request, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "DA Pool sponsorship assignment saved successfully."
        });
    }

    [HttpPost("applications/{applicationId:long}/donor")]
    public async Task<IActionResult> AssignDonor(
        long applicationId,
        [FromBody] AssignDonorSponsorshipRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        request.ApplicationId = applicationId;
        var result = await _service.AssignDonorAsync(request, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "Donor sponsorship assignment saved successfully."
        });
    }

    [HttpPost("donor")]
    public async Task<IActionResult> AssignDonorDirect(
        [FromBody] AssignDonorSponsorshipRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.AssignDonorAsync(request, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "Donor sponsorship assignment saved successfully."
        });
    }

    [HttpPut("{assignmentId:long}")]
    public async Task<IActionResult> Update(
        long assignmentId,
        [FromBody] CreateSponsorshipAssignmentRequestDto request,
        CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.UpdateAsync(assignmentId, request, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "Sponsorship assignment updated successfully."
        });
    }

    [HttpPut("{assignmentId:long}/deactivate")]
    public async Task<IActionResult> Deactivate(long assignmentId, CancellationToken cancellationToken)
    {
        var currentUser = await _currentUser.GetCurrentUserAsync();
        var result = await _service.DeactivateAsync(assignmentId, currentUser, cancellationToken);

        return Ok(new ApiResponseDto
        {
            Success = true,
            Result = result,
            Message = "Sponsorship assignment deactivated successfully."
        });
    }
}
