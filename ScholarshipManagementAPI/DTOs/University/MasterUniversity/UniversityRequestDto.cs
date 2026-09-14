using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.University.MasterUniversity
{
    public class UniversityRequestDto
    {
        public long? UniversityId { get; set; }

        #region Basic Information

        public string UniversityName { get; set; } = string.Empty;

        public long? UniversityTypeId { get; set; }

        public string? CharterAccreditation { get; set; }

        public int? EstablishedYear { get; set; }

        public long CountryId { get; set; }

        public string City { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? Website { get; set; }

        #endregion

        #region Vice Chancellor

        public string? VcName { get; set; }

        public string? VcEmail { get; set; }

        public string? VcMobile { get; set; }

        #endregion

        #region Coordinator

        public string CoordName { get; set; } = string.Empty;

        public string? CoordPosition { get; set; }

        public string CoordEmail { get; set; } = string.Empty;

        public string CoordPhone { get; set; } = string.Empty;

        #endregion

        #region Statistics

        public int? FacultiesCount { get; set; }

        public int? FacultyFulltimeCount { get; set; }

        public int? AdminStaffCount { get; set; }

        public int? ProgDegreeCount { get; set; }

        public int? ProgDiplomaCount { get; set; }

        public int? ProgCertificateCount { get; set; }

        public int? ProgPostgradCount { get; set; }

        public int? StudentsTotal { get; set; }

        public int? StudentsEnrolled { get; set; }

        public decimal? IntlStudentsPct { get; set; }

        public long? StudentsGenderTypeId { get; set; }

        public int? StudDegreeCount { get; set; }

        public int? StudDiplomaCount { get; set; }

        public int? StudCertificateCount { get; set; }

        public int? StudPostgradCount { get; set; }

        public int? GraduatesTotal { get; set; }

        public int? AlumniCount { get; set; }

        #endregion

        #region Performance

        public decimal? OpSustainabilityPct { get; set; }

        public decimal? EmployabilityPct { get; set; }

        public decimal? PhdStaffPct { get; set; }

        public string? FteRatio { get; set; }

        public decimal? TeachingLoadHours { get; set; }

        public int? AnnualPublications { get; set; }

        public int? OnlineProgramsCount { get; set; }

        public int? IntlAccreditedProgramsCount { get; set; }

        public string? ExternalGrants { get; set; }

        public string? Notes { get; set; }

        #endregion

        #region Accreditation

        public byte AccreditationStatus { get; set; }

        public long? AccreditationBy { get; set; }

        public DateTime? AccreditationDate { get; set; }

        public string? CommitteeComment { get; set; }

        #endregion

        #region Audit

        public bool IsDraft { get; set; }

        public bool IsActive { get; set; }

        public long? CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public long? UpdatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        #endregion

        #region Response Only

        public string? CountryName { get; set; }

        public string? UniversityTypeName { get; set; }

        public string? StudentsGenderTypeName { get; set; }

        public string? AccreditationStatusName { get; set; }

        public string? AccreditationByName { get; set; }

        public string? CreatedByName { get; set; }

        public string? UpdatedByName { get; set; }

        public string? FormattedCreatedDate { get; set; }

        public string? FormattedUpdatedDate { get; set; }

        public long? ProgramsCount { get; set; }

        #endregion
    }
}
