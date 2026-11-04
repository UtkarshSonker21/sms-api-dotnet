using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Ngo.SponsorshipAssignments;

namespace ScholarshipManagementAPI.Services.Interface.Ngo;

public interface IStudentSponsorshipAssignmentService
{
    Task<IReadOnlyList<SponsorshipAssignmentOptionDto>> GetOptionsByCategoryAsync(
        long studentCategoryId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SponsorshipAssignmentDto>> GetByApplicationAsync(
        long applicationId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<SponsorshipAssignmentDto> CreateAsync(
        long applicationId,
        CreateSponsorshipAssignmentRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<SponsorshipAssignmentDto> UpdateAsync(
        long assignmentId,
        CreateSponsorshipAssignmentRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<SponsorshipAssignmentDto> DeactivateAsync(
        long assignmentId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);

    Task<SponsorshipAssignmentDto> AssignDonorAsync(
        AssignDonorSponsorshipRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default);
}
