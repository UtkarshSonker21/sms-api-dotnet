using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfProgramSemester
{
    public long ProgramSemesterId { get; set; }

    public long ProgramId { get; set; }

    public int SemesterNo { get; set; }

    public string? SemesterName { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? RegistrationStartDate { get; set; }

    public DateTime? RegistrationEndDate { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual ICollection<KfProgramCourse> KfProgramCourses { get; set; } = new List<KfProgramCourse>();

    public virtual ICollection<KfStudentAcademicRegistration> KfStudentAcademicRegistrations { get; set; } = new List<KfStudentAcademicRegistration>();

    public virtual KfProgram Program { get; set; } = null!;
}
