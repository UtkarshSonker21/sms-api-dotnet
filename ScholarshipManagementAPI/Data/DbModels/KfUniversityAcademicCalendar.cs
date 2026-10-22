using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfUniversityAcademicCalendar
{
    public long AcademicCalendarId { get; set; }

    public long UniversityId { get; set; }

    public string AcademicYearName { get; set; } = null!;

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public byte AcademicYearStartMonth { get; set; }

    public byte AcademicYearEndMonth { get; set; }

    public byte TermsPerYear { get; set; }

    public string NamingStyle { get; set; } = null!;

    public bool ReusePreviousCalendar { get; set; }

    public long? ReusedFromCalendarId { get; set; }

    public long CreatedBy { get; set; }

    public DateTime CreatedOn { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public virtual ICollection<KfUniversityAcademicCalendar> InverseReusedFromCalendar { get; set; } = new List<KfUniversityAcademicCalendar>();

    public virtual ICollection<KfUniversityAcademicTerm> KfUniversityAcademicTerms { get; set; } = new List<KfUniversityAcademicTerm>();

    public virtual KfUniversityAcademicCalendar? ReusedFromCalendar { get; set; }
}
