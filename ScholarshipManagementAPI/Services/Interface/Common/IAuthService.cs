using ScholarshipManagementAPI.DTOs.Common.Auth;

namespace ScholarshipManagementAPI.Services.Interface.Common
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto dto);

        Task LogoutAsync(long loginId);

        Task<LoginResponseDto> SwitchRoleAsync(long loginId, long roleId);


        Task<bool> ForgotUserNameAsync(UserIdentifierDto request);

        Task<bool> ForgotUserPasswordAsync(UserIdentifierDto request);


        Task<bool> ResetUserPasswordAsync(ResetPasswordRequestDto request);

        


        Task<bool> LoginWithCodeAsync(UserIdentifierDto request);


        Task<LoginResponseDto?> VerifyLoginCodeAsync(VerifyOtpDto request);



        Task<CurrentUserProfileDto?> GetMyProfileAsync(long loginId, long roleId);
        Task<bool> UpdateMyProfileAsync(long loginId, UpdateMyProfileDto dto);
        Task<bool> UpdatePasswordAsync(UpdatePasswordRequestDto request, long loginId);


    }
}
