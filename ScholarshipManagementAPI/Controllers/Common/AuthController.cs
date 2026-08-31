using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Common;

namespace ScholarshipManagementAPI.Controllers.Common
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }


        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequestDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            
            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Logged in successfully",
                Result = result
            });
        }



        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            //var loginIdClaim = User.FindFirst("LoginId")?.Value;

            var loginId = JwtClaimHelper.LoginId(User);

            await _authService.LogoutAsync(loginId);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Logged out successfully",
                Result = null
            });
        }



        [HttpPost("switch-role")]
        [Authorize]
        public async Task<IActionResult> SwitchRole([FromQuery] long roleId)
        {
            var loginId = JwtClaimHelper.LoginId(User);

            var result = await _authService.SwitchRoleAsync(loginId, roleId);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Role switched successfully",
                Result = result
            });
        }



        [HttpPost("forgot-username")]
        public async Task<IActionResult> ForgotUsername([FromBody] UserIdentifierDto request)
        {
            var result = await _authService.ForgotUserNameAsync(request);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "If the email exists, you will receive your username shortly.",
                Result = result
            });
        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] UserIdentifierDto request)
        {
            var result = await _authService.ForgotUserPasswordAsync(request);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "If the email exists, you will receive your password rest link shortly.",
                Result = result
            });
        }


        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            var result = await _authService.ResetUserPasswordAsync(request);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Password has been reset successfully. Please login again.",
                Result = result
            });
        }



        [HttpPost("login-with-code")]
        public async Task<IActionResult> LoginWithCode([FromBody] UserIdentifierDto request)
        {
            var result = await _authService.LoginWithCodeAsync(request);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "If the email exists, you will receive a 6-digit code shortly.",
                Result = result
            });
        }



        [HttpPost("verify-login-code")]
        public async Task<IActionResult> VerifyLoginCode([FromBody] VerifyOtpDto request)
        {
            var loginResult = await _authService.VerifyLoginCodeAsync(request);

            if (loginResult == null)
            {
                return Ok(new ApiResponseDto
                {
                    Success = false,
                    Message = "Invalid or expired verification code.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Login successful.",
                Result = loginResult
            });
        }



        [Authorize]
        [HttpGet("my-profile")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var loginId = JwtClaimHelper.LoginId(User);
            var roleId = JwtClaimHelper.RoleId(User);

            var result = await _authService.GetMyProfileAsync(loginId,roleId);

            if (result == null)
            {
                return Ok(new ApiResponseDto
                {
                    Success = false,
                    Message = "Profile not found.",
                    Result = null
                });
            }

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Profile fetched successfully.",
                Result = result
            });
        }


        [HttpPost("update/my-profile")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile(UpdateMyProfileDto dto)
        {
            var loginId = JwtClaimHelper.LoginId(User);
            var loginName = JwtClaimHelper.RoleId(User);

            var result = await _authService.UpdateMyProfileAsync(loginId, dto);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Profile updated successfully",
                Result = result
            });
        }


        [HttpPost("update-password")]
        [Authorize]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequestDto request)
        {
            // request.LoginId = JwtClaimHelper.LoginId(User);
            var loginId = JwtClaimHelper.LoginId(User);

            var result = await _authService.UpdatePasswordAsync(request, loginId);

            return Ok(new ApiResponseDto
            {
                Success = true,
                Message = "Password has been updated successfully.",
                Result = result
            });
        }


    }
}
