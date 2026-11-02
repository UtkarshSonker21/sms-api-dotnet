namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class AcademicResultsImportRowDto
    {
        public int RowNumber { get; set; }
        public string? CourseCode { get; set; }
        public string? Result { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class AcademicResultsImportPreviewDto
    {
        public long AcademicRegistrationId { get; set; }
        public int TotalRows { get; set; }
        public int ValidRows { get; set; }
        public int InvalidRows { get; set; }
        public bool CanSubmit { get; set; }
        public DateTime? ExpectedUpdatedOn { get; set; }
        public string? PreviewToken { get; set; }
        public string? Message { get; set; }
        public List<AcademicResultsImportRowDto> Rows { get; set; } = new();
    }
}
