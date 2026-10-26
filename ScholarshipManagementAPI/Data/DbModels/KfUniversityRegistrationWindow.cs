using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfUniversityRegistrationWindow
{
    public long UniversityRegistrationWindowId { get; set; }

    public int UniversityId { get; set; }

    public byte PeriodType { get; set; }

    public int AcademicYearStart { get; set; }

    public DateTime? RegistrationFrom { get; set; }

    public DateTime RegistrationTo { get; set; }

    public bool IsOpen { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = null!;
}
