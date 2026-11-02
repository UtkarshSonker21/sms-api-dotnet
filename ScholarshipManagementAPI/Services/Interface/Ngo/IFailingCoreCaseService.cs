using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Ngo.FailingCoreCases;

namespace ScholarshipManagementAPI.Services.Interface.Ngo;

public interface IFailingCoreCaseService
{
    Task<IReadOnlyList<FailingCoreCaseDto>> GetCasesAsync(string? status, LoggedInUserDto currentUser);
    Task<FailingCoreCaseDetailDto?> GetByIdAsync(long id, LoggedInUserDto currentUser);
    Task<FailingCoreCaseDetailDto> RecordDecisionAsync(long id, FailingCoreCaseDecisionRequestDto request, LoggedInUserDto currentUser);
}
