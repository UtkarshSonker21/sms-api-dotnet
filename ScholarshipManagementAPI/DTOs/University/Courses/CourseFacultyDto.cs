namespace ScholarshipManagementAPI.DTOs.University.Courses
{
    public class CourseFacultyDto
    {
        public long FacultyId { get; set; }
        public string FacultyName { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
    }
}
