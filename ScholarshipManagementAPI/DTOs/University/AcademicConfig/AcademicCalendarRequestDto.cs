namespace ScholarshipManagementAPI.DTOs.University.AcademicConfig
{
    public class AcademicCalendarRequestDto
    {
        public long UniversityId { get; set; }
        public string AcademicYearName { get; set; } = string.Empty;
        public DateOnly PeriodStartDate { get; set; }
        public DateOnly PeriodEndDate { get; set; }
        public byte AcademicYearStartMonth { get; set; }
        public byte AcademicYearEndMonth { get; set; }
        public byte TermsPerYear { get; set; }
        public string NamingStyle { get; set; } = "Term";
        public List<AcademicTermRequestDto> Terms { get; set; } = new();
    }
}
