using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.University.Programs
{
    public class ProgramRequestDto
    {
        public long? ProgramId { get; set; }                               // For Update scenarios - Optional for Create, Required for Update

        [Required]
        public long UniversityId { get; set; }

        [Required]
        public long FacultyId { get; set; }

        [Required]
        [StringLength(300)]
        public string ProgramName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string ProgramCode { get; set; } = string.Empty;

        [Required]
        public long Degree { get; set; }

        public int NumberOfSemesters { get; set; }

        public int CreditsRequired { get; set; }

        public int AllowedStudentSeats { get; set; }

        public decimal? MinAcceptanceRate { get; set; }

        public string? AllowedHighSchoolDivisions { get; set; }

        public bool IsDraft { get; set; }

        public byte AccreditationStatus { get; set; }

        public long? AccreditationBy { get; set; }

        public DateTime? AccreditationDate { get; set; }

        public string? CommitteeComment { get; set; }

        public DateTime? SubmittedDate { get; set; }

        public string? AllowedTanzanianCombinations { get; set; }

        public bool IsActive { get; set; } = true;

        // Response Only
        public string? UniversityName { get; set; }
        public string? FacultyName { get; set; }
        public string? FacultyCode { get; set; }

        public DateTime CreatedDate { get; set; }
        public long CreatedBy { get; set; }
        public string? CreatedByName { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }

        // Child Collections
        public List<ProgramDocumentDto>? Documents { get; set; }

        public List<ProgramCostDto>? Costs { get; set; }

        public List<ProgramCourseDto>? Courses { get; set; }

        public List<ProgramSemesterDto>? Semesters { get; set; }
    }
}
