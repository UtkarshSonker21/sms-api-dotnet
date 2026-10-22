using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfUniversityAcademicTerm
{
    public long AcademicTermId { get; set; }

    public long AcademicCalendarId { get; set; }

    public byte TermSequence { get; set; }

    public string TermName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public DateOnly RegistrationStartDate { get; set; }

    public DateOnly RegistrationEndDate { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual KfUniversityAcademicCalendar AcademicCalendar { get; set; } = null!;
}
