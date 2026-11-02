using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.Programs;

namespace ScholarshipManagementAPI.Services.Interface.University
{
    public interface IProgramsService
    {
        Task<long> CreateAsync(ProgramRequestDto dto, LoggedInUserDto currentUser);

        Task<bool> UpdateAsync(ProgramRequestDto dto, LoggedInUserDto currentUser);

        Task<bool> DeleteAsync(long id);

        Task<ProgramRequestDto?> GetByIdAsync(long id, LoggedInUserDto currentUser);

        Task<PagedResultDto<ProgramRequestDto>> GetByFilterAsync(ProgramFilterDto filter, LoggedInUserDto currentUser);



        Task<List<ProgramSemesterDto>> GetSemestersAsync(long programId, LoggedInUserDto currentUser);

        Task<ProgramSemesterBackfillDto?> PreviewSemesterBackfillAsync(long programId, LoggedInUserDto currentUser);

        Task<ProgramSemesterBackfillDto?> BackfillMissingSemestersAsync(long programId, LoggedInUserDto currentUser);

        Task<AcademicProgramRecoveryPreviewDto?> PreviewCourseMappingRecoveryAsync(long programId, LoggedInUserDto currentUser);

        Task<AcademicRegistrationRecoveryPreviewDto?> PreviewRegistrationRecoveryAsync(long academicRegistrationId, LoggedInUserDto currentUser);

        Task<AcademicDataRecoveryApplyResultDto> ApplyRegistrationRecoveryAsync(
            long academicRegistrationId,
            AcademicRegistrationRecoveryApplyRequestDto request,
            LoggedInUserDto currentUser);

        Task<List<ProgramSemesterDto>> GetSemesterDefaultsAsync(long programId, long academicCalendarId, LoggedInUserDto currentUser);

        Task<bool> SaveSemestersAsync(long programId, List<ProgramSemesterDto> semesters, LoggedInUserDto currentUser, long? academicCalendarId = null);

    }
}
