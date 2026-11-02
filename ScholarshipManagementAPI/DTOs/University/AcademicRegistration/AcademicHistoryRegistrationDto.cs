namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicHistoryRegistrationDto
    {
        public long AcademicRegistrationId { get; set; }

        public long ApplicationId { get; set; }

        public long ProgramId { get; set; }

        public string ProgramCode { get; set; } = string.Empty;

        public string ProgramName { get; set; } = string.Empty;

        public int SemesterNo { get; set; }

        public DateTime RegistrationDate { get; set; }

        public long? ProgramSemesterId { get; set; }

        public string ScheduleLinkStatus { get; set; } = string.Empty;

        public string? SemesterName { get; set; }

        public long? AcademicTermId { get; set; }

        public long? AcademicCalendarId { get; set; }

        public byte? TermSequence { get; set; }

        public string? TermName { get; set; }

        public string? AcademicYearName { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public List<string> DataIssues { get; set; } = new();

        public AcademicHistorySemesterResultDto? SemesterResult { get; set; }

        public List<AcademicHistoryCourseDto> Courses { get; set; } = new();
    }
}
