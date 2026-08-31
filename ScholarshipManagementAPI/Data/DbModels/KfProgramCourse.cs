using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfProgramCourse
{
    public long ProgramCourseId { get; set; }

    public long ProgramId { get; set; }

    public long CourseId { get; set; }

    public byte CourseType { get; set; }

    public int Credits { get; set; }

    public int? DisplayOrder { get; set; }

    public int SemesterNo { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual KfCourse Course { get; set; } = null!;

    public virtual KfProgram Program { get; set; } = null!;
}
