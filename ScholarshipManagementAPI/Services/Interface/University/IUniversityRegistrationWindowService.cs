using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.University.RegistrationWindows;

namespace ScholarshipManagementAPI.Services.Interface.University
{
    public interface IUniversityRegistrationWindowService
    {
        Task<UniversityRegistrationWindowsResponseDto> GetForUserAsync(LoggedInUserDto currentUser);
        Task<long> OpenOrUpdateAsync(UniversityRegistrationWindowRequestDto request, LoggedInUserDto currentUser);
        Task CloseAsync(long id, CloseUniversityRegistrationWindowRequestDto request, LoggedInUserDto currentUser);
    }
}
