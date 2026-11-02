using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ScholarshipManagementAPI.DTOs.School.Students
{
    public class StudentRequestDto
    {
        public long? StudentId { get; set; }  // For Update, null for Create

        #region Personal Information

        [StringLength(100)]
        public string? StudentCode { get; set; }

        public string? PhotoPath { get; set; }

        [Required]
        [StringLength(200)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? SecondName { get; set; }

        [StringLength(200)]
        public string? ThirdName { get; set; }

        [Required]
        [StringLength(200)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? MotherName { get; set; }

        public DateTime? Dob { get; set; }

        public long? NationalityId { get; set; }

        public long? ResidenceCountryId { get; set; }

        [StringLength(200)]
        public string? Tribe { get; set; }

        public long? ReligionId { get; set; }

        public long? GenderId { get; set; }

        public bool? IsOrphan { get; set; }

        [StringLength(100)]
        public string? OrphanNumber { get; set; }

        #endregion

        #region Address

        [StringLength(200)]
        public string? City { get; set; }

        [StringLength(200)]
        public string? Village { get; set; }

        [StringLength(200)]
        public string? Block { get; set; }

        [StringLength(200)]
        public string? Street { get; set; }

        [StringLength(200)]
        public string? House { get; set; }

        [StringLength(100)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(250)]
        public string? Email { get; set; }

        #endregion

        #region Academic Information

        public bool FromDaSchool { get; set; }

        [StringLength(100)]
        public string? DaStudentCode { get; set; }

        [Required]
        public long SchoolId { get; set; }

        [StringLength(200)]
        public string? HsSpecialization { get; set; }

        [StringLength(300)]
        public string? TanzanianStudentCombination { get; set; }

        public decimal? TotalScore { get; set; }

        public decimal? MaxScore { get; set; }

        public decimal? RelativeGrade { get; set; }

        public decimal? EnglishScore { get; set; }

        #endregion

        #region Transfer Student

        [StringLength(300)]
        public string? TransferInstitution { get; set; }

        [StringLength(300)]
        public string? TransferProgram { get; set; }

        [StringLength(100)]
        public string? TransferInstitutionType { get; set; }

        public int? TransferCredits { get; set; }

        public DateTime? TransferLastSemEnd { get; set; }

        public decimal? TransferGpa { get; set; }

        // 0 = Spring, 1 = Summer, 2 = Fall
        public int? TransferPlacementSemester { get; set; }

        #endregion

        #region Behaviour & Social Evaluation

        public long? FinancialNeedStatusId { get; set; }

        public long? SelfRelianceLevelId { get; set; }

        public long? MotivationLevelId { get; set; }

        public long? FutureGoalsLevelId { get; set; }

        public string? RecommendationLetterPath { get; set; }

        [StringLength(2000)]
        public string? RecommendationLetterNotes { get; set; }

        [JsonIgnore]
        public IFormFile? RecommendationLetterFile { get; set; }

        #endregion

        #region Audit

        public bool? IsDraft { get; set; }

        public bool IsActive { get; set; } = true;

        public long CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public long? UpdatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        #endregion

        #region Response Only

        public string? FullName { get; set; }

        public string? SchoolName { get; set; }

        public string? NationalityName { get; set; }

        public string? ResidenceCountryName { get; set; }

        public string? ReligionName { get; set; }

        public string? GenderName { get; set; }

        public string? FinancialNeedStatusName { get; set; }

        public string? SelfRelianceLevelName { get; set; }

        public string? MotivationLevelName { get; set; }

        public string? FutureGoalsLevelName { get; set; }

        public string? FormattedCreatedBy { get; set; }

        public string? FormattedCreatedDate { get; set; }

        public string? FormattedUpdatedBy { get; set; }

        public string? FormattedUpdatedDate { get; set; }



        // Assigned Program
        public string? StudentAssignedProgramName { get; set; }
        public string? StudentAssignedUniversityName { get; set; }
        public long? StudentAssignedUniversityId { get; set; }
        public long? StudentApplicationStatusId { get; set; }

        // Document Completion
        public int RequiredDocumentCount { get; set; }
        public int SubmittedDocumentCount { get; set; }
        public int MissingDocumentCount { get; set; }
        public bool AllDocumentsSubmitted { get; set; }

        #endregion

    }


    public class UploadFileRequestDto
    {
        public IFormFile File { get; set; } = default!;
    }

}
