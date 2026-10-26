using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentProgramApplication
{
    public long ApplicationId { get; set; }

    public long StudentId { get; set; }

    public long ProgramId { get; set; }

    public int ApplicationStatus { get; set; }

    public DateTime AppliedDate { get; set; }

    public DateTime? SubmittedDate { get; set; }

    public string? Remarks { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public long? AdmissionCycleId { get; set; }

    public virtual KfProgramAdmissionCycle? AdmissionCycle { get; set; }

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<KfStudentAcademicRegistration> KfStudentAcademicRegistrations { get; set; } = new List<KfStudentAcademicRegistration>();

    public virtual ICollection<KfStudentHistory> KfStudentHistories { get; set; } = new List<KfStudentHistory>();

    public virtual ICollection<KfStudentProgramDocument> KfStudentProgramDocuments { get; set; } = new List<KfStudentProgramDocument>();

    public virtual KfProgram Program { get; set; } = null!;

    public virtual KfStudentRegistration Student { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
