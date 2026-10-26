namespace ScholarshipManagementAPI.DTOs.University.RegistrationWindows
{
    public class UniversityRegistrationWindowRequestDto
    {
        public long UniversityId { get; set; }
        public byte PeriodType { get; set; }
        public int AcademicYearStart { get; set; }
        public DateTime RegistrationFrom { get; set; }
        public DateTime RegistrationTo { get; set; }
        public string? Notes { get; set; }
        public string? RowVersion { get; set; }
    }
}
