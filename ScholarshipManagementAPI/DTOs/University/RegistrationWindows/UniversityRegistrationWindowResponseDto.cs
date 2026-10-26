namespace ScholarshipManagementAPI.DTOs.University.RegistrationWindows
{
    public class UniversityRegistrationWindowResponseDto
    {
        public long UniversityRegistrationWindowId { get; set; }
        public long UniversityId { get; set; }
        public string UniversityName { get; set; } = string.Empty;
        public byte PeriodType { get; set; }
        public int AcademicYearStart { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public DateTime? RegistrationFrom { get; set; }
        public DateTime RegistrationTo { get; set; }
        public bool IsOpen { get; set; }
        public bool IsCurrentlyOpen { get; set; }
        public string? Notes { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }
}
