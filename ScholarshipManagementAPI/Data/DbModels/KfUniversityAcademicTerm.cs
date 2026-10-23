using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfUniversityAcademicTerm
{
    public long AcademicTermId { get; set; }

    public long AcademicCalendarId { get; set; }

    public byte TermSequence { get; set; }

    public string TermName { get; set; } = null!;

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual KfUniversityAcademicCalendar AcademicCalendar { get; set; } = null!;

    public virtual ICollection<KfProgramSemester> KfProgramSemesters { get; set; } = new List<KfProgramSemester>();
}
