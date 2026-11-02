using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.AcademicRegistration;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Controllers.University
{
    [ApiController]
    [Route("api/university/academic-registration")]
    public class AcademicRegistrationController : ControllerBase
    {
        private readonly IStudentAcademicRegistrationService _service;
        private readonly CurrentUserContextService _currentUser;

        public AcademicRegistrationController(IStudentAcademicRegistrationService service, CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }


        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] AcademicRegistrationFilterDto filter)
        {
            var result = await _service.SearchAsync(filter, await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = result.Items.Count == 0 ? false : true,
                Result = result,
                Message = result.Items.Count == 0
                    ? "Data not found"
                    : "Data fetched successfully"
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentRequestDto dto)
        {
            var result = await _service.RegisterStudentAsync(dto, await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = true,
                Message = $"Student registered successfully. {result.NewlyCreatedCount} course enrollments created; {result.AlreadyExistingCount} already existed."
            });
        }

        [Authorize]
        [HttpGet("results/import-template")]
        public IActionResult DownloadResultsImportTemplate()
        {
            return File(
                AcademicResultsWorkbook.CreateTemplate(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "sms-academic-results-template.xlsx");
        }

        [Authorize]
        [HttpPost("results/import/preview")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> PreviewResultsImport(IFormFile file)
        {
            var result = await _service.PreviewResultsImportAsync(
                file,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto { Success = true, Result = result, Message = result.Message ?? "Workbook validation completed." });
        }

        [Authorize]
        [HttpPost("results/import")]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> ImportResults(
            IFormFile file,
            [FromForm] string? previewToken,
            [FromForm] DateTime? expectedUpdatedOn)
        {
            var result = await _service.ImportResultsAsync(
                file,
                previewToken,
                expectedUpdatedOn,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Imported results were saved as a Draft. Review them in Academic Record before finalizing."
            });
        }

        [Authorize]
        [HttpGet("{academicRegistrationId:long}/results")]
        public async Task<IActionResult> GetResults(long academicRegistrationId)
        {
            var result = await _service.GetResultsAsync(
                academicRegistrationId,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Academic results fetched successfully."
            });
        }

        [Authorize]
        [HttpGet("by-application/{applicationId:long}/results")]
        public async Task<IActionResult> GetResultsByApplication(long applicationId)
        {
            var result = await _service.GetResultsByApplicationAsync(
                applicationId,
                await _currentUser.GetCurrentUserAsync());

            if (result == null)
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = null,
                    Message = "No academic registration was found for this application."
                });

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Academic results fetched successfully."
            });
        }

        [Authorize]
        [HttpGet("by-application/{applicationId:long}/history")]
        public async Task<IActionResult> GetAcademicHistoryByApplication(long applicationId)
        {
            var result = await _service.GetAcademicHistoryByApplicationAsync(
                applicationId,
                await _currentUser.GetCurrentUserAsync());

            if (result == null)
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = null,
                    Message = "The requested application was not found."
                });

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.Registrations.Count == 0
                    ? "No academic history was found for this student."
                    : "Academic history fetched successfully."
            });
        }

        [Authorize]
        [HttpPost("{academicRegistrationId:long}/results")]
        public async Task<IActionResult> SaveResults(
            long academicRegistrationId,
            [FromBody] SaveAcademicResultsRequestDto dto)
        {
            var result = await _service.SaveResultsAsync(
                academicRegistrationId,
                dto,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Academic results saved as draft."
            });
        }

        [Authorize]
        [HttpPost("{academicRegistrationId:long}/results/finalize")]
        public async Task<IActionResult> FinalizeResults(
            long academicRegistrationId,
            [FromBody] FinalizeAcademicResultsRequestDto dto)
        {
            var result = await _service.FinalizeResultsAsync(
                academicRegistrationId,
                dto,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Semester results finalized successfully."
            });
        }

        [Authorize]
        [HttpPost("{academicRegistrationId:long}/progress")]
        public async Task<IActionResult> ProgressToNextSemester(
            long academicRegistrationId,
            [FromBody] ProgressAcademicRegistrationRequestDto dto)
        {
            var result = await _service.ProgressToNextSemesterAsync(
                academicRegistrationId,
                dto,
                await _currentUser.GetCurrentUserAsync());

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.ConfirmationRequired
                    ? "Registration restrictions require explicit confirmation. No registration was created."
                    : "Student registered for the next semester successfully."
            });
        }

    }
}
