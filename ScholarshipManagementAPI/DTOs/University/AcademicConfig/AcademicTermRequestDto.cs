namespace ScholarshipManagementAPI.DTOs.University.AcademicConfig
{
    public class AcademicTermRequestDto
    {
        public byte TermSequence { get; set; }
        public string TermName { get; set; } = string.Empty;
    }
}
