namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicRegistrationResultsDto
    {
        public long AcademicRegistrationId { get; set; }

        public long ApplicationId { get; set; }

        public long ProgramId { get; set; }

        public int SemesterNo { get; set; }

        public long? ProgramSemesterId { get; set; }

        public decimal? SemesterGpa { get; set; }

        public string? InternalNote { get; set; }

        public DateTime? ExpectedUpdatedOn { get; set; }

        public string? SubmissionStatus { get; set; }

        public DateTime? SubmittedOn { get; set; }

        public List<CourseResultEntryDto> Courses { get; set; } = new();
    }
}
