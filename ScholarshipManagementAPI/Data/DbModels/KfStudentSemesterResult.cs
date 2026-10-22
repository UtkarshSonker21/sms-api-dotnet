using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentSemesterResult
{
    public long Id { get; set; }

    public long AcademicRegistrationId { get; set; }

    public decimal? SemesterGpa { get; set; }

    public string SubmissionStatus { get; set; } = null!;

    public long? SubmittedBy { get; set; }

    public DateTime? SubmittedOn { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual KfStudentAcademicRegistration AcademicRegistration { get; set; } = null!;

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual KfUsersLogin? SubmittedByNavigation { get; set; }

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
