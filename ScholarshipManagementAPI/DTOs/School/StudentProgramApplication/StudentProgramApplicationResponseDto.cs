using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;

public class StudentProgramApplicationResponseDto
{
    public long ApplicationId { get; set; }
    public long StudentId { get; set; }

    public long ProgramId { get; set; }
    public string ProgramName { get; set; } = null!;
    public string ProgramCode { get; set; } = null!;

    public int ApplicationStatus { get; set; }
    public string ApplicationStatusName { get; set; } = null!;

    public DateTime AppliedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }

    public string? Remarks { get; set; }
    public string? Notes { get; set; }

    public long CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }

    public bool IsAllRequiredDocumentsUploaded { get; set; }

    public List<RequiredDocumentDto> RequiredDocuments { get; set; } = new();
    public List<StudentProgramDocumentResponseDto> Documents { get; set; } = new();

    public string UniversityName { get; set; } = null!;
    public string FacultyName { get; set; } = null!;

}
