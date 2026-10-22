using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.University.AcademicConfig;

namespace ScholarshipManagementAPI.Services.Interface.University
{
    public interface IUniversityAcademicCalendarService
    {
        Task<long> CreateAsync(AcademicCalendarRequestDto dto, LoggedInUserDto currentUser);

        Task<bool> UpdateAsync(long academicCalendarId, AcademicCalendarRequestDto dto, LoggedInUserDto currentUser);

        Task<AcademicCalendarResponseDto?> GetByIdAsync(long academicCalendarId, LoggedInUserDto currentUser);

        Task<List<AcademicCalendarResponseDto>> GetByUniversityAsync(long universityId, LoggedInUserDto currentUser);

        Task<AcademicCalendarResponseDto?> GetByAcademicYearAsync(long universityId, string academicYearName, LoggedInUserDto currentUser);
    }
}
