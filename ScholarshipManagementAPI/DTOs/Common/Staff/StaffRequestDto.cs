using System.ComponentModel.DataAnnotations;

namespace ScholarshipManagementAPI.DTOs.Common.Staff
{
    public class StaffRequestDto
    {
        public long? StaffId { get; set; }   // null / 0 = Create, >0 = Update


        [Required]
        public long StaffType { get; set; }          // UsersModule.ModuleId


        public List<long> UniversityIds { get; set; } = new();
        public List<long> SchoolIds { get; set; } = new();


        // ===== Personal Info =====
        [Required, StringLength(100)]
        public string StaffSalutation { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string StaffFirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string StaffLastName { get; set; } = string.Empty;

        [Required]
        public long Gender { get; set; }

        // ===== Address =====
        public long? PermCountryId { get; set; }

        [StringLength(200)]
        public string? PermAddress { get; set; }

        [StringLength(100)]
        public string? PermCity { get; set; }

        [StringLength(50)]
        public string? PermZipCode { get; set; }

        [StringLength(100)]
        public string? PermState { get; set; }



        // ===== Contact =====
        [Required, StringLength(100)]
        public string OfficialEmail { get; set; } = string.Empty;

        [EmailAddress, StringLength(100)]
        public string? PersonalEmail { get; set; }

        [StringLength(100)]
        public string? MobileNumber { get; set; }


        [StringLength(200)]
        public string? Photo { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        
        public bool IsActive { get; set; }
        public bool IsDisabled { get; set; }


        // ===== Extra (Response convenience) =====
        public string? StaffTypeName { get; set; } 

        public string? PermCountryName { get; set; }

        public DateTime CreatedDate { get; set; }
        public long CreatedBy { get; set; }
        public string? CreatedByName { get; set; }

        public DateTime? UpdatedDate { get; set; }
        public long? UpdatedBy { get; set; }
        public string? UpdatedByName { get; set; }


        public string? LoginName { get; set; }
    }
}
