using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class SaveAcademicResultsRequestDto
    {
        [Required(ErrorMessage = "Enter the semester GPA (0.00 – 4.00).")]
        [Range(typeof(decimal), "0.00", "4.00", ErrorMessage = "Enter the semester GPA (0.00 – 4.00).")]
        public decimal? SemesterGpa { get; set; }

        public string? InternalNote { get; set; }

        public DateTime? ExpectedUpdatedOn { get; set; }

        public List<SaveCourseResultRequestDto> CourseResults { get; set; } = new();
    }
}
