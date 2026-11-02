using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using Microsoft.AspNetCore.Http;
using ScholarshipManagementAPI.DTOs.University.AcademicRegistration;

namespace ScholarshipManagementAPI.Services.Interface.University
{
    public interface IStudentAcademicRegistrationService
    {
        Task<PagedResultDto<AcademicRegistrationDto>> SearchAsync(AcademicRegistrationFilterDto filter, LoggedInUserDto currentUser);

        Task<CourseEnrollmentSummaryDto> RegisterStudentAsync(RegisterStudentRequestDto dto, LoggedInUserDto currentUser);

        Task<AcademicRegistrationResultsDto> GetResultsAsync(long academicRegistrationId, LoggedInUserDto currentUser);

        Task<AcademicRegistrationResultsDto?> GetResultsByApplicationAsync(long applicationId, LoggedInUserDto currentUser);

        Task<AcademicHistoryDto?> GetAcademicHistoryByApplicationAsync(long applicationId, LoggedInUserDto currentUser);

        Task<AcademicResultsSaveSummaryDto> SaveResultsAsync(long academicRegistrationId, SaveAcademicResultsRequestDto dto, LoggedInUserDto currentUser);

        Task<AcademicResultsImportPreviewDto> PreviewResultsImportAsync(IFormFile file, LoggedInUserDto currentUser);

        Task<AcademicResultsSaveSummaryDto> ImportResultsAsync(IFormFile file, string? previewToken, DateTime? expectedUpdatedOn, LoggedInUserDto currentUser);

        Task<AcademicRegistrationResultsDto> FinalizeResultsAsync(long academicRegistrationId, FinalizeAcademicResultsRequestDto dto, LoggedInUserDto currentUser);

        Task<AcademicProgressionResponseDto> ProgressToNextSemesterAsync(long academicRegistrationId, ProgressAcademicRegistrationRequestDto dto, LoggedInUserDto currentUser);

    }
}
