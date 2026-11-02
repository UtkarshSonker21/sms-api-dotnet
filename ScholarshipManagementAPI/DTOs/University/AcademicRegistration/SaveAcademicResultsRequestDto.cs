namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class SaveAcademicResultsRequestDto
    {
        public decimal? SemesterGpa { get; set; }

        public string? InternalNote { get; set; }

        public DateTime? ExpectedUpdatedOn { get; set; }

        public List<SaveCourseResultRequestDto> CourseResults { get; set; } = new();
    }
}
