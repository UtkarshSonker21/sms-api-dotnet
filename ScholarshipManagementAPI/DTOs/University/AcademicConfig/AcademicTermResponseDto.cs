namespace ScholarshipManagementAPI.DTOs.University.AcademicConfig
{
    public class AcademicTermResponseDto
    {
        public long AcademicTermId { get; set; }
        public byte TermSequence { get; set; }
        public string TermName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public DateOnly RegistrationStartDate { get; set; }
        public DateOnly RegistrationEndDate { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
