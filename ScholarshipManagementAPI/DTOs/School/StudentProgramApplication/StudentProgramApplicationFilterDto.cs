using ScholarshipManagementAPI.DTOs.Common.Filter;

namespace ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;

public class StudentProgramApplicationFilterDto : BaseFilterDto
{
    public long? SchoolCoordinatorId { get; set; }

    public long? CountryId { get; set; }

    public long? UniversityId { get; set; }

    public long? FacultyId { get; set; }

    public long? ProgramId { get; set; }

    public long? ApplicationStatusId { get; set; }

    public long? StudentCategoryId { get; set; }

    public bool? IsDonorSponsored { get; set; }
}
