namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicProgressionResponseDto
    {
        public bool ConfirmationRequired { get; set; }

        public List<AcademicProgressionWarningDto> Warnings { get; set; } = new();

        public long? AcademicRegistrationId { get; set; }

        public long ApplicationId { get; set; }

        public long ProgramId { get; set; }

        public int CurrentSemesterNo { get; set; }

        public int NextSemesterNo { get; set; }

        public long NextProgramSemesterId { get; set; }

        public string? NextSemesterName { get; set; }

        public string? NextTermName { get; set; }

        public string? AcademicYearName { get; set; }

        public DateTime? SemesterStartDate { get; set; }

        public DateTime? SemesterEndDate { get; set; }

        public CourseEnrollmentSummaryDto? EnrollmentSummary { get; set; }
    }
}
