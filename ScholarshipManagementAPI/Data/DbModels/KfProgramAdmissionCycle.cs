using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfProgramAdmissionCycle
{
    public long AdmissionCycleId { get; set; }

    public long ProgramId { get; set; }

    public string CycleName { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? ModifiedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public virtual ICollection<KfStudentProgramApplication> KfStudentProgramApplications { get; set; } = new List<KfStudentProgramApplication>();

    public virtual KfProgram Program { get; set; } = null!;
}
