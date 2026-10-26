using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.RegistrationWindows;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Controllers.University
{
    [ApiController]
    [Authorize]
    [Route("api/university/registration-windows")]
    public class UniversityRegistrationWindowController : ControllerBase
    {
        private readonly IUniversityRegistrationWindowService _service;
        private readonly CurrentUserContextService _currentUser;

        public UniversityRegistrationWindowController(IUniversityRegistrationWindowService service, CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var result = await _service.GetForUserAsync(await _currentUser.GetCurrentUserAsync());
            return Ok(new ApiResponseDto { Success = true, Result = result, Message = "Registration windows retrieved successfully." });
        }

        [HttpPut]
        public async Task<IActionResult> OpenOrUpdate([FromBody] UniversityRegistrationWindowRequestDto request)
        {
            var id = await _service.OpenOrUpdateAsync(request, await _currentUser.GetCurrentUserAsync());
            return Ok(new ApiResponseDto { Success = true, Result = id, Message = "Registration window opened successfully." });
        }

        [HttpPut("{id:long}/close")]
        public async Task<IActionResult> Close(long id, [FromBody] CloseUniversityRegistrationWindowRequestDto request)
        {
            await _service.CloseAsync(id, request, await _currentUser.GetCurrentUserAsync());
            return Ok(new ApiResponseDto { Success = true, Result = true, Message = "Registration window closed successfully." });
        }
    }
}
