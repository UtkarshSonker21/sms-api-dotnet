using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentCourseResult
{
    public long Id { get; set; }

    public long StudentCourseEnrollmentId { get; set; }

    public string? Grade { get; set; }

    public bool? IsPassed { get; set; }

    public string? Remarks { get; set; }

    public long? RecordedBy { get; set; }

    public DateTime? RecordedOn { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual KfUsersLogin? RecordedByNavigation { get; set; }

    public virtual KfStudentCourseEnrollment StudentCourseEnrollment { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
