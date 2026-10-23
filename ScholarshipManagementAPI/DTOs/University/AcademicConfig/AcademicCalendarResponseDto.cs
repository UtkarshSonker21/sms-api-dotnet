namespace ScholarshipManagementAPI.DTOs.University.AcademicConfig
{
    public class AcademicCalendarResponseDto
    {
        public long AcademicCalendarId { get; set; }
        public long UniversityId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public DateOnly PeriodStartDate { get; set; }
        public DateOnly PeriodEndDate { get; set; }
        public byte AcademicYearStartMonth { get; set; }
        public byte AcademicYearEndMonth { get; set; }
        public byte TermsPerYear { get; set; }
        public string NamingStyle { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
        public List<AcademicTermResponseDto> Terms { get; set; } = new();
    }
}
