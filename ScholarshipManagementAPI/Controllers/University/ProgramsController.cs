using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.Faculties;
using ScholarshipManagementAPI.DTOs.University.Programs;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Implementation.University;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Controllers.University
{
    [ApiController]
    [Route("api/university/programs")]
    public class ProgramsController : ControllerBase
    {
        private readonly IProgramsService _service;
        private readonly CurrentUserContextService _currentUser;

        public ProgramsController(IProgramsService service, CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }


        // -------- CREATE --------
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] ProgramRequestDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            dto.CreatedBy = currentUser.LoginId;
            var id = await _service.CreateAsync(dto, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = id,
                Message = "Program created successfully"
            });
        }


        // -------- UPDATE --------
        [HttpPut("update/{id:long}")]
        [Authorize]
        public async Task<IActionResult> Update(long id, [FromBody] ProgramRequestDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            dto.ProgramId = id;
            dto.UpdatedBy = currentUser.LoginId;

            var updated = await _service.UpdateAsync(dto, currentUser);

            if (!updated)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Record not found",
                    Result = null,
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Program updated successfully",
                Result = updated,
            });
        }


        // -------- DELETE --------
        [HttpDelete("delete/{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(long id)
        {
            var deleted = await _service.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Record not found",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Program deleted successfully",
                Result = deleted
            });
        }


        // -------- GET BY ID --------
        [HttpGet("getById/{id}")]
        [Authorize]
        public async Task<IActionResult> GetById(long id)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var data = await _service.GetByIdAsync(id,currentUser);

            if (data == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Record not found",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = data,
                Message = "Record found"
            });
        }


        // -------- FILTER / GET ALL --------
        [HttpPost("search")]
        [Authorize]
        public async Task<IActionResult> GetByFilter([FromBody] ProgramFilterDto filter)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.GetByFilterAsync(filter, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = result.Items.Count == 0 ? false : true,
                Result = result,
                Message = result.Items.Count == 0
                    ? "Data not found"
                    : "Data fetched successfully"
            });
        }



        [HttpGet("semesters/{programId:long}")]
        public async Task<IActionResult> GetSemesters(long programId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.GetSemestersAsync(programId, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = result.Count == 0 ? false : true,
                Result = result,
                Message = result.Count == 0
                    ? "Data not found"
                    : "Data fetched successfully"
            });
        }

        [HttpGet("semesters/{programId:long}/backfill-preview")]
        [Authorize]
        public async Task<IActionResult> PreviewSemesterBackfill(long programId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.PreviewSemesterBackfillAsync(programId, currentUser);

            if (result == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Program not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.IsEligibleForRecovery
                    ? "Program semester recovery preview generated successfully."
                    : "Program semester recovery preview contains blocking issues."
            });
        }

        [HttpPost("semesters/{programId:long}/backfill")]
        [Authorize]
        public async Task<IActionResult> BackfillMissingSemesters(long programId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.BackfillMissingSemestersAsync(programId, currentUser);

            if (result == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Program not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.CreatedSemesterNumbers.Count == 0
                    ? "Program semester records were already complete; no changes were needed."
                    : "Missing program semester records were created successfully."
            });
        }

        [HttpGet("{programId:long}/recovery/course-mappings/preview")]
        [Authorize]
        public async Task<IActionResult> PreviewCourseMappingRecovery(long programId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.PreviewCourseMappingRecoveryAsync(programId, currentUser);
            if (result == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Program not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.IsEligibleForRecovery
                    ? "Course-mapping recovery preview generated successfully."
                    : "Course-mapping recovery preview contains blocking issues."
            });
        }

        [HttpGet("recovery/registrations/{academicRegistrationId:long}/preview")]
        [Authorize]
        public async Task<IActionResult> PreviewRegistrationRecovery(long academicRegistrationId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.PreviewRegistrationRecoveryAsync(academicRegistrationId, currentUser);
            if (result == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Message = "Academic registration not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = result.IsEligibleForRecovery
                    ? "Academic registration recovery preview generated successfully."
                    : "Academic registration recovery preview contains blocking issues."
            });
        }

        [HttpPost("recovery/registrations/{academicRegistrationId:long}/apply")]
        [Authorize]
        public async Task<IActionResult> ApplyRegistrationRecovery(
            long academicRegistrationId,
            [FromBody] AcademicRegistrationRecoveryApplyRequestDto request)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.ApplyRegistrationRecoveryAsync(academicRegistrationId, request, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Academic data recovery completed."
            });
        }

        [HttpGet("semesters/{programId:long}/defaults")]
        public async Task<IActionResult> GetSemesterDefaults(long programId, [FromQuery] long academicCalendarId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var result = await _service.GetSemesterDefaultsAsync(programId, academicCalendarId, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = result.Count > 0,
                Result = result,
                Message = result.Count > 0 ? "Program semester defaults generated successfully." : "Data not found"
            });
        }

        [HttpPut("semesters/{programId:long}")]
        [Authorize]
        public async Task<IActionResult> SaveSemesters(long programId, [FromBody] List<ProgramSemesterDto> semesters, [FromQuery] long? academicCalendarId = null)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var saved = await _service.SaveSemestersAsync(programId, semesters, currentUser, academicCalendarId);

            return Ok(new ApiResponseDto
            {
                Success = saved,
                Result = saved,
                Message = "Program semester dates saved successfully."
            });
        }



    }
}
