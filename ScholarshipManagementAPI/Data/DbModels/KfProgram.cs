using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfProgram
{
    public long ProgramId { get; set; }

    public long UniversityId { get; set; }

    public long FacultyId { get; set; }

    public string ProgramName { get; set; } = null!;

    public string ProgramCode { get; set; } = null!;

    public long Degree { get; set; }

    public int NumberOfSemesters { get; set; }

    public int CreditsRequired { get; set; }

    public int AllowedStudentSeats { get; set; }

    public decimal? MinAcceptanceRate { get; set; }

    public string? AllowedHighSchoolDivisions { get; set; }

    public bool IsDraft { get; set; }

    public byte AccreditationStatus { get; set; }

    public string? CommitteeComment { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public bool IsActive { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? AllowedTanzanianCombinations { get; set; }

    public DateTime? AccreditationDate { get; set; }

    public long? AccreditationBy { get; set; }

    public virtual KfUsersLogin? AccreditationByNavigation { get; set; }

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual ZzMasterDropdown DegreeNavigation { get; set; } = null!;

    public virtual KfFaculty Faculty { get; set; } = null!;

    public virtual ICollection<KfProgramCost> KfProgramCosts { get; set; } = new List<KfProgramCost>();

    public virtual ICollection<KfProgramCourse> KfProgramCourses { get; set; } = new List<KfProgramCourse>();

    public virtual ICollection<KfProgramDocument> KfProgramDocuments { get; set; } = new List<KfProgramDocument>();

    public virtual ICollection<KfProgramRegistrationWindow> KfProgramRegistrationWindows { get; set; } = new List<KfProgramRegistrationWindow>();

    public virtual ICollection<KfProgramSemester> KfProgramSemesters { get; set; } = new List<KfProgramSemester>();

    public virtual ICollection<KfStudentAcademicRegistration> KfStudentAcademicRegistrations { get; set; } = new List<KfStudentAcademicRegistration>();

    public virtual ICollection<KfStudentProgramApplication> KfStudentProgramApplications { get; set; } = new List<KfStudentProgramApplication>();

    public virtual KfUniversity University { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
