namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicHistorySemesterResultDto
    {
        public decimal? SemesterGpa { get; set; }

        public string SubmissionStatus { get; set; } = string.Empty;

        public DateTime? CreatedOn { get; set; }

        public DateTime? UpdatedOn { get; set; }

        public DateTime? SubmittedOn { get; set; }
    }
}
