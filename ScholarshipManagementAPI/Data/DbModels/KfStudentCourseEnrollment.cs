using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentCourseEnrollment
{
    public long Id { get; set; }

    public long AcademicRegistrationId { get; set; }

    public long ProgramCourseId { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual KfStudentAcademicRegistration AcademicRegistration { get; set; } = null!;

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual KfStudentCourseResult? KfStudentCourseResult { get; set; }

    public virtual KfProgramCourse ProgramCourse { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
