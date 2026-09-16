using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfStaff
{
    public long StaffId { get; set; }

    /// <summary>
    /// university, school, ngo
    /// </summary>
    public long StaffType { get; set; }

    public string StaffSalutation { get; set; } = null!;

    public string StaffFirstName { get; set; } = null!;

    public string StaffLastName { get; set; } = null!;

    public long Gender { get; set; }

    public string? PermAddress { get; set; }

    public string? PermCity { get; set; }

    public string? PermZipCode { get; set; }

    public string? PermState { get; set; }

    public long? PermCountryId { get; set; }

    public string OfficialEmail { get; set; } = null!;

    public string? PersonalEmail { get; set; }

    public string? Photo { get; set; }

    public string? MobileNumber { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public long CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public bool IsDisabled { get; set; }

    public virtual KfUsersLogin CreatedByNavigation { get; set; } = null!;

    public virtual ZzMasterDropdown GenderNavigation { get; set; } = null!;

    public virtual ICollection<KfStaffSchoolCoordinatorMapping> KfStaffSchoolCoordinatorMappings { get; set; } = new List<KfStaffSchoolCoordinatorMapping>();

    public virtual ICollection<KfStaffUniversityCoordinatorMapping> KfStaffUniversityCoordinatorMappings { get; set; } = new List<KfStaffUniversityCoordinatorMapping>();

    public virtual ICollection<KfUsersLogin> KfUsersLogins { get; set; } = new List<KfUsersLogin>();

    public virtual ZzMasterCountry? PermCountry { get; set; }

    public virtual KfUsersModule StaffTypeNavigation { get; set; } = null!;

    public virtual KfUsersLogin? UpdatedByNavigation { get; set; }
}
