using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfUniversity
{
    public long UniversityId { get; set; }

    public string UniversityName { get; set; } = null!;

    public long? UniversityType { get; set; }

    public string? CharterAccreditation { get; set; }

    public int? EstablishedYear { get; set; }

    public long CountryId { get; set; }

    public string City { get; set; } = null!;

    public string? Address { get; set; }

    public string? Website { get; set; }

    public string? VcName { get; set; }

    public string? VcEmail { get; set; }

    public string? VcMobile { get; set; }

    public string CoordName { get; set; } = null!;

    public string? CoordPosition { get; set; }

    public string CoordEmail { get; set; } = null!;

    public string CoordPhone { get; set; } = null!;

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

    public byte AccreditationStatus { get; set; }

    public long? AccreditationBy { get; set; }

    public bool IsDraft { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public DateTime? AccreditationDate { get; set; }

    public string? CommitteeComment { get; set; }

    public virtual KfUsersLogin? AccreditationByNavigation { get; set; }

    public virtual ZzMasterCountry Country { get; set; } = null!;

    public virtual KfUsersLogin? CreatedByNavigation { get; set; }

    public virtual ICollection<KfCourse> KfCourses { get; set; } = new List<KfCourse>();

    public virtual ICollection<KfFaculty> KfFaculties { get; set; } = new List<KfFaculty>();

    public virtual ICollection<KfProgram> KfPrograms { get; set; } = new List<KfProgram>();

    public virtual ICollection<KfStaffUniversityCoordinatorMapping> KfStaffUniversityCoordinatorMappings { get; set; } = new List<KfStaffUniversityCoordinatorMapping>();

    public virtual ICollection<KfUniversityAcademicCalendar> KfUniversityAcademicCalendars { get; set; } = new List<KfUniversityAcademicCalendar>();

    public virtual ZzMasterDropdown? StudentsGenderType { get; set; }

    public virtual ZzMasterDropdown? UniversityTypeNavigation { get; set; }

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
