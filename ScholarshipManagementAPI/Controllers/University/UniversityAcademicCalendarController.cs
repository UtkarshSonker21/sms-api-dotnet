using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.AcademicConfig;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Controllers.University
{
    [ApiController]
    [Authorize]
    [Route("api/university/academic-calendars")]
    public class UniversityAcademicCalendarController : ControllerBase
    {
        private readonly IUniversityAcademicCalendarService _service;
        private readonly CurrentUserContextService _currentUser;

        public UniversityAcademicCalendarController(
            IUniversityAcademicCalendarService service,
            CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AcademicCalendarRequestDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var id = await _service.CreateAsync(dto, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = id,
                Message = "Academic calendar created successfully."
            });
        }

        [HttpPut("{academicCalendarId:long}")]
        public async Task<IActionResult> Update(long academicCalendarId, [FromBody] AcademicCalendarRequestDto dto)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var updated = await _service.UpdateAsync(academicCalendarId, dto, currentUser);

            if (!updated)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = null,
                    Message = "Academic calendar not found."
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = true,
                Message = "Academic calendar updated successfully."
            });
        }

        [HttpGet("{academicCalendarId:long}")]
        public async Task<IActionResult> GetById(long academicCalendarId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var calendar = await _service.GetByIdAsync(academicCalendarId, currentUser);

            if (calendar == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = null,
                    Message = "Academic calendar not found."
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = calendar,
                Message = "Academic calendar retrieved successfully."
            });
        }

        [HttpGet("by-university/{universityId:long}")]
        public async Task<IActionResult> GetByUniversity(long universityId)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var calendars = await _service.GetByUniversityAsync(universityId, currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = calendars,
                Message = "Academic calendars retrieved successfully."
            });
        }

        [HttpGet("by-academic-year")]
        public async Task<IActionResult> GetByAcademicYear([FromQuery] long universityId,[FromQuery] string academicYearName)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();
            var calendar = await _service.GetByAcademicYearAsync(universityId, academicYearName, currentUser);

            if (calendar == null)
            {
                return NotFound(new ApiResponseDto
                {
                    Success = false,
                    Result = null,
                    Message = "Academic calendar not found."
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = calendar,
                Message = "Academic calendar retrieved successfully."
            });
        }
    
    }
}
