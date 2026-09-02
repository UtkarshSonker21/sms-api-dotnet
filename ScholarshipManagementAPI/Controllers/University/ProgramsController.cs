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
            dto.CreatedBy = JwtClaimHelper.LoginId(User);                 // or from claims
            var id = await _service.CreateAsync(dto);

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
            dto.ProgramId = id;
            dto.UpdatedBy = JwtClaimHelper.LoginId(User);                 // or from claims

            var updated = await _service.UpdateAsync(dto);

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



    }
}
