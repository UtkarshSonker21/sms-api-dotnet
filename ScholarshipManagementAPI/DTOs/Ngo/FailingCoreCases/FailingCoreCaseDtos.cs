namespace ScholarshipManagementAPI.DTOs.Ngo.FailingCoreCases;

public class FailingCoreCaseDto
{
    public long Id { get; set; }
    public long AcademicRegistrationId { get; set; }
    public long ApplicationId { get; set; }
    public long StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public long UniversityId { get; set; }
    public string UniversityName { get; set; } = string.Empty;
    public long ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int SemesterNo { get; set; }
    public string? SemesterName { get; set; }
    public string? TermName { get; set; }
    public string? AcademicYearName { get; set; }
    public DateTime? SemesterStartDate { get; set; }
    public DateTime? SemesterEndDate { get; set; }
    public DateTime SubmittedOn { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? DecisionComment { get; set; }
    public DateTime? DecisionOn { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<FailingCoreCaseCourseDto> FailedCoreCourses { get; set; } = new();
}

public sealed class FailingCoreCaseDetailDto : FailingCoreCaseDto
{
    public decimal? SemesterGpa { get; set; }
    public List<FailingCoreCaseSemesterCourseDto> SemesterCourses { get; set; } = new();
}

public sealed class FailingCoreCaseCourseDto
{
    public long StudentCourseResultId { get; set; }
    public long ProgramCourseId { get; set; }
    public long CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
}

public sealed class FailingCoreCaseDecisionRequestDto
{
    public string Status { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public byte[]? ExpectedRowVersion { get; set; }
}

public sealed class FailingCoreCaseSemesterCourseDto
{
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string CourseType { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool? IsPassed { get; set; }
    public bool IsRetake { get; set; }
}
