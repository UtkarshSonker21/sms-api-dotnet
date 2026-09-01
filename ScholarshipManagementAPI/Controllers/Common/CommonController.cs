
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common;
using ScholarshipManagementAPI.DTOs.Common.GlobalSearch;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Implementation.Common;
using ScholarshipManagementAPI.Services.Interface.Common;
using ScholarshipManagementAPI.Services.Interface.SuperAdmin;
using System;

namespace ScholarshipManagementAPI.Controllers.Common
{
    [ApiController]
    [Route("api/common")]
    public class CommonController : ControllerBase
    {
        private readonly ICommonService _service;
        private readonly CurrentUserContextService _currentUser;

        public CommonController(ICommonService service, CurrentUserContextService currentUser)
        {
            _service = service;
            _currentUser = currentUser;
        }



        [HttpGet("user-modules")]
        [Authorize]
        public async Task<IActionResult> GetAllUsersModule()
        {
            var data = await _service.GetAllUsersModule();

            return Ok(new ApiResponseDto
            {
                Success = data.Any(),
                Message = data.Any()
                    ? "Modules fetched successfully"
                    : "No modules found",
                Result = data
            });
        }

  
        [HttpGet("load-menus")]
        [Authorize]
        public async Task<IActionResult> LoadMenusByRole()
        {
            var roleId = JwtClaimHelper.RoleId(User);
            //var roleId = 1;

            var menus = await _service.LoadMenusByRoleAsync(roleId);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Menus loaded successfully",
                Result = menus
            });
        }



        [HttpGet("test-error")]
        public IActionResult TestError()
        {
            throw new Exception("🚨 Test exception for email alert verification");
        }






        [HttpPost("media/users/profile/{userId}")]
        public async Task<IActionResult> UploadProfileImage(int userId, IFormFile file)
        {
            var fileKey = await _service.UploadUserProfileImageAsync(userId, file);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Profile image uploaded successfully",
                Result = fileKey
            });
        }


        [HttpPost("media/users/documents/{userId}")]
        public async Task<IActionResult> UploadUserDocument(int userId, IFormFile file)
        {
            var fileKey = await _service
                .UploadUserDocumentAsync(userId, file);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Document uploaded successfully",
                Result = fileKey
            });
        }




        [HttpPost("global-search")]
        [Authorize]
        public async Task<IActionResult> GlobalSearch([FromBody] GlobalSearchRequestDto request)
        {
            var currentUser = await _currentUser.GetCurrentUserAsync();

            var result = await _service.GlobalSearchAsync(
                request,
                currentUser);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Result = result,
                Message = "Search completed successfully."
            });
        }






    }
}
