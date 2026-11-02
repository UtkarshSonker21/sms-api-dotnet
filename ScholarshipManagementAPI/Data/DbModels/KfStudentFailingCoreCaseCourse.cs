using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentFailingCoreCaseCourse
{
    public long Id { get; set; }

    public long FailingCoreCaseId { get; set; }

    public long StudentCourseResultId { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual KfStudentFailingCoreCase FailingCoreCase { get; set; } = null!;

    public virtual ICollection<KfStudentCourseEnrollment> KfStudentCourseEnrollments { get; set; } = new List<KfStudentCourseEnrollment>();

    public virtual KfStudentCourseResult StudentCourseResult { get; set; } = null!;
}
