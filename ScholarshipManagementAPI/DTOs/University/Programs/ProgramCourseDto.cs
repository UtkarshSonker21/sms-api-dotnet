namespace ScholarshipManagementAPI.DTOs.University.Programs
{
    public class ProgramCourseDto
    {
        public long? ProgramCourseId { get; set; }

        public long CourseId { get; set; }

        public string? CourseNameEn { get; set; }

        public string? CourseNameAr { get; set; }
        public string? CourseCode { get; set; }

        public byte CourseType { get; set; } // 1: Core, 2: Elective, 3: Optional

        public int Credits { get; set; }

        public int? DisplayOrder { get; set; }

        public int SemesterNo { get; set; }
        public string SemesterName { get; set; } = string.Empty;
        public bool? IsActive { get; set; }
    }
}
