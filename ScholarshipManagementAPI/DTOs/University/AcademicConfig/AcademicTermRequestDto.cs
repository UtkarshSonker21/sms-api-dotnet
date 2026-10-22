namespace ScholarshipManagementAPI.DTOs.University.AcademicConfig
{
    public class AcademicTermRequestDto
    {
        public byte TermSequence { get; set; }
        public string TermName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public DateOnly RegistrationStartDate { get; set; }
        public DateOnly RegistrationEndDate { get; set; }
    }
}
