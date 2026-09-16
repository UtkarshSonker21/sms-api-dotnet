using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.Ngo.Administration.SchoolCoordinators
{
    public class SchoolCoordinatorRequestDto
    {
        // Staff (null/0 = Create, >0 = Update)
        public long? StaffId { get; set; }

        // Login
        public long? LoginId { get; set; }

        public long StaffType { get; set; }

        // Schools Mapping
        [Required(ErrorMessage = "At least one school must be selected.")]
        public List<long> SchoolIds { get; set; } = new();

        // Response Only
        public List<string> SchoolNames { get; set; } = new();

        // Personal Information
        [Required]
        public string StaffSalutation { get; set; } = string.Empty;

        [Required]
        public string StaffFirstName { get; set; } = string.Empty;

        [Required]
        public string StaffLastName { get; set; } = string.Empty;

        // Response Only
        public string? FullName { get; set; }

        [Required]
        public long Gender { get; set; }

        // Contact Information
        [Required]
        [EmailAddress]
        public string OfficialEmail { get; set; } = string.Empty;

        public string? PersonalEmail { get; set; }

        public string? MobileNumber { get; set; }

        public string? Remarks { get; set; }

        [Required]
        [EmailAddress]
        public string RecoveryEmail { get; set; } = string.Empty;

        // Role
        [Required]
        public long RoleId { get; set; }

        // Response Only
        public string? RoleName { get; set; }

        // Response Only
        public string? LoginName { get; set; }

        // Status
        public bool IsActive { get; set; }
        public bool IsDisabled { get; set; }

        // Audit Information (Response Only)
        public DateTime? CreatedDate { get; set; }

        public long? CreatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public long? UpdatedBy { get; set; }

        // Response Only
        public bool IsDefaultRole { get; set; }

        public List<long> CountryIds { get; set; } = new();

        public List<string> CountryNames { get; set; } = new();


    }
}
