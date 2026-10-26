namespace ScholarshipManagementAPI.DTOs.University.RegistrationWindows
{
    public class UniversityRegistrationWindowsResponseDto
    {
        public List<UniversityOptionDto> Universities { get; set; } = new();
        public List<UniversityRegistrationWindowResponseDto> Windows { get; set; } = new();
    }

    public class UniversityOptionDto
    {
        public long UniversityId { get; set; }
        public string UniversityName { get; set; } = string.Empty;
    }
}
