using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;

public class ChangeStudentProgramStatusDto
{
    public int ApplicationStatusId { get; set; }

    [StringLength(1000)]
    public string? Remarks { get; set; }
}
