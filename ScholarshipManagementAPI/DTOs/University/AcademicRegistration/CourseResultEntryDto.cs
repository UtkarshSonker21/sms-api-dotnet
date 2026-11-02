namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class CourseResultEntryDto
    {
        public long StudentCourseEnrollmentId { get; set; }

        public long ProgramCourseId { get; set; }

        public long CourseId { get; set; }

        public string CourseCode { get; set; } = string.Empty;

        public string CourseName { get; set; } = string.Empty;

        public byte CourseType { get; set; }

        public int Credits { get; set; }

        public bool IsRetake { get; set; }

        public bool? IsPassed { get; set; }

        public string? Grade { get; set; }

        public string? Remarks { get; set; }
    }
}
