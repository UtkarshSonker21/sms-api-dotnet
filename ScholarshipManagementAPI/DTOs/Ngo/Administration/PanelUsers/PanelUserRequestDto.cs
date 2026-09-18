using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.Ngo.Administration.PanelUsers
{
    public class PanelUserRequestDto
    {
        // Staff (null/0 = Create, >0 = Update)
        public long? StaffId { get; set; }

        // Login
        public long? LoginId { get; set; }


        public long StaffType { get; set; }

        // Personal Information
        [Required]
        public string StaffSalutation { get; set; } = string.Empty;

        [Required]
        public string StaffFirstName { get; set; } = string.Empty;

        [Required]
        public string StaffLastName { get; set; } = string.Empty;

        // Response Only
        public string? FullName { get; set; }

        public long Gender { get; set; } 

        // Contact Information
        [Required]
        public string OfficialEmail { get; set; } = string.Empty;

        public string? PersonalEmail { get; set; }

        public string? MobileNumber { get; set; }

        public string? Remarks { get; set; }

        [Required]
        public string RecoveryEmail { get; set; } = string.Empty;

        // Role
        [Required]
        public long RoleId { get; set; }

        // Response Only
        public string? RoleName { get; set; }

        public string? LoginName { get; set; }

        //status
        public bool IsActive { get; set; }
        public bool IsDisabled { get; set; } = false;

        // Audit Information (Response Only)
        public DateTime? CreatedDate { get; set; }

        public long? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public long? UpdatedBy { get; set; }


        // check if the user is default user or not
        public bool IsDefaultRole{ get; set; }

    }
}
