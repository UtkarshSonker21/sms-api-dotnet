namespace ScholarshipManagementAPI.DTOs.University.AcademicRegistration
{
    public class SaveCourseResultRequestDto
    {
        public long StudentCourseEnrollmentId { get; set; }

        public bool? IsPassed { get; set; }
    }
}
