using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStudentSponsorshipAssignment
{
    public long AssignmentId { get; set; }

    public long ApplicationId { get; set; }

    public long SponsorshipCategoryMappingId { get; set; }

    public bool IsActive { get; set; }

    public long AssignedBy { get; set; }

    public DateTime AssignedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string? Remarks { get; set; }

    public virtual KfStudentProgramApplication Application { get; set; } = null!;

    public virtual KfUsersLogin AssignedByNavigation { get; set; } = null!;

    public virtual KfSponsorshipCategoryMapping SponsorshipCategoryMapping { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
