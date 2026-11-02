namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicHistoryDto
    {
        public long AnchorApplicationId { get; set; }

        public long StudentId { get; set; }

        public List<AcademicHistoryRegistrationDto> Registrations { get; set; } = new();
    }
}
