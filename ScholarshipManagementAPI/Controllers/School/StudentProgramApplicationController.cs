using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.School;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ScholarshipManagementAPI.Controllers.School
{
    [ApiController]
    [Route("api/school/student-program")]
    public class StudentProgramApplicationController : ControllerBase
    {
        private readonly IStudentProgramApplicationService _service;
        private readonly CurrentUserContextService _currentUser;

        public StudentProgramApplicationController(IStudentProgramApplicationService service, CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }


        [HttpGet("candidate-programs/{studentId:long}")]
        [Authorize]
        public async Task<IActionResult> GetCandidatePrograms(long studentId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var data = await _service.GetCandidateProgramsAsync(studentId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = data,
                Message = "Candidate programs retrieved successfully."
            });
        }


        [HttpPost("apply/{studentId:long}")]
        [Authorize]
        public async Task<IActionResult> Apply(long studentId, [FromBody] ApplyRequestDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var id = await _service.ApplyAsync(studentId, dto, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = id,
                Message = "Program application draft created successfully."
            });
        }


        [HttpDelete("cancel/{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> CancelApplication(long applicationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var success = await _service.CancelApplicationAsync(applicationId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = success,
                Result = success,
                Message = "Application draft cancelled and deleted successfully."
            });
        }


        [HttpPost("submit/{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> SubmitApplication(long applicationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var success = await _service.SubmitApplicationAsync(applicationId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = success,
                Result = success,
                Message = "Application submitted successfully."
            });
        }


        [HttpGet("getById/{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> GetApplication(long applicationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var data = await _service.GetApplicationAsync(applicationId, currentUser);
            if (data == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Application not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = data,
                Message = "Application retrieved successfully."
            });
        }


        [HttpPost("upload-document/{applicationId:long}")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument(long applicationId, [FromForm] UploadDocumentRequestDto request)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();

            var doc = await _service.UploadDocumentAsync(
                applicationId,
                request.ProgramDocumentId,
                request.DocumentTypeId,
                request.File,
                currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = doc,
                Message = "Document uploaded successfully."
            });
        }


        [HttpDelete("delete-document/{applicationId:long}/{documentId:long}")]
        [Authorize]
        public async Task<IActionResult> DeleteDocument(long applicationId, long documentId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var success = await _service.DeleteDocumentAsync(applicationId, documentId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = success,
                Result = success,
                Message = "Document deleted successfully."
            });
        }


        [HttpGet("documents/{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> GetDocuments(long applicationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var data = await _service.GetDocumentsAsync(applicationId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = data,
                Message = "Documents retrieved successfully."
            });
        }


        [HttpGet("history/{studentId:long}")]
        [Authorize]
        public async Task<IActionResult> GetHistory(long studentId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var data = await _service.GetHistoryAsync(studentId, currentUser);
            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = data,
                Message = "Student history retrieved successfully."
            });
        }



        
        [HttpPost("search")]
        [Authorize]
        public async Task<IActionResult> Search([FromBody] StudentProgramApplicationFilterDto filter)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.SearchAsync(filter, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Records loaded successfully."
            });
        }


        [HttpGet("{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> GetById(long applicationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.GetByIdAsync(applicationId, currentUser);

            if (result == null)
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = result,
                    Message = "Application not found."
                });

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Record found."
            });
        }


        [HttpPut("change-status/{applicationId:long}")]
        [Authorize]
        public async Task<IActionResult> ChangeStatus(long applicationId,[FromBody] ChangeStudentProgramStatusDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            await _service.ChangeStatusAsync(applicationId, dto, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = true,
                Message = "Application status updated successfully."
            });
        }


    }
}
