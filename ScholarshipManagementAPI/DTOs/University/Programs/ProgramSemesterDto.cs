namespace ScholarshipManagementAPI.DTOs.University.Programs
{
    public class ProgramSemesterDto
    {
        public long ProgramSemesterId { get; set; }

        public int SemesterNo { get; set; }

        public string SemesterName { get; set; } = string.Empty;

        public long? AcademicTermId { get; set; }

        public long? AcademicCalendarId { get; set; }

        public string? AcademicYearName { get; set; }

        public byte? TermSequence { get; set; }

        public string? TermName { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime? RegistrationStartDate { get; set; }

        public DateTime? RegistrationEndDate { get; set; }
    }
}
