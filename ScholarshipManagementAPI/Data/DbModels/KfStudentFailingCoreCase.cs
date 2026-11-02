using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentFailingCoreCase
{
    public long Id { get; set; }

    public long AcademicRegistrationId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime SubmittedOn { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public string? DecisionComment { get; set; }

    public long? DecisionBy { get; set; }

    public DateTime? DecisionOn { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual KfStudentAcademicRegistration AcademicRegistration { get; set; } = null!;

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual KfUsersLogin? DecisionByNavigation { get; set; }

    public virtual ICollection<KfStudentFailingCoreCaseCourse> KfStudentFailingCoreCaseCourses { get; set; } = new List<KfStudentFailingCoreCaseCourse>();
}
